using UnityEngine;
using System.IO;
using System.Linq;

public class SaveSystem : MonoBehaviour
{
    private string savePath;
    private TeamManager teamManager;

    void Awake()
    {
        savePath = Application.persistentDataPath + "/savefile.json";
        teamManager = FindFirstObjectByType<TeamManager>();
    }

    [ContextMenu("Save Game")] // 可以在 Inspector 點右鍵測試
    public void SaveGame()
    {
        SaveData data = new SaveData();
        data.activeUnitIndex = teamManager.activeCharacterIndex;
        data.saveTime = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

        foreach (var unit in teamManager.team)
        {
            if (unit.character == null) continue;

            data.teamUnits.Add(new UnitData
            {
                unitName = unit.character.name,
                position = unit.character.transform.position,
                rotation = unit.character.transform.rotation,
                isAvailable = unit.isAvailable
            });
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);

        Debug.Log($"[SaveSystem] 存檔成功！路徑: {savePath}");
    }

    [ContextMenu("Load Game")]
    public void LoadGame()
    {
        if (teamManager == null)
        {
            teamManager = FindFirstObjectByType<TeamManager>();
            if (teamManager == null)
            {
                Debug.LogError("[SaveSystem] 找不到 TeamManager，Load 終止！");
                return;
            }
        }

        if (!File.Exists(savePath))
        {
            Debug.LogWarning("存檔檔案不存在！");
            return;
        }

        string json = File.ReadAllText(savePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        // 💀 還原邏輯
        for (int i = 0; i < data.teamUnits.Count; i++)
        {
            // 修正上一題的運算子錯誤：陣列用 .Length
            if (i >= teamManager.team.Length) break;

            var unit = teamManager.team[i];

            if (unit.character == null)
            {
                Debug.LogWarning($"[SaveSystem] 隊伍索引 {i} 的物件不存在，跳過此項。");
                continue;
            }

            // 執行還原位置
            unit.character.transform.position = data.teamUnits[i].position;
            unit.character.transform.rotation = data.teamUnits[i].rotation;
            unit.isAvailable = data.teamUnits[i].isAvailable;
        }

        // 切換回存檔時的角色
        teamManager.SwitchToCharacterByIndex(data.activeUnitIndex);

        Debug.Log($"[SaveSystem] 存檔載入完成！存檔時間: {data.saveTime}");
    }
}