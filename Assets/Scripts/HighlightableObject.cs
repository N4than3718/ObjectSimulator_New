using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ▼▼▼ 核心修改：移除 RequireComponent 屬性 ▼▼▼
// [RequireComponent(typeof(Renderer))]
public class HighlightableObject : MonoBehaviour
{
    [Header("Highlight Materials")]
    [Tooltip("被選中時的黃色高亮材質模板")]
    public Material targetedHighlightTemplate;
    [Tooltip("可加入隊伍時的白色高亮材質模板")]
    public Material availableHighlightTemplate;

    // --- 內部狀態 ---
    private Renderer objectRenderer;
    private Material[] originalMaterials;
    private Material targetedInstance;
    private Material availableInstance;

    private bool isTargeted = false;
    private bool isAvailable = false;

    void Awake()
    {
        // ▼▼▼ 核心修改：從 GetComponent 改為 GetComponentInChildren ▼▼▼
        objectRenderer = GetComponentInChildren<Renderer>(true); // true 表示包含非活動的子物件
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        if (objectRenderer == null)
        {
            Debug.LogError($"HighlightableObject on {gameObject.name} cannot find a Renderer in its children!", this);
            enabled = false; // 找不到 Renderer 就直接禁用腳本
            return;
        }
        originalMaterials = objectRenderer.materials;
    }

    // 由 SpectatorController 或 PlayerMovement 呼叫
    public void SetTargetedHighlight(bool active)
    {
        if (!this.enabled) return; // 如果一開始就沒 Renderer，直接返回
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
            UpdateHighlightMaterials();
        }
    }

    private void UpdateHighlightMaterials()
    {
        if (objectRenderer == null) return;

        List<Material> currentMaterials = originalMaterials.ToList();
        DestroyCurrentInstances();

        if (isTargeted && targetedHighlightTemplate != null)
        {
            targetedInstance = new Material(targetedHighlightTemplate);
            currentMaterials.Add(targetedInstance);
        }
        else if (isAvailable && availableHighlightTemplate != null)
        {
            availableInstance = new Material(availableHighlightTemplate);
            currentMaterials.Add(availableInstance);
        }

        objectRenderer.materials = currentMaterials.ToArray();
    }

    private void DestroyCurrentInstances()
    {
        if (targetedInstance != null) { Destroy(targetedInstance); targetedInstance = null; }
        if (availableInstance != null) { Destroy(availableInstance); availableInstance = null; }
    }

    void OnDestroy()
    {
        DestroyCurrentInstances();
    }

    public bool IsInTeam(TeamManager teamManager)
    {
        if (teamManager == null) return false;
        // 檢查這個腳本所在的 GameObject 是否在隊伍中
        // (因為我們現在把腳本掛在父物件上，所以不需要 .root 了)
        return teamManager.IsInTeam(this.gameObject);
    }
}