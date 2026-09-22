using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance { get; private set; }

    private static readonly string LogFilePath =
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "session_log.csv");

    private int launchCount;
    private int resetCount;
    private float appStartRealtime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SessionLogger");
        go.AddComponent<SessionLogger>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        launchCount = PlayerPrefs.GetInt("SessionLogger_LaunchCount", 0) + 1;
        PlayerPrefs.SetInt("SessionLogger_LaunchCount", launchCount);
        PlayerPrefs.Save();

        resetCount = 0;
        appStartRealtime = Time.realtimeSinceStartup;

        EnsureHeader();
        WriteLog("Launch");
    }

    private void OnApplicationQuit()
    {
        WriteLog("Quit");
    }

    public void LogReset()
    {
        resetCount++;
        WriteLog("Reset");
    }

    private void WriteLog(string eventType)
    {
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.score : 0;
        float usageSeconds = Time.realtimeSinceStartup - appStartRealtime;

        string baseRow = string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            eventType,
            launchCount.ToString(),
            resetCount.ToString(),
            usageSeconds.ToString("F1"),
            score.ToString());

        string row = baseRow + "," + string.Join(",", GetSettingValues());

        File.AppendAllText(LogFilePath, row + Environment.NewLine);
    }

    private static string[] GetSettingValues()
    {
        return Enum.GetValues(typeof(SettingKey))
            .Cast<SettingKey>()
            .Select(k => (CompetitionSettings.Instance != null && CompetitionSettings.Instance.Get(k)).ToString())
            .ToArray();
    }

    private void EnsureHeader()
    {
        if (File.Exists(LogFilePath)) return;

        string settingColumns = string.Join(",", Enum.GetValues(typeof(SettingKey)).Cast<SettingKey>());
        string header = "Timestamp,Event,LaunchCount,ResetCount,UsageSeconds,Score," + settingColumns;
        File.WriteAllText(LogFilePath, header + Environment.NewLine);
    }
}
