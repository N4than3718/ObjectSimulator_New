using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ������ �֤߭ק�G���� RequireComponent �ݩ� ������
// [RequireComponent(typeof(Renderer))]
public class HighlightableObject : MonoBehaviour
{
    [Header("Highlight Materials")]
    [Tooltip("�Q�襤�ɪ����Ⱚ�G����ҪO")]
    public Material targetedHighlightTemplate;
    [Tooltip("�i�[�J����ɪ��զⰪ�G����ҪO")]
    public Material availableHighlightTemplate;

    // --- �������A ---
    private Renderer objectRenderer;
    private Material[] originalMaterials;
    private Material targetedInstance;
    private Material availableInstance;

    private bool isTargeted = false;
    private bool isAvailable = false;

    void Awake()
    {
        // ������ �֤߭ק�G�q GetComponent �אּ GetComponentInChildren ������
        objectRenderer = GetComponentInChildren<Renderer>(true); // true ��ܥ]�t�D���ʪ��l����
        // ��������������������������������������������������������

        if (objectRenderer == null)
        {
            Debug.LogError($"HighlightableObject on {gameObject.name} cannot find a Renderer in its children!", this);
            enabled = false; // �䤣�� Renderer �N�����T�θ}��
            return;
        }
        originalMaterials = objectRenderer.materials;
    }

    // �� SpectatorController �� PlayerMovement �I�s
    public void SetTargetedHighlight(bool active)
    {
        if (!this.enabled) return; // �p�G�@�}�l�N�S Renderer�A������^
        if (isTargeted != active)
        {
            isTargeted = active;
            UpdateHighlightMaterials();
        }
    }

    // �� HighlightManager �I�s
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
        // �ˬd�o�Ӹ}���Ҧb�� GameObject �O�_�b���
        // (�]���ڭ̲{�b��}�����b������W�A�ҥH���ݭn .root �F)
        return teamManager.IsInTeam(this.gameObject);
    }
}