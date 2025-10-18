using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// [RequireComponent(typeof(Renderer))] // Keep this removed
public class HighlightableObject : MonoBehaviour
{
    [Header("Highlight Materials")]
    public Material targetedHighlightTemplate;
    public Material availableHighlightTemplate;

    private Renderer objectRenderer;
    private Material[] originalMaterials;
    private Material targetedInstance;
    private Material availableInstance;

    private bool isTargeted = false;
    private bool isAvailable = false;

    // Store the last set width to avoid unnecessary updates
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

    public void SetTargetedHighlight(bool active)
    {
        if (!this.enabled) return;
        if (isTargeted != active)
        {
            isTargeted = active;
            UpdateHighlightMaterials();
        }
    }

    public void SetAvailableHighlight(bool active)
    {
        if (!this.enabled) return;
        if (isAvailable != active)
        {
            isAvailable = active;
            UpdateHighlightMaterials();
        }
    }

    // ¡¿¡¿¡¿ NEW METHOD ¡¿¡¿¡¿
    // Method called by controllers to set the outline width dynamically
    public void SetOutlineWidth(float width)
    {
        // Only update if the width has actually changed
        if (!this.enabled || Mathf.Approximately(currentOutlineWidth, width)) return;

        currentOutlineWidth = width;
        Material activeInstance = targetedInstance ?? availableInstance; // Get the currently active instance

        if (activeInstance != null)
        {
            activeInstance.SetFloat("_OutlineWidth", width);
        }
    }
    // ¡¶¡¶¡¶¡¶¡¶¡¶¡¶¡¶¡¶¡¶¡¶¡¶

    private void UpdateHighlightMaterials()
    {
        if (objectRenderer == null) return;

        List<Material> currentMaterials = originalMaterials.ToList();
        DestroyCurrentInstances();
        currentOutlineWidth = -1f; // Reset width when materials change

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

        // After applying new materials, immediately set the stored width if applicable
        if (currentOutlineWidth > 0 && (targetedInstance != null || availableInstance != null))
        {
            SetOutlineWidth(currentOutlineWidth); // Re-apply width to the new instance
        }
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
        return teamManager.IsInTeam(this.gameObject);
    }
}