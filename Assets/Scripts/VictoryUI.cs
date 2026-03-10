using UnityEngine;
using TMPro;
using DG.Tweening;

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

        // ✨ 4. DOTween 果凍彈跳動畫！
        // 先把面板瞬間縮小到看不見
        transform.localScale = Vector3.zero;
        // 花 0.5 秒彈性放大到原始大小 (Vector3.one)，並無視時間暫停 (SetUpdate(true))
        transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
        int seconds = Mathf.FloorToInt(timeInSeconds - minutes * 60);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private string DetermineRank(int detects, int violence)
    {
        // 💀 使用 <sprite name="你切割時取的名字"> 來呼叫圖片
        // 或是用 <sprite index=0> 呼叫圖集裡的第一張圖

        if (detects == 0 && violence == 0)
            return "<color=#00FF00><sprite name=\"emoji_ghost\"> 完美幽靈</color>";

        if (detects == 0 && violence > 0)
            return "<color=#FFA500><sprite name=\"emoji_ninja\"> 沉默殺手</color>";

        if (detects > 3 && violence > 3)
            return "<color=#FF0000><sprite name=\"emoji_boom\"> 狂暴鐵槌</color>";

        if (detects > 0 && violence == 0)
            return "<color=#FFFF00><sprite name=\"emoji_box\"> 驚慌的紙箱</color>";

        return "<sprite index=0> 潛入特工"; // 預設隨便給個圖
    }
}