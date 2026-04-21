using UnityEngine;
using TMPro;
using System.Collections.Generic; // 💀 必須引入這個才能使用 List

// 💀 定義單一任務的資料結構
[System.Serializable]
public class Mission
{
    public string missionID; // 任務ID (必須跟目標物品的 Tag 一模一樣！)
    public string description; // 任務描述
    public int targetAmount; // 目標數量
    [HideInInspector] public int currentAmount = 0;
    [HideInInspector] public bool isComplete = false;
}

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Header("UI 綁定")]
    [SerializeField] private TextMeshProUGUI missionText;

    // 💀 存放這關所有任務的清單
    private List<Mission> activeMissions = new List<Mission>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 💀 接收來自關卡派發員的多個任務，並檢查過往進度
    public void InitializeMissions(List<Mission> levelMissions)
    {
        activeMissions = levelMissions;

        foreach (var mission in activeMissions)
        {
            // 預設狀態重置
            mission.currentAmount = 0;
            mission.isComplete = false;

            // 💀 核心連動：去問 DataManager 這個任務的 ID (事件名稱) 之前有沒有做過？
            // 例如 missionID 是 "PowerBroken"，它就會去查 Event_PowerBroken
            if (DataManager.Instance != null && DataManager.Instance.GetEvent(mission.missionID))
            {
                // 如果存檔說已經做過了，直接把進度灌滿，並標記完成！
                mission.currentAmount = mission.targetAmount;
                mission.isComplete = true;
                Debug.Log($"[MissionManager] 偵測到跨關卡進度，自動完成任務：{mission.description}");
            }
        }

        UpdateUI();

        // 💀 防呆機制：如果一進關卡，發現所有任務早就都在前幾關做完了，直接過關！
        CheckAllMissionsCompleted();
    }

    // 💀 把這段原本寫在 AddProgress 裡的檢查邏輯，抽成一個獨立的函數
    private void CheckAllMissionsCompleted()
    {
        if (activeMissions == null || activeMissions.Count == 0) return;

        bool allMissionsCompleted = true;

        foreach (var mission in activeMissions)
        {
            if (!mission.isComplete)
            {
                allMissionsCompleted = false;
                break; // 只要有一個沒完成，就不用繼續檢查了
            }
        }

        if (allMissionsCompleted)
        {
            Debug.Log("🎉 本關卡所有任務達成！");
            // 💀 呼叫 GameDirector 觸發過關
            if (GameDirector.Instance != null) GameDirector.Instance.TriggerVictory();
        }
    }

    // 💀 外部觸發區現在必須傳入 missionID (Tag) 才能知道要更新哪一個任務！
    public void AddProgress(string missionID, int amount)
    {

        foreach (var mission in activeMissions)
        {
            // 找到對應的任務
            if (mission.missionID == missionID && !mission.isComplete)
            {
                mission.currentAmount += amount;

                if (mission.currentAmount >= mission.targetAmount)
                {
                    mission.currentAmount = mission.targetAmount;
                    mission.isComplete = true;
                    Debug.Log($"任務達成：{mission.description}");

                    if (DataManager.Instance != null)
                    {
                        DataManager.Instance.SetEvent(mission.missionID, true);
                    }
                }
            }
        }

        UpdateUI();

        CheckAllMissionsCompleted();
    }

    private void UpdateUI()
    {
        if (missionText == null) return;

        string finalText = ""; // 準備一塊空白黑板

        // 把所有任務一行一行寫上去
        foreach (var mission in activeMissions)
        {
            if (mission.isComplete)
            {
                finalText += $"<color=#00FF00>{mission.description} (完成!)</color>\n";
            }
            else
            {
                finalText += $"{mission.description} ({mission.currentAmount}/{mission.targetAmount})\n";
            }
        }

        missionText.text = finalText; // 一次把多行文字印到螢幕上！
    }
}