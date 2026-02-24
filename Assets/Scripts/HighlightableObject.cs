using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HighlightableObject : MonoBehaviour
{
    [Header("Highlight Materials")]
    public Material targetedHighlightTemplate;
    public Material availableHighlightTemplate;
    public Material inactiveTeamHighlightTemplate;

    public bool IsTargeted => isTargeted;
    public bool IsAvailable() => isAvailable;
    public bool IsInactiveTeamMember() => isInactiveTeamMember;

    // 💀 改用陣列儲存所有子物件的 Renderers
    private Renderer[] objectRenderers;
    // 💀 使用二維陣列記錄每一個 Renderer 原本的材質
    private Material[][] originalMaterialsArray;

    // 💀 材質實例快取 (Cache)，避免頻繁 new 和 Destroy 造成 GC
    private Material targetedInstance;
    private Material availableInstance;
    private Material inactiveInstance;

    private bool isTargeted = false;
    private bool isAvailable = false;
    private bool isInactiveTeamMember = false;
    private float currentOutlineWidth = -1f;

    void OnEnable()
    {
        if (HighlightManager.Instance != null)
            HighlightManager.Instance.RegisterHighlightable(this);
    }

    void OnDisable()
    {
        if (HighlightManager.Instance != null)
            HighlightManager.Instance.UnregisterHighlightable(this);
    }

    void Awake()
    {
        // 💀 關鍵修復：取得所有子物件的 Renderer (包含 Bell, Clock, Cap 等)
        objectRenderers = GetComponentsInChildren<Renderer>(true);
        if (objectRenderers.Length == 0)
        {
            Debug.LogError($"HighlightableObject on {gameObject.name} cannot find any Renderer!", this);
            enabled = false;
            return;
        }

        // 記錄每一個 Renderer 本身的原始材質
        originalMaterialsArray = new Material[objectRenderers.Length][];
        for (int i = 0; i < objectRenderers.Length; i++)
        {
            originalMaterialsArray[i] = objectRenderers[i].materials;
        }

        // 預先實例化材質，避免遊戲中卡頓
        if (targetedHighlightTemplate) targetedInstance = new Material(targetedHighlightTemplate);
        if (availableHighlightTemplate) availableInstance = new Material(availableHighlightTemplate);
        if (inactiveTeamHighlightTemplate) inactiveInstance = new Material(inactiveTeamHighlightTemplate);
    }

    public void SetTargetedHighlight(bool active)
    {
        if (!this.enabled || isTargeted == active) return;
        isTargeted = active;
        UpdateHighlightMaterials();
    }

    public void SetAvailableHighlight(bool active)
    {
        if (!this.enabled || isAvailable == active) return;
        isAvailable = active;
        if (active) isInactiveTeamMember = false;
        UpdateHighlightMaterials();
    }

    public void SetInactiveTeamHighlight(bool active)
    {
        if (!this.enabled || isInactiveTeamMember == active) return;
        isInactiveTeamMember = active;
        if (active) isAvailable = false;
        UpdateHighlightMaterials();
    }

    public void SetOutlineWidth(float width)
    {
        if (!this.enabled || Mathf.Approximately(currentOutlineWidth, width)) return;
        currentOutlineWidth = width;

        Material activeInstance = GetActiveHighlightMaterial();
        if (activeInstance != null)
        {
            activeInstance.SetFloat("_OutlineWidth", width);
        }
    }

    private Material GetActiveHighlightMaterial()
    {
        if (isTargeted) return targetedInstance;
        if (isInactiveTeamMember) return inactiveInstance;
        if (isAvailable) return availableInstance;
        return null;
    }

    private void UpdateHighlightMaterials()
    {
        if (objectRenderers == null || objectRenderers.Length == 0) return;

        Material activeInstance = GetActiveHighlightMaterial();

        // 💀 迴圈遍歷所有零件，全部套用材質
        for (int i = 0; i < objectRenderers.Length; i++)
        {
            if (objectRenderers[i] == null) continue;

            List<Material> currentMaterials = originalMaterialsArray[i].ToList();

            if (activeInstance != null)
            {
                currentMaterials.Add(activeInstance);
            }

            objectRenderers[i].materials = currentMaterials.ToArray();
        }

        // 重新應用寬度
        if (currentOutlineWidth > 0 && activeInstance != null)
        {
            activeInstance.SetFloat("_OutlineWidth", currentOutlineWidth);
        }
    }

    void OnDestroy()
    {
        // 只有在物件被銷毀時才清理材質，徹底解決 GC 問題
        if (targetedInstance != null) Destroy(targetedInstance);
        if (availableInstance != null) Destroy(availableInstance);
        if (inactiveInstance != null) Destroy(inactiveInstance);
    }

    public bool IsInTeam(TeamManager teamManager)
    {
        return teamManager != null && teamManager.IsInTeam(this.gameObject);
    }
}