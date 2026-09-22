using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// キーワードグループ全てにマッチしたときにタスクを達成する条件コンポーネント。
/// 各グループ内はOR判定（どれか1つ含まれればOK）、グループ間はAND判定（全グループ必須）。
/// キーワードにはプレースホルダー {wantItem} {soldOutItem} {soldOutItem2} {soldOutItem3} {dummyItem} {kitchenSide} {customerTable} を使用可能。
/// </summary>
public class TalkCondition : MonoBehaviour
{
    [System.Serializable]
    public class KeywordGroup
    {
        [Tooltip("このグループ内のいずれか1つが入力に含まれればOK（OR）")]
        public string[] keywords;
    }

    [Header("Task")]
    public string taskName;

    [Header("Condition  ※グループ間はAND・グループ内はOR  ※{wantItem}{wantItem2}{wantItem3}{soldOutItem}{soldOutItem2}{soldOutItem3}{dummyItem}{kitchenSide}{customerTable}")]
    public KeywordGroup[] keywordGroups;

    [Header("Progress (複数回の発話にまたがってグループを満たす場合)")]
    [Tooltip("この秒数、新しいグループ達成がなければ進行状態をリセットする")]
    public float progressResetSeconds = 30f;

    [Header("Reply (空欄なら返答なし)  ※プレースホルダー使用可")]
    public string replyText;
    public SpeechBubble npcBubble;
    public float npcBubbleDelay = 4f;

    [Header("Difficulty (アルファ/ベータでスコアを変える場合)")]
    public bool hasDifficulty = false;
    public SettingKey difficultyKey;

    [Header("Prerequisites (前提タスク名、任意)")]
    public string[] prerequisites;

    [Header("On Completed")]
    public UnityEvent onCompleted;

    bool[] groupAchieved;
    float lastProgressTime = -999f;

    void Awake()
    {
        groupAchieved = new bool[keywordGroups != null ? keywordGroups.Length : 0];
    }

    public bool CanAttemptNow()
        => !FreeTaskManager.Instance.IsCompleted(taskName)
        && FreeTaskManager.Instance.CanAttempt(prerequisites);

    public string TryComplete(string text)
    {
        if (FreeTaskManager.Instance.IsCompleted(taskName)) return null;
        if (!UpdateProgress(text)) return null;

        bool isBeta = hasDifficulty
            && CompetitionSettings.Instance != null
            && CompetitionSettings.Instance.Get(difficultyKey);

        FreeTaskManager.Instance.CompleteTask(taskName, isBeta);
        onCompleted.Invoke();

        string reply = Resolve(replyText);
        if (!string.IsNullOrEmpty(reply) && npcBubble != null)
            StartCoroutine(SayDelayed(reply));
        return string.IsNullOrEmpty(reply) ? null : reply;
    }

    // 未達成のグループのうち、この発話でマッチしたものを達成済みにする。
    // Scratch側はブロックごとに発話を分けて送ってくるため、複数回の発話にまたがって
    // 少しずつグループを満たしていく想定（一定時間進捗がなければリセットする）。
    // 全グループが達成済みになったら true を返す。
    bool UpdateProgress(string text)
    {
        if (keywordGroups == null || keywordGroups.Length == 0) return false;

        if (Time.time - lastProgressTime > progressResetSeconds)
            System.Array.Clear(groupAchieved, 0, groupAchieved.Length);

        text = NormalizeDirection(text);

        bool anyNewMatch = false;
        for (int i = 0; i < keywordGroups.Length; i++)
        {
            if (groupAchieved[i]) continue;

            var group = keywordGroups[i];
            if (group.keywords == null || group.keywords.Length == 0) continue;

            foreach (var kw in group.keywords)
            {
                if (!string.IsNullOrEmpty(kw) && text.Contains(Resolve(kw)))
                {
                    groupAchieved[i] = true;
                    anyNewMatch = true;
                    break;
                }
            }
        }

        if (anyNewMatch) lastProgressTime = Time.time;

        foreach (bool achieved in groupAchieved)
            if (!achieved) return false;

        return true;
    }

    static string NormalizeDirection(string text)
        => text.Replace("みぎ", "右").Replace("ひだり", "左");

    System.Collections.IEnumerator SayDelayed(string reply)
    {
        yield return new WaitForSeconds(npcBubbleDelay);
        npcBubble.Say(reply);
        TTSManager.Instance?.Speak(reply);
    }

    static string Resolve(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var r = InitialRandomSettings.Instance;
        if (r == null) return template;

        return template
            .Replace("{wantItem}",      r.wantItem)
            .Replace("{wantItem2}",     r.wantItem2)
            .Replace("{wantItem3}",     r.wantItem3)
            .Replace("{soldOutItem}",   r.soldOutItem)
            .Replace("{soldOutItem2}",  r.soldOutItem2)
            .Replace("{soldOutItem3}",  r.soldOutItem3)
            .Replace("{dummyItem}",     r.dummyItem)
            .Replace("{kitchenSide}",   r.kitchenSide)
            .Replace("{customerTable}", r.customerTableNo);
    }
}
