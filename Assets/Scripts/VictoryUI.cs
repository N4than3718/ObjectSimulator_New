using UnityEngine;
using TMPro;

public class VictoryUI : MonoBehaviour
{
    [Header("文字綁定")]
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI rankText;

    [Header("關卡設定")]
    [Tooltip("用來告訴 DataManager 這是第幾關的時間")]
    [SerializeField] private int levelIndex = 1;

    // 💀 GameDirector 會呼叫這個方法來更新文字
    public void UpdateVictoryData()
    {
        if (LevelTimer.Instance == null) return;

        // 1. 停止計時
        LevelTimer.Instance.StopTimer();

        float finalTime = LevelTimer.Instance.CurrentTime;
        int detects = LevelTimer.Instance.timesDetected;
        int violence = LevelTimer.Instance.timesViolent;

        // 2. 儲存最佳時間
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveBestTime(levelIndex, finalTime);
        }

        // 3. 更新 UI
        if (timeText != null) timeText.text = $"通關時間: {FormatTime(finalTime)}";
        if (statsText != null) statsText.text = $"被發現: {detects} 次\n施暴: {violence} 次";
        if (rankText != null) rankText.text = $"獲得稱號: {DetermineRank(detects, violence)}";
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
        int seconds = Mathf.FloorToInt(timeInSeconds - minutes * 60);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private string DetermineRank(int detects, int violence)
    {
        if (detects == 0 && violence == 0) return "<color=#00FF00>👻 完美幽靈</color>";
        if (detects == 0 && violence > 0) return "<color=#FFA500>🥷 沉默殺手</color>";
        if (detects > 3 && violence > 3) return "<color=#FF0000>💥 狂暴鐵槌</color>";
        if (detects > 0 && violence == 0) return "<color=#FFFF00>📦 驚慌的紙箱</color>";
        return "🕵️ 潛入特工";
    }
}