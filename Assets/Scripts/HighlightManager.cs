using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("�զⰪ�G����ҪO (Available)")]
    [SerializeField] private Material availableHighlightTemplate;
    // ������ �s�W�G��Ⱚ�G�ҪO ������
    [Tooltip("��Ⱚ�G����ҪO (Inactive Team Member)")]
    [SerializeField] private Material inactiveTeamHighlightTemplate;
    // ��������������������������������

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.2f;

    [Header("Dynamic Outline")] // �o�ǰѼƲ{�b�P�ɥΩ�զ�M���
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.03f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();

        // ������ �ˬd�Ҧ����n�ޥ� ������
        if (teamManager == null || availableHighlightTemplate == null || inactiveTeamHighlightTemplate == null)
        {
            Debug.LogError("HighlightManager is missing references (TeamManager, Available Template, or Inactive Team Template)!");
            enabled = false;
            return;
        }
        // ����������������������������

        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));
        foreach (var obj in allHighlightables)
        {
            if (obj != null && obj.enabled)
            {
                // ���ӼҪO���ǵ� HighlightableObject
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
            GameObject activeCharacter = teamManager.ActiveCharacterGameObject; // �����e�ޱ�������

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

                    // ������ �֤߭ק�G�ھڪ��A�]�m���G ������
                    if (isInTeam && !isActive)
                    {
                        // �b��������Q�ޱ� -> ���
                        highlightable.SetInactiveTeamHighlight(true);
                        highlightable.SetAvailableHighlight(false); // �T�O�զ�����
                    }
                    else if (!isInTeam)
                    {
                        // ���b��� -> �զ�
                        highlightable.SetAvailableHighlight(true);
                        highlightable.SetInactiveTeamHighlight(false); // �T�O�������
                    }
                    else // (isInTeam && isActive)
                    {
                        // �O��e�ޱ������� -> �����զ�M��� (����� PlayerMovement/Spectator ����)
                        highlightable.SetAvailableHighlight(false);
                        highlightable.SetInactiveTeamHighlight(false);
                    }

                    // �p�G��ܪ��O�զ�κ��A�N��s�����e��
                    if ((!isInTeam || (isInTeam && !isActive)) && !highlightable.IsTargeted) // IsTargeted �O�@�Ӱ��]���ݩʡA�ڭ̻ݭn�[�^�h
                    {
                        float distance = Vector3.Distance(currentCameraTransform.position, highlightable.transform.position);
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightable.SetOutlineWidth(newWidth);
                    }
                    // �p�G�O Targeted (����)�A�e�ץ� PlayerMovement/Spectator ����
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}