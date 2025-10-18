using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HighlightableObject : MonoBehaviour
{
    [Header("Highlight Materials")]
    [Tooltip("被選中時的黃色高亮材質模板")]
    public Material targetedHighlightTemplate;
    [Tooltip("可加入隊伍時的白色高亮材質模板")]
    public Material availableHighlightTemplate;
    // ▼▼▼ 新增：待命隊友的綠色高亮 ▼▼▼
    [Tooltip("待命隊友的綠色高亮材質模板")]
    public Material inactiveTeamHighlightTemplate;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // --- 內部狀態 ---
    public bool IsTargeted => isTargeted; // 讓外部可以查詢是否被瞄準
    public bool IsAvailable() => isAvailable;
    public bool IsInactiveTeamMember() => isInactiveTeamMember;
    private Renderer objectRenderer;
    private Material[] originalMaterials;
    private Material targetedInstance;
    private Material availableInstance;
    private Material inactiveInstance; // 新增綠色實例

    private bool isTargeted = false;
    private bool isAvailable = false;
    private bool isInactiveTeamMember = false; // 新增狀態

    private float currentOutlineWidth = -1f;

    void Awake()
    {
        objectRenderer = GetComponentInChildren<Renderer>(true);
        if (objectRenderer == null)
        {
            Debug.LogError($"HighlightableObject on {gameObject.name} cannot find a Renderer!", this);
            enabled = false;
            return;
        }
        originalMaterials = objectRenderer.materials;
    }

    // 由 SpectatorController 或 PlayerMovement 呼叫
    public void SetTargetedHighlight(bool active)
    {
        if (!this.enabled) return;
        if (isTargeted != active)
        {
            isTargeted = active;
            UpdateHighlightMaterials();
        }
    }

    // 由 HighlightManager 呼叫
    public void SetAvailableHighlight(bool active)
    {
        if (!this.enabled) return;
        if (isAvailable != active)
        {
            isAvailable = active;
            // 如果正在啟用 Available，確保 Inactive 是關閉的
            if (active) isInactiveTeamMember = false;
            UpdateHighlightMaterials();
        }
    }

    // ▼▼▼ 新增：由 HighlightManager 呼叫 ▼▼▼
    public void SetInactiveTeamHighlight(bool active)
    {
        if (!this.enabled) return;
        if (isInactiveTeamMember != active)
        {
            isInactiveTeamMember = active;
            // 如果正在啟用 Inactive，確保 Available 是關閉的
            if (active) isAvailable = false;
            UpdateHighlightMaterials();
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    public void SetOutlineWidth(float width)
    {
        if (!this.enabled || Mathf.Approximately(currentOutlineWidth, width)) return;
        currentOutlineWidth = width;
        // ▼▼▼ 優先級：黃 > 綠 > 白 ▼▼▼
        Material activeInstance = targetedInstance ?? inactiveInstance ?? availableInstance;
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        if (activeInstance != null)
        {
            activeInstance.SetFloat("_OutlineWidth", width);
        }
    }

    private void UpdateHighlightMaterials()
    {
        if (objectRenderer == null) return;

        List<Material> currentMaterials = originalMaterials.ToList();
        DestroyCurrentInstances();
        currentOutlineWidth = -1f;

        // ▼▼▼ 核心修改：加入綠色高亮的判斷邏輯，優先級 黃 > 綠 > 白 ▼▼▼
        if (isTargeted && targetedHighlightTemplate != null)
        {
            targetedInstance = new Material(targetedHighlightTemplate);
            currentMaterials.Add(targetedInstance);
        }
        else if (isInactiveTeamMember && inactiveTeamHighlightTemplate != null) // 黃色不亮才檢查綠色
        {
            inactiveInstance = new Material(inactiveTeamHighlightTemplate);
            currentMaterials.Add(inactiveInstance);
        }
        else if (isAvailable && availableHighlightTemplate != null) // 黃綠都不亮才檢查白色
        {
            availableInstance = new Material(availableHighlightTemplate);
            currentMaterials.Add(availableInstance);
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        objectRenderer.materials = currentMaterials.ToArray();

        // 重新應用寬度
        if (currentOutlineWidth > 0)
        {
            Material activeInstance = targetedInstance ?? inactiveInstance ?? availableInstance;
            if (activeInstance != null) activeInstance.SetFloat("_OutlineWidth", currentOutlineWidth);
        }
    }

    private void DestroyCurrentInstances()
    {
        if (targetedInstance != null) { Destroy(targetedInstance); targetedInstance = null; }
        if (availableInstance != null) { Destroy(availableInstance); availableInstance = null; }
        // ▼▼▼ 清理綠色實例 ▼▼▼
        if (inactiveInstance != null) { Destroy(inactiveInstance); inactiveInstance = null; }
        // ▲▲▲▲▲▲▲▲▲▲▲▲
    }

    void OnDestroy()
    {
        DestroyCurrentInstances();
    }

    public bool IsInTeam(TeamManager teamManager)
    {
        if (teamManager == null) return false;
        return teamManager.IsInTeam(this.gameObject);
    }
}