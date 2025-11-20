using UnityEngine;

public class NoiseIndicatorManager : MonoBehaviour
{
    [Header("UI 設定")]
    [SerializeField] private NoiseIndicatorUI indicatorPrefab; // 拖曳 Prefab
    [SerializeField] private Transform indicatorContainer; // 拖曳 Canvas (或 Canvas 下的一個 Panel)

    private TeamManager teamManager;

    private void Awake()
    {
        teamManager = FindAnyObjectByType<TeamManager>();
    }

    private void OnEnable()
    {
        NpcAI.OnNoiseHeard += HandleNoiseHeard; // 訂閱事件
    }

    private void OnDisable()
    {
        NpcAI.OnNoiseHeard -= HandleNoiseHeard; // 取消訂閱
    }

    private void HandleNoiseHeard(NpcAI npc, float intensity)
    {
        // 1. 取得當前攝影機
        Transform camTransform = teamManager.CurrentCameraTransform;
        if (camTransform == null) return;

        // 2. 檢查 NPC 是否在螢幕範圍外？(可選，通常這類 UI 即使在螢幕內也會顯示方向)

        // 3. 生成指示器
        NoiseIndicatorUI newIndicator = Instantiate(indicatorPrefab, indicatorContainer);

        // 4. 初始化
        newIndicator.Initialize(npc, camTransform);
    }
}