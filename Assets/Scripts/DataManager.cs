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

    // 💀 Coder: 開發者工具，按一個鍵重置所有存檔
    [ContextMenu("Clear All Data")]
    public void ClearAllData()
    {
        PlayerPrefs.DeleteAll();
        Debug.LogWarning("[DataManager] 所有存檔已刪除！");
    }
}