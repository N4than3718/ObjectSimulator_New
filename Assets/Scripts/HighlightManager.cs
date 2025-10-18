using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("白色高亮材質模板 (Available)")]
    [SerializeField] private Material availableHighlightTemplate;
    // ▼▼▼ 新增：綠色高亮模板 ▼▼▼
    [Tooltip("綠色高亮材質模板 (Inactive Team Member)")]
    [SerializeField] private Material inactiveTeamHighlightTemplate;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.2f;

    [Header("Dynamic Outline")] // 這些參數現在同時用於白色和綠色
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.03f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();

        // ▼▼▼ 檢查所有必要引用 ▼▼▼
        if (teamManager == null || availableHighlightTemplate == null || inactiveTeamHighlightTemplate == null)
        {
            Debug.LogError("HighlightManager is missing references (TeamManager, Available Template, or Inactive Team Template)!");
            enabled = false;
            return;
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));
        foreach (var obj in allHighlightables)
        {
            if (obj != null && obj.enabled)
            {
                // 把兩個模板都傳給 HighlightableObject
                obj.availableHighlightTemplate = this.availableHighlightTemplate;
                obj.inactiveTeamHighlightTemplate = this.inactiveTeamHighlightTemplate;
            }
        }
        StartCoroutine(UpdateAvailableHighlights());
    }

    IEnumerator UpdateAvailableHighlights()
    {
        while (true)
        {
            Transform currentCameraTransform = teamManager.CurrentCameraTransform;
            GameObject activeCharacter = teamManager.ActiveCharacterGameObject; // 獲取當前操控的角色

            if (currentCameraTransform == null)
            {
                yield return new WaitForSeconds(updateInterval);
                continue;
            }

            foreach (HighlightableObject highlightable in allHighlightables)
            {
                if (highlightable != null && highlightable.enabled)
                {
                    GameObject currentObject = highlightable.gameObject;
                    bool isInTeam = highlightable.IsInTeam(teamManager);
                    bool isActive = (currentObject == activeCharacter);

                    // ▼▼▼ 核心修改：根據狀態設置高亮 ▼▼▼
                    if (isInTeam && !isActive)
                    {
                        // 在隊伍中但未被操控 -> 綠色
                        highlightable.SetInactiveTeamHighlight(true);
                        highlightable.SetAvailableHighlight(false); // 確保白色關閉
                    }
                    else if (!isInTeam)
                    {
                        // 不在隊伍中 -> 白色
                        highlightable.SetAvailableHighlight(true);
                        highlightable.SetInactiveTeamHighlight(false); // 確保綠色關閉
                    }
                    else // (isInTeam && isActive)
                    {
                        // 是當前操控的角色 -> 關閉白色和綠色 (黃色由 PlayerMovement/Spectator 控制)
                        highlightable.SetAvailableHighlight(false);
                        highlightable.SetInactiveTeamHighlight(false);
                    }

                    // 如果顯示的是白色或綠色，就更新輪廓寬度
                    if ((!isInTeam || (isInTeam && !isActive)) && !highlightable.IsTargeted) // IsTargeted 是一個假設的屬性，我們需要加回去
                    {
                        float distance = Vector3.Distance(currentCameraTransform.position, highlightable.transform.position);
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightable.SetOutlineWidth(newWidth);
                    }
                    // 如果是 Targeted (黃色)，寬度由 PlayerMovement/Spectator 控制
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}