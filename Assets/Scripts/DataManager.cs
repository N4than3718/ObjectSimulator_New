using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    // 定義常量鍵名，避免拼錯字
    private const string KEY_REACHED_LEVEL = "ReachedLevel";
    private const string KEY_BEST_TIME_PREFIX = "BestTime_Level_";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 讓資料管理員跨場景存在
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- 讀取功能 ---

    public int GetReachedLevel()
    {
        return PlayerPrefs.GetInt(KEY_REACHED_LEVEL, 1); // 預設第 1 關
    }

    public float GetBestTime(int levelIndex)
    {
        return PlayerPrefs.GetFloat(KEY_BEST_TIME_PREFIX + levelIndex, float.MaxValue);
    }

    // --- 寫入功能 ---

    public void SaveLevelProgress(int levelIndex)
    {
        int current = GetReachedLevel();
        if (levelIndex > current)
        {
            PlayerPrefs.SetInt(KEY_REACHED_LEVEL, levelIndex);
            PlayerPrefs.Save();
            Debug.Log($"[DataManager] 進度已儲存: 解鎖關卡 {levelIndex}");
        }
    }

    public void SaveBestTime(int levelIndex, float time)
    {
        float currentTime = GetBestTime(levelIndex);
        if (time < currentTime)
        {
            PlayerPrefs.SetFloat(KEY_BEST_TIME_PREFIX + levelIndex, time);
            PlayerPrefs.Save();
            Debug.Log($"[DataManager] 新紀錄! 關卡 {levelIndex} 時間: {time:F2}s");
        }
    }

    // --- 任務事件記錄功能 (跨關卡狀態) ---

    // 💀 寫入事件 (例如：SetEvent("PowerBroken", true))
    public void SetEvent(string eventName, bool isCompleted)
    {
        // PlayerPrefs 不支援直接存 bool，所以我們用 Int 代替 (1=真, 0=假)
        PlayerPrefs.SetInt("Event_" + eventName, isCompleted ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[DataManager] 任務事件更新: {eventName} = {isCompleted}");
    }

    // 💀 讀取事件 (第三關載入時用來檢查)
    public bool GetEvent(string eventName)
    {
        return PlayerPrefs.GetInt("Event_" + eventName, 0) == 1;
    }

    // 💀 (可選) 清除所有事件，通常在開新遊戲時呼叫
    public void ResetAllEvents()
    {
        PlayerPrefs.DeleteKey("Event_KeyDropped");
        PlayerPrefs.DeleteKey("Event_PowerBroken");
        // ... 有幾個事件就刪幾個
    }

    // 💀 Coder: 開發者工具，按一個鍵重置所有存檔
    [ContextMenu("Clear All Data")]
    public void ClearAllData()
    {
        PlayerPrefs.DeleteAll();
        Debug.LogWarning("[DataManager] 所有存檔已刪除！");
    }
}