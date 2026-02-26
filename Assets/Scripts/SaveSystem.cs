using UnityEngine;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks; // 引入 Task 處理非同步

public class SaveSystem : MonoBehaviour
{
    private string savePath;
    private TeamManager teamManager;

    // 讓 UI 系統或音效系統可以訂閱存檔/讀檔事件 ✨
    public static event Action OnSaveStarted;
    public static event Action OnSaveCompleted;
    public static event Action OnLoadCompleted;

    void Awake()
    {
        // 💀 使用 Path.Combine 避免跨平台路徑斜線問題
        savePath = Path.Combine(Application.persistentDataPath, "savefile.json");
        teamManager = FindFirstObjectByType<TeamManager>();
    }

    [ContextMenu("Save Game")]
    public async void SaveGame() // 💀 改為 async
    {
        OnSaveStarted?.Invoke(); // 通知 UI 顯示 "存檔中..." ✨

        SaveData data = new SaveData();
        data.activeUnitIndex = teamManager.activeCharacterIndex;
        // 💀 儲存時間改用 Ticks 或 ISO 8601 標準格式，方便未來做存檔排序
        data.saveTime = DateTime.UtcNow.ToString("o");

        foreach (var unit in teamManager.team)
        {
            if (unit.character == null) continue;

            // 💀 檢查物件身上有沒有實作 ISaveable 的腳本 (例如 CardboardSkill)
            ISaveable saveable = unit.character.GetComponent<ISaveable>();
            string customJson = saveable != null ? saveable.GetSaveData() : "";

            data.teamUnits.Add(new UnitData
            {
                unitName = unit.character.name,
                position = unit.character.transform.position,
                rotation = unit.character.transform.rotation,
                isAvailable = unit.isAvailable,
                customStateJson = customJson // 存入專屬資料
            });
        }

        string json = JsonUtility.ToJson(data, true);

        try
        {
            // 💀 使用非同步寫入，避免存檔時畫面卡頓 (Micro-stutter)
            await File.WriteAllTextAsync(savePath, json);
            Debug.Log($"[SaveSystem] 存檔成功！路徑: {savePath}");
            OnSaveCompleted?.Invoke(); // 通知 UI 顯示 "存檔完成" ✨
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 存檔失敗: {e.Message}");
        }
    }

    [ContextMenu("Load Game")]
    public void LoadGame()
    {
        if (teamManager == null) teamManager = FindFirstObjectByType<TeamManager>();

        if (!File.Exists(savePath))
        {
            Debug.LogWarning("存檔檔案不存在！");
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            for (int i = 0; i < data.teamUnits.Count; i++)
            {
                if (i >= teamManager.team.Length) break;
                var unit = teamManager.team[i];
                if (unit.character == null) continue;

                // 💀 關鍵防護：如果物件有 Rigidbody 或 NavMeshAgent，瞬移前必須先關閉，瞬移後再開
                // 否則物理引擎會因為瞬間位移計算出極大的力道導致物件噴飛
                Rigidbody rb = unit.character.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                unit.character.transform.position = data.teamUnits[i].position;
                unit.character.transform.rotation = data.teamUnits[i].rotation;
                unit.isAvailable = data.teamUnits[i].isAvailable;

                // 💀 還原專屬狀態
                ISaveable saveable = unit.character.GetComponent<ISaveable>();
                if (saveable != null && !string.IsNullOrEmpty(data.teamUnits[i].customStateJson))
                {
                    saveable.RestoreSaveData(data.teamUnits[i].customStateJson);
                }

                if (rb != null) rb.isKinematic = false;
            }

            teamManager.SwitchToCharacterByIndex(data.activeUnitIndex);
            Debug.Log($"[SaveSystem] 存檔載入完成！");

            OnLoadCompleted?.Invoke(); // 通知視覺/音效系統 ✨
        }
        catch (Exception e)
        {
            // 💀 捕捉 JSON 損毀或格式不符的錯誤
            Debug.LogError($"[SaveSystem] 讀檔失敗，檔案可能損毀: {e.Message}");
        }
    }
}