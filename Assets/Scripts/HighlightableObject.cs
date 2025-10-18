using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Renderer))] // 確保物件有 Renderer
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

    private bool isTargeted = false; // 是否被玩家準星瞄準
    private bool isAvailable = false; // 是否可加入隊伍但未加入

    void Awake()
    {
        // 嘗試自動獲取 Renderer，優先找子物件
        objectRenderer = GetComponentInChildren<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError($"HighlightableObject on {gameObject.name} cannot find a Renderer!", this);
            enabled = false; // 找不到就禁用
            return;
        }
        originalMaterials = objectRenderer.materials;
    }

    // 由 SpectatorController 或 PlayerMovement 呼叫
    public void SetTargetedHighlight(bool active)
    {
        if (isTargeted != active)
        {
            isTargeted = active;
            UpdateHighlightMaterials();
        }
    }

    // 由 HighlightManager 呼叫
    public void SetAvailableHighlight(bool active)
    {
        if (!this.enabled) return; // 如果一開始就沒 Renderer，直接返回
        if (isAvailable != active)
        {
            isAvailable = active;
            UpdateHighlightMaterials();
        }
    }

    // 核心：根據狀態決定最終顯示哪個高亮 (黃色優先)
    private void UpdateHighlightMaterials()
    {
        if (objectRenderer == null) return;

        List<Material> currentMaterials = originalMaterials.ToList();
        DestroyCurrentInstances(); // 先清除舊的實例

        if (isTargeted && targetedHighlightTemplate != null)
        {
            targetedInstance = new Material(targetedHighlightTemplate);
            currentMaterials.Add(targetedInstance);
            // Debug.Log($"{gameObject.name} Applying Targeted Highlight");
        }
        else if (isAvailable && availableHighlightTemplate != null)
        {
            availableInstance = new Material(availableHighlightTemplate);
            currentMaterials.Add(availableInstance);
            // Debug.Log($"{gameObject.name} Applying Available Highlight");
        }
        // else { Debug.Log($"{gameObject.name} Removing Highlight"); }

        objectRenderer.materials = currentMaterials.ToArray();
    }

    // 清理動態創建的材質實例
    private void DestroyCurrentInstances()
    {
        if (targetedInstance != null)
        {
            Destroy(targetedInstance);
            targetedInstance = null;
        }
        if (availableInstance != null)
        {
            Destroy(availableInstance);
            availableInstance = null;
        }
    }

    void OnDestroy()
    {
        // 確保物件銷毀時也清理材質
        DestroyCurrentInstances();
    }

    // 外部腳本可以查詢物件是否已被招募
    public bool IsInTeam(TeamManager teamManager)
    {
        if (teamManager == null) return false;
        return teamManager.IsInTeam(this.gameObject.transform.root.gameObject); // 檢查根物件
    }

}