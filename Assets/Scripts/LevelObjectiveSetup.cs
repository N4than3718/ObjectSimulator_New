using UnityEngine;
using System.Collections.Generic; // 💀 記得加這行

public class LevelObjectiveSetup : MonoBehaviour
{
    [Header("本關專屬任務清單")]
    // 💀 把剛剛在 MissionManager 定義的結構變成一個可以在 Inspector 編輯的陣列
    [SerializeField] private List<Mission> levelMissions = new List<Mission>();

    private void Start()
    {
        if (MissionManager.Instance != null)
        {
            // 把整包任務清單丟給大腦
            MissionManager.Instance.InitializeMissions(levelMissions);
        }
    }
}