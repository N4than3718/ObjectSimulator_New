using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HighlightableObject : MonoBehaviour
{
    [Header("Highlight Materials")]
    [Tooltip("�Q�襤�ɪ����Ⱚ�G����ҪO")]
    public Material targetedHighlightTemplate;
    [Tooltip("�i�[�J����ɪ��զⰪ�G����ҪO")]
    public Material availableHighlightTemplate;
    // ������ �s�W�G�ݩR���ͪ���Ⱚ�G ������
    [Tooltip("�ݩR���ͪ���Ⱚ�G����ҪO")]
    public Material inactiveTeamHighlightTemplate;
    // ��������������������������������������

    // --- �������A ---
    public bool IsTargeted => isTargeted; // ���~���i�H�d�߬O�_�Q�˷�
    public bool IsAvailable() => isAvailable;
    public bool IsInactiveTeamMember() => isInactiveTeamMember;
    private Renderer objectRenderer;
    private Material[] originalMaterials;
    private Material targetedInstance;
    private Material availableInstance;
    private Material inactiveInstance; // �s�W�����

    private bool isTargeted = false;
    private bool isAvailable = false;
    private bool isInactiveTeamMember = false; // �s�W���A

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

    // �� SpectatorController �� PlayerMovement �I�s
    public void SetTargetedHighlight(bool active)
    {
        if (!this.enabled) return;
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
            // �p�G���b�ҥ� Available�A�T�O Inactive �O������
            if (active) isInactiveTeamMember = false;
            UpdateHighlightMaterials();
        }
    }

    // ������ �s�W�G�� HighlightManager �I�s ������
    public void SetInactiveTeamHighlight(bool active)
    {
        if (!this.enabled) return;
        if (isInactiveTeamMember != active)
        {
            isInactiveTeamMember = active;
            // �p�G���b�ҥ� Inactive�A�T�O Available �O������
            if (active) isAvailable = false;
            UpdateHighlightMaterials();
        }
    }
    // ��������������������������������������

    public void SetOutlineWidth(float width)
    {
        if (!this.enabled || Mathf.Approximately(currentOutlineWidth, width)) return;
        currentOutlineWidth = width;
        // ������ �u���šG�� > �� > �� ������
        Material activeInstance = targetedInstance ?? inactiveInstance ?? availableInstance;
        // ��������������������������������
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

        // ������ �֤߭ק�G�[�J��Ⱚ�G���P�_�޿�A�u���� �� > �� > �� ������
        if (isTargeted && targetedHighlightTemplate != null)
        {
            targetedInstance = new Material(targetedHighlightTemplate);
            currentMaterials.Add(targetedInstance);
        }
        else if (isInactiveTeamMember && inactiveTeamHighlightTemplate != null) // ���⤣�G�~�ˬd���
        {
            inactiveInstance = new Material(inactiveTeamHighlightTemplate);
            currentMaterials.Add(inactiveInstance);
        }
        else if (isAvailable && availableHighlightTemplate != null) // ���񳣤��G�~�ˬd�զ�
        {
            availableInstance = new Material(availableHighlightTemplate);
            currentMaterials.Add(availableInstance);
        }
        // ��������������������������������������������������������������

        objectRenderer.materials = currentMaterials.ToArray();

        // ���s���μe��
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
        // ������ �M�z����� ������
        if (inactiveInstance != null) { Destroy(inactiveInstance); inactiveInstance = null; }
        // ������������������������
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