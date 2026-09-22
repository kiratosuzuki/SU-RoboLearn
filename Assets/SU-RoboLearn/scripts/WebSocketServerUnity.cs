using UnityEngine;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.Collections.Generic;
using System;

public class WebSocketServerUnity : MonoBehaviour
{
    private WebSocketServer wssv;

    [Header("Robot")]
    public Transform baseFootprint;
    public Camera robotCamera;
    public float maxDistance = 50f;
    public LayerMask detectLayer = ~0;

    [Header("Components")]
    public TalkGoal talkGoal;
    public HsrSmoothGripper gripper;
    public SpeechBubble bubble;
    public ArmLifter armLifter;

    [Header("Camera Topic")]
    public float cameraScanInterval = 0.5f;

    [Header("Talk Reply Delay")]
    public float replyDelay = 4f;

    [Header("Talk Block (会話中は移動コマンドをブロックする時間)")]
    public float talkBlockDuration = 4f;

    [Header("Noise Settings (実環境ノイズ再現)")]
    [Tooltip("move指令に対する実移動量の倍率(%)。100が指令通り、110なら常に1.1倍")]
    [Range(50f, 150f)] public float moveNoisePercent = 100f;
    [Tooltip("turn指令に対する実回転量の倍率(%)。100が指令通り、110なら常に1.1倍")]
    [Range(50f, 150f)] public float turnNoisePercent = 100f;


    // Movement parameters
    private float linearSpeed = 0.5f;
    private float angularSpeed = 60f;

    // Movement state
    private bool isExecuting = false;
    private string currentCommand = "";
    private float turnDirSign = 1f;

    // Duration
    private float moveTimer = 0f;
    private float moveTimeout = 0f;

    // Delay state
    private bool delayMode = false;
    private float delayTimer = 0f;
    private float delayDuration = 0f;
    private string pendingResponse = "";

    // Camera publish
    private float cameraScanTimer = 0f;

    // Talk block: 会話中（発話断片の受付〜応答確定まで）は移動コマンドを止める
    private float talkBlockUntilTime = 0f;

    // Command queue (written from WS thread, read from main thread)
    private readonly Queue<Command> commandQueue = new Queue<Command>();
    private readonly Queue<string> talkQueue = new Queue<string>();

    private struct Command
    {
        public string type;
        public float value;
        public bool hasValue;
    }


    // ======================================================
    // Lifecycle
    // ======================================================
    void Start()
    {
        wssv = new WebSocketServer(8080);

        // /unity/order  — Scratch → Unity (コマンド受信)
        wssv.AddWebSocketService<OrderService>("/unity/order", () =>
        {
            var s = new OrderService();
            s.OnCommand = EnqueueRaw;
            return s;
        });

        // /unity/camera   — Unity → Scratch (常時スキャン配信)
        wssv.AddWebSocketService<PassiveService>("/unity/camera");

        // /unity/response — Unity → Scratch (行動完了通知)
        wssv.AddWebSocketService<PassiveService>("/unity/response");

        // /unity/reply    — Unity → Scratch (talk応答テキスト)
        wssv.AddWebSocketService<PassiveService>("/unity/reply");

        wssv.Start();
        Debug.Log("🌐 WebSocket Server started: ws://localhost:8080");
        Debug.Log("  order    → ws://localhost:8080/unity/order");
        Debug.Log("  camera   → ws://localhost:8080/unity/camera");
        Debug.Log("  response → ws://localhost:8080/unity/response");
        Debug.Log("  reply    → ws://localhost:8080/unity/reply");
    }

    void OnTalkReplyReady(string reply)
    {
        // 応答表示中も移動をブロックしておく
        talkBlockUntilTime = Time.time + replyDelay;
        StartCoroutine(BroadcastReplyAndDone(reply));
    }

    private System.Collections.IEnumerator BroadcastReplyAndDone(string reply)
    {
        yield return new WaitForSeconds(replyDelay);
        Broadcast("/unity/reply", reply);
        Broadcast("/unity/response", "done:talk");
    }

    void OnDestroy() => wssv?.Stop();
    void OnApplicationQuit() => wssv?.Stop();

    void Update()
    {
        ProcessTalkQueue();
        ProcessMovement();
        PublishCamera();
    }


    // ======================================================
    // コマンド受付 (WSスレッド → メインスレッド橋渡し)
    // ======================================================
    private void EnqueueRaw(string msg)
    {
        if (msg.StartsWith("talk:"))
        {
            string text = msg.Substring(5).Trim();
            lock (talkQueue)
                talkQueue.Enqueue(text);
            return;
        }

        Command cmd = Parse(msg);
        lock (commandQueue)
            commandQueue.Enqueue(cmd);
    }

    private Command Parse(string msg)
    {
        var cmd = new Command { type = msg, value = 0f };

        if (msg.Contains(":"))
        {
            string[] parts = msg.Split(':');
            cmd.type = parts[0];
            if (parts.Length > 1 && float.TryParse(parts[1], out float v))
            {
                cmd.value = v;
                cmd.hasValue = true;
            }
        }

        return cmd;
    }


    // ======================================================
    // talk受付 (isExecutingのロックとは独立に、届き次第すぐ処理する)
    // ======================================================
    private void ProcessTalkQueue()
    {
        while (true)
        {
            string text;
            lock (talkQueue)
            {
                if (talkQueue.Count == 0) return;
                text = talkQueue.Dequeue();
            }

            bubble.Say(text);
            TTSManager.Instance?.Speak(text);
            string reply = talkGoal.HandleUserText(text);
            talkBlockUntilTime = Time.time + talkBlockDuration;
            OnTalkReplyReady(reply);
        }
    }


    // ======================================================
    // カメラトピック配信 (メインスレッド)
    // ======================================================
    private void PublishCamera()
    {
        cameraScanTimer += Time.deltaTime;
        if (cameraScanTimer < cameraScanInterval) return;
        cameraScanTimer = 0f;

        string result = DoScan();
        Broadcast("/unity/camera", result);
    }


    // ======================================================
    // 行動処理 (メインスレッド)
    // ======================================================
    private void ProcessMovement()
    {
        if (baseFootprint == null) return;

        // 会話中（発話断片の受付〜応答確定・表示まで）は移動コマンドをブロック
        if (Time.time < talkBlockUntilTime) return;

        // 遅延待ち
        if (delayMode)
        {
            delayTimer += Time.deltaTime;
            if (delayTimer < delayDuration) return;

            delayMode = false;
            isExecuting = false;
            if (!string.IsNullOrEmpty(pendingResponse))
            {
                Broadcast("/unity/response", pendingResponse);
                pendingResponse = "";
            }
            return;
        }

        // 次のコマンドをデキュー
        if (!isExecuting)
        {
            lock (commandQueue)
            {
                if (commandQueue.Count == 0) return;

                Command cmd = commandQueue.Dequeue();
                currentCommand = cmd.type;

                if (currentCommand == "move")
                {
                    float dist = cmd.hasValue ? Mathf.Abs(cmd.value) / 100f : 1.0f;
                    dist *= moveNoisePercent / 100f;
                    moveTimeout = dist / linearSpeed;
                }
                else if (currentCommand == "turn")
                {
                    float angle = cmd.hasValue ? cmd.value : 90f;
                    angle *= turnNoisePercent / 100f;
                    turnDirSign = Mathf.Sign(angle);
                    moveTimeout = Mathf.Abs(angle) / angularSpeed;
                }

                isExecuting = true;
                moveTimer = 0f;
            }
        }

        if (!isExecuting) return;

        switch (currentCommand)
        {
            case "move":
            {
                moveTimer += Time.deltaTime;
                baseFootprint.position += -baseFootprint.right * linearSpeed * Time.deltaTime;
                if (moveTimer >= moveTimeout)
                {
                    isExecuting = false;
                    Broadcast("/unity/response", "done:move");
                }
                break;
            }

            case "turn":
            {
                moveTimer += Time.deltaTime;
                baseFootprint.rotation *= Quaternion.AngleAxis(angularSpeed * turnDirSign * Time.deltaTime, Vector3.forward);
                if (moveTimer >= moveTimeout)
                {
                    isExecuting = false;
                    Broadcast("/unity/response", "done:turn");
                }
                break;
            }

            case "open":
                gripper?.Open();
                StartDelay(2f, "done:open");
                break;

            case "close":
                gripper?.Close();
                StartDelay(2f, "done:close");
                break;

            case "arm_up":
                armLifter?.MoveUp();
                StartDelay(1f, "done:arm_up");
                break;

            case "arm_down":
                armLifter?.MoveDown();
                StartDelay(1f, "done:arm_down");
                break;

            default:
                isExecuting = false;
                break;
        }
    }

    private void StartDelay(float duration, string responseAfter)
    {
        delayDuration = duration;
        delayTimer = 0f;
        delayMode = true;
        pendingResponse = responseAfter;
        isExecuting = false;
    }


    // ======================================================
    // スキャン
    // ======================================================
    private string DoScan()
    {
        if (robotCamera == null) return "none";

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(robotCamera);
        Collider[] hits = Physics.OverlapSphere(robotCamera.transform.position, maxDistance, detectLayer);

        var detected = new List<string>();
        foreach (var hit in hits)
        {
            if (GeometryUtility.TestPlanesAABB(planes, hit.bounds) && hit.CompareTag("scan"))
                detected.Add(hit.gameObject.name);
        }

        return detected.Count > 0 ? string.Join(",", detected) : "none";
    }


    // ======================================================
    // ブロードキャスト
    // ======================================================
    private void Broadcast(string path, string message)
    {
        try
        {
            wssv.WebSocketServices[path].Sessions.Broadcast(message);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Broadcast failed [{path}]: {e.Message}");
        }
    }


    // ======================================================
    // Gizmos
    // ======================================================
    private void OnDrawGizmos()
    {
        if (robotCamera == null) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(robotCamera.transform.position, maxDistance);
    }
}


// ======================================================
// OrderService — /unity/order
// Scratch からコマンドを受け取るだけ
// ======================================================
public class OrderService : WebSocketBehavior
{
    public Action<string> OnCommand;

    protected override void OnMessage(MessageEventArgs e)
    {
        OnCommand?.Invoke(e.Data);
    }
}


// ======================================================
// PassiveService — /unity/camera, /unity/response
// クライアントが接続してブロードキャストを受け取るだけ
// ======================================================
public class PassiveService : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e) { }
}
