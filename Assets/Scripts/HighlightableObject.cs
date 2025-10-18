using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Renderer))] // �T�O���� Renderer
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

    private bool isTargeted = false; // �O�_�Q���a�ǬP�˷�
    private bool isAvailable = false; // �O�_�i�[�J��������[�J

    void Awake()
    {
        // ���զ۰���� Renderer�A�u����l����
        objectRenderer = GetComponentInChildren<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError($"HighlightableObject on {gameObject.name} cannot find a Renderer!", this);
            enabled = false; // �䤣��N�T��
            return;
        }
        originalMaterials = objectRenderer.materials;
    }

    // �� SpectatorController �� PlayerMovement �I�s
    public void SetTargetedHighlight(bool active)
    {
        if (isTargeted != active)
        {
            isTargeted = active;
            UpdateHighlightMaterials();
        }
    }

    // �� HighlightManager �I�s
    public void SetAvailableHighlight(bool active)
    {
        if (!this.enabled) return; // �p�G�@�}�l�N�S Renderer�A������^
        if (isAvailable != active)
        {
            isAvailable = active;
            UpdateHighlightMaterials();
        }
    }

    // �֤ߡG�ھڪ��A�M�w�̲���ܭ��Ӱ��G (�����u��)
    private void UpdateHighlightMaterials()
    {
        if (objectRenderer == null) return;

        List<Material> currentMaterials = originalMaterials.ToList();
        DestroyCurrentInstances(); // ���M���ª����

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

    // �M�z�ʺA�Ыت�������
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
        // �T�O����P���ɤ]�M�z����
        DestroyCurrentInstances();
    }

    // �~���}���i�H�d�ߪ���O�_�w�Q�۶�
    public bool IsInTeam(TeamManager teamManager)
    {
        if (teamManager == null) return false;
        return teamManager.IsInTeam(this.gameObject.transform.root.gameObject); // �ˬd�ڪ���
    }

}