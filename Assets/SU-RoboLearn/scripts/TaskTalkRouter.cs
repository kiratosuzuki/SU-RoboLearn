using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// シーン内の全 TalkCondition にテキストを流すディスパッチャー。
/// 発話ごとに即座に判定する。分割発話（例:「売り切れ商品は」「{soldOutItem}」「ですか」）は
/// TalkCondition 側がグループ単位で進行状態を持つことで、複数回の発話にまたがって対応する。
/// ループ前にスナップショットを取り、同一メッセージ内での前提タスク連鎖を防ぐ。
/// </summary>
public class TaskTalkRouter : TalkGoal
{
    [System.Serializable]
    public class FinishInquiry
    {
        [Tooltip("Inspector上での識別用（例: 売り切れ、注文）")]
        public string name;
        [Tooltip("このカテゴリの発話であることを示す語（例: 売り切れ/うりきれ、注文/オーダー）")]
        public string[] categoryKeywords;
        [Tooltip("終了確認を示す語（例: 以上ですか、全部ですか）")]
        public string[] finishKeywords;
        [Tooltip("すべて完了していれば「はい」と判定するタスク名（FreeTaskManager登録済みのもの）")]
        public string[] taskNames;
        public string yesReply = "はい";
        public string noReply = "いいえ";
        public UnityEvent onYes;
        public UnityEvent onNo;
    }

    [Header("「以上ですか」判定 (TalkConditionを介さず直接処理する)")]
    public FinishInquiry[] finishInquiries =
    {
        new FinishInquiry
        {
            name = "売り切れ",
            categoryKeywords = new[] { "売り切れ", "うりきれ" },
            finishKeywords = new[] { "以上ですか", "全部ですか" },
            taskNames = new[] { "売り切れ1_1", "売り切れ2_1", "売り切れ3_1" },
        },
        new FinishInquiry
        {
            name = "注文",
            categoryKeywords = new[] { "注文", "ちゅうもん", "オーダー", "おーだー" },
            finishKeywords = new[] { "以上ですか", "全部ですか" },
            taskNames = new[] { "注文1_1", "注文2_1", "注文3_1" },
        },
    };

    public SpeechBubble npcBubble;
    public float replyDelay = 4f;

    TalkCondition[] conditions;

    void Awake()
    {
        conditions = FindObjectsOfType<TalkCondition>();
    }

    public override string HandleUserText(string text)
    {
        foreach (var inquiry in finishInquiries)
        {
            if (ContainsAny(text, inquiry.categoryKeywords) && ContainsAny(text, inquiry.finishKeywords))
                return HandleFinishInquiry(inquiry);
        }

        // 処理前に実行可能なタスクを確定（連鎖防止）
        var eligible = new HashSet<TalkCondition>();
        foreach (var c in conditions)
            if (c.CanAttemptNow()) eligible.Add(c);

        string lastReply = null;
        foreach (var c in conditions)
        {
            if (!eligible.Contains(c)) continue;
            string reply = c.TryComplete(text);
            if (reply != null) lastReply = reply;
        }
        return lastReply ?? "none";
    }

    static bool ContainsAny(string text, string[] keywords)
    {
        if (keywords == null) return false;
        foreach (var kw in keywords)
            if (!string.IsNullOrEmpty(kw) && text.Contains(kw))
                return true;
        return false;
    }

    string HandleFinishInquiry(FinishInquiry inquiry)
    {
        bool allDone = System.Array.TrueForAll(inquiry.taskNames, FreeTaskManager.Instance.IsCompleted);
        string reply = allDone ? inquiry.yesReply : inquiry.noReply;

        if (allDone) inquiry.onYes.Invoke();
        else inquiry.onNo.Invoke();

        if (npcBubble != null)
            StartCoroutine(SayDelayed(reply));

        return reply;
    }

    IEnumerator SayDelayed(string reply)
    {
        yield return new WaitForSeconds(replyDelay);
        npcBubble.Say(reply);
        TTSManager.Instance?.Speak(reply);
    }
}
