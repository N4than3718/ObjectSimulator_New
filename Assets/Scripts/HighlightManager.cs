using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("白色高亮材質模板")]
    [SerializeField] private Material availableHighlightTemplate;
    [Tooltip("觀察者攝影機的 Transform (如果未指定會自動查找)")]
    [SerializeField] private Transform spectatorCameraTransform;

    [Header("Settings")]
    [Tooltip("掃描場景更新高亮的頻率（秒）")]
    [SerializeField] private float updateInterval = 0.2f; // 可以稍微加快更新頻率

    // ▼▼▼ 新增：白色高亮的動態輪廓參數 ▼▼▼
    [Header("Dynamic Outline (Available)")]
    [Tooltip("輪廓的最小寬度")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [Tooltip("輪廓的最大寬度")]
    [SerializeField] private float maxOutlineWidth = 0.03f; // 白色可以稍微細一點
    [Tooltip("達到最大寬度所需的距離")]
    [SerializeField] private float maxDistanceForOutline = 50f;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        // --- 獲取引用 ---
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (spectatorCameraTransform == null)
        {
            SpectatorController sc = FindAnyObjectByType<SpectatorController>();
            if (sc != null) spectatorCameraTransform = sc.transform;
        }

        // --- 錯誤檢查 ---
        if (teamManager == null || availableHighlightTemplate == null || spectatorCameraTransform == null)
        {
            Debug.LogError("HighlightManager is missing required references (TeamManager, Available Template, or Spectator Camera Transform)!");
            enabled = false;
            return;
        }

        // --- 初始化列表和模板 ---
        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));
        foreach (var obj in allHighlightables)
        {
            if (obj != null && obj.enabled) // Check if obj is valid and enabled
            {
                obj.availableHighlightTemplate = this.availableHighlightTemplate;
            }
        }

        StartCoroutine(UpdateAvailableHighlights());
    }

    IEnumerator UpdateAvailableHighlights()
    {
        while (true)
        {
            // 如果玩家正在操控物件，就不需要更新白色高亮 (或可以選擇更新，取決於設計)
            // if (teamManager.CurrentState == TeamManager.GameState.Possessing) {
            //     yield return new WaitForSeconds(updateInterval);
            //     continue;
            // }

            foreach (HighlightableObject highlightable in allHighlightables)
            {
                if (highlightable != null && highlightable.enabled)
                {
                    bool shouldBeAvailable = !highlightable.IsInTeam(teamManager);
                    highlightable.SetAvailableHighlight(shouldBeAvailable);

                    // ▼▼▼ 核心修改：如果顯示白色高亮，就計算並設定寬度 ▼▼▼
                    if (shouldBeAvailable)
                    {
                        float distance = Vector3.Distance(spectatorCameraTransform.position, highlightable.transform.position);
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightable.SetOutlineWidth(newWidth); // 更新寬度
                    }
                    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}