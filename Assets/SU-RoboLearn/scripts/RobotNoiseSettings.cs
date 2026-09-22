using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobotNoiseSettings : MonoBehaviour
{
    [Header("Target")]
    public WebSocketServerUnity server;

    [Header("Sliders (Min:50 / Max:150 に設定)")]
    public Slider moveNoiseSlider;
    public Slider turnNoiseSlider;

    [Header("Value Labels (任意、現在値の表示用)")]
    public TextMeshProUGUI moveNoiseLabel;
    public TextMeshProUGUI turnNoiseLabel;

    void OnEnable()
    {
        if (server == null)
            server = FindObjectOfType<WebSocketServerUnity>();

        if (server == null)
        {
            Debug.LogError("[RobotNoiseSettings] WebSocketServerUnity が見つかりません。Server フィールドに手動でアサインしてください。");
            return;
        }

        if (moveNoiseSlider == null || turnNoiseSlider == null)
        {
            Debug.LogError("[RobotNoiseSettings] スライダーが未アサインです。インスペクターで設定してください。");
            return;
        }

        moveNoiseSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MoveNoisePercent", 100f));
        turnNoiseSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("TurnNoisePercent", 100f));

        moveNoiseSlider.onValueChanged.RemoveListener(OnMoveNoiseChanged);
        moveNoiseSlider.onValueChanged.AddListener(OnMoveNoiseChanged);

        turnNoiseSlider.onValueChanged.RemoveListener(OnTurnNoiseChanged);
        turnNoiseSlider.onValueChanged.AddListener(OnTurnNoiseChanged);

        ApplyAll();
    }

    void OnDisable()
    {
        if (moveNoiseSlider != null) moveNoiseSlider.onValueChanged.RemoveListener(OnMoveNoiseChanged);
        if (turnNoiseSlider != null) turnNoiseSlider.onValueChanged.RemoveListener(OnTurnNoiseChanged);
    }

    void ApplyAll()
    {
        if (server == null) return;
        server.moveNoisePercent = moveNoiseSlider.value;
        server.turnNoisePercent = turnNoiseSlider.value;
        UpdateLabel(moveNoiseLabel, moveNoiseSlider.value);
        UpdateLabel(turnNoiseLabel, turnNoiseSlider.value);
    }

    public void OnMoveNoiseChanged(float value)
    {
        PlayerPrefs.SetFloat("MoveNoisePercent", value);
        PlayerPrefs.Save();
        if (server != null) server.moveNoisePercent = value;
        UpdateLabel(moveNoiseLabel, value);
    }

    public void OnTurnNoiseChanged(float value)
    {
        PlayerPrefs.SetFloat("TurnNoisePercent", value);
        PlayerPrefs.Save();
        if (server != null) server.turnNoisePercent = value;
        UpdateLabel(turnNoiseLabel, value);
    }

    static void UpdateLabel(TextMeshProUGUI label, float value)
    {
        if (label != null) label.text = $"{value:F0}%";
    }
}
