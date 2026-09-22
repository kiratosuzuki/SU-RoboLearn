using UnityEngine;

public class InitialRandomSettings : MonoBehaviour
{
    public static InitialRandomSettings Instance { get; private set; }

    [Header("Kitchen")]
    public GameObject person;
    public Transform kitchenPos0;
    public Transform kitchenPos1;
    public bool useLocal_K_Position = false;

    [Header("Tray (連動オブジェクト)")]
    public GameObject tray;
    public Transform trayPos0;
    public Transform trayPos1;
    public bool useLocal_T_Position = false;

    [Header("Customer")]
    public GameObject customer;
    public Transform cusPos0;
    public Transform cusPos1;
    public Transform cusPos2;
    public Transform cusPos3;
    public bool useLocal_C_Position = false;

    [Header("Customer Rotation Override")]
    public Vector3 cusRotation0 = new Vector3(270f, 90f, 0f);
    public Vector3 cusRotation1 = new Vector3(270f, 90f, 0f);
    public Vector3 cusRotation2 = new Vector3(270f, 0f, 180f);
    public Vector3 cusRotation3 = new Vector3(270f, 270f, 0f);

    [Header("Robot Initial Direction")]
    public GameObject robot;

    [Header("Foods (parent)")]
    public Transform foodsParent;

    [Header("Behavior")]
    public bool randomizeOnStart = true;
    [Range(0, 1)]
    public int forcedPersonIndex = 0;
    [Range(0, 3)]
    public int forcedcusIndex = 0;

    public bool enableLogs = true;

    // ====== 外部参照用 ======
    public string wantItem;      // 注文商品1  ({wantItem})
    public string wantItem2;     // 注文商品2  ({wantItem2})
    public string wantItem3;     // 注文商品3  ({wantItem3})
    public string soldOutItem;   // 売り切れ1  ({soldOutItem})
    public string soldOutItem2;  // 売り切れ2  ({soldOutItem2})
    public string soldOutItem3;  // 売り切れ3  ({soldOutItem3})
    public string dummyItem;     // ダミー     ({dummyItem})

    // idx=0 → "右"、idx=1 → "左"  ({kitchenSide})
    public string kitchenSide => idx == 0 ? "右" : "左";

    // cusidx → テーブル番号文字列  ({customerTable})
    // 0→"1"、1→"2"、2→"3"、3→"2"（pos3はpos1と同じ番号扱い）
    public string customerTableNo
    {
        get
        {
            switch (cusidx)
            {
                case 0:  return "テーブル1";
                case 1:  return "テーブル2";
                case 2:  return "テーブル3";
                case 3:  return "テーブル2";
                default: return $"テーブル{cusidx}";
            }
        }
    }

    public int idx;
    public int cusidx;
    public int robotDirIdx;

    // ====== ★重要：食品インデックス（状態） ======
    int want;
    int want2;
    int want3;
    int soldOut;
    int soldOut2;
    int soldOut3;
    int dummy;

    static readonly string[] directionNames  = { "North (0°)", "East (90°)", "South (180°)", "West (270°)" };
    static readonly float[]  directionAngles = { 0f, 90f, 180f, 270f };

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ChooseItems();

        //=========== キッチンの位置 ===========
        bool skipKitchen = CompetitionSettings.Instance != null
            && CompetitionSettings.Instance.skipRecognizeKitchen;

        idx = skipKitchen ? 0
            : randomizeOnStart ? Random.Range(0, 2)
            : Mathf.Clamp(forcedPersonIndex, 0, 1);

        SetKitchenPositionByIndex(idx);
        SetTrayPositionByIndex(idx);

        //=========== 客の位置 ===========
        bool customerRandom = CompetitionSettings.Instance != null
            ? CompetitionSettings.Instance.moveToCustomerBeta
            : true;

        cusidx = customerRandom ? Random.Range(0, 4) : 0;
        SetcusPositionByIndex(cusidx);

        //=========== ロボットの初期向き ===========
        bool doorBeta = CompetitionSettings.Instance != null
            && CompetitionSettings.Instance.doorOpenBeta;

        robotDirIdx = doorBeta ? Random.Range(0, 4) : 2;
        SetRobotDirectionByIndex(robotDirIdx);

        if (enableLogs)
        {
            Debug.Log($"Kitchen Location: {(idx == 0 ? "A (kitchenPos0)" : "B (kitchenPos1)")}");
            Debug.Log($"Customer Seat: {cusidx}");
            Debug.Log($"Robot Initial Direction: {directionNames[robotDirIdx]}");
        }
    }

    //=====================================================
    // 食品ランダム選択（注文商品3つ＋売り切れ3つ、最低7品必要）
    //=====================================================
    void ChooseItems()
    {
        int n = foodsParent.childCount;

        if (n < 7)
        {
            Debug.LogError($"foodsParent の子オブジェクトが {n} 個しかありません。注文3品＋売り切れ3品＋ダミー1品で最低7品必要です。");
            return;
        }

        // 全部非表示
        for (int i = 0; i < n; i++)
            foodsParent.GetChild(i).gameObject.SetActive(false);

        // 全インデックスをシャッフル
        var pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < n; i++) pool.Add(i);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        // 注文3つ・売り切れ3つ・ダミー1つを順に割り当て
        want   = pool[0];
        want2  = pool[1];
        want3  = pool[2];
        soldOut  = pool[3];
        soldOut2 = pool[4];
        soldOut3 = pool[5];
        dummy    = pool[6];

        wantItem     = foodsParent.GetChild(want).name;
        wantItem2    = foodsParent.GetChild(want2).name;
        wantItem3    = foodsParent.GetChild(want3).name;
        soldOutItem  = foodsParent.GetChild(soldOut).name;
        soldOutItem2 = foodsParent.GetChild(soldOut2).name;
        soldOutItem3 = foodsParent.GetChild(soldOut3).name;
        dummyItem    = foodsParent.GetChild(dummy).name;

        if (enableLogs)
        {
            Debug.Log($"WantItem1:  {wantItem}");
            Debug.Log($"WantItem2:  {wantItem2}");
            Debug.Log($"WantItem3:  {wantItem3}");
            Debug.Log($"SoldOut1:   {soldOutItem}");
            Debug.Log($"SoldOut2:   {soldOutItem2}");
            Debug.Log($"SoldOut3:   {soldOutItem3}");
            Debug.Log($"Dummy:      {dummyItem}");
        }

    }

    //=====================================================
    // キッチンの移動
    //=====================================================
    public void SetKitchenPositionByIndex(int idx)
    {
        if (person == null) return;

        Transform target = (idx == 0) ? kitchenPos0 : kitchenPos1;

        if (useLocal_K_Position)
            person.transform.localPosition = target.localPosition;
        else
            person.transform.position = target.position;

        person.transform.rotation =
            (idx == 0) ? Quaternion.Euler(0f, 180f, 0f)
                       : Quaternion.Euler(0f, 0f, 0f);
    }

    //=====================================================
    // tray の移動
    //=====================================================
    public void SetTrayPositionByIndex(int idx)
    {
        if (tray == null) return;

        Transform target = (idx == 0) ? trayPos0 : trayPos1;

        if (useLocal_T_Position)
            tray.transform.localPosition = target.localPosition;
        else
            tray.transform.position = target.position;

        tray.transform.rotation =
            (idx == 0) ? Quaternion.Euler(0f, 0f, 0f)
                       : Quaternion.Euler(0f, 180f, 0f);
    }

    //=====================================================
    // 客の移動
    //=====================================================
    public void SetcusPositionByIndex(int idx)
    {
        if (customer == null) return;

        Transform target =
            (idx == 0) ? cusPos0 :
            (idx == 1) ? cusPos1 :
            (idx == 2) ? cusPos2 : cusPos3;

        if (useLocal_C_Position)
            customer.transform.localPosition = target.localPosition;
        else
            customer.transform.position = target.position;

        customer.transform.rotation = Quaternion.Euler(
            idx == 0 ? cusRotation0 :
            idx == 1 ? cusRotation1 :
            idx == 2 ? cusRotation2 : cusRotation3
        );

    }

    //=====================================================
    // ロボットの初期向き設定
    //=====================================================
    public void SetRobotDirectionByIndex(int idx)
    {
        if (robot == null) return;

        robot.transform.rotation = Quaternion.Euler(270f, directionAngles[idx], 0f);
    }
}
