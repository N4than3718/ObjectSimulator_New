using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("�զⰪ�G����ҪO")]
    [SerializeField] private Material availableHighlightTemplate; // �ҪO�� HighlightableObject ��

    [Header("Settings")]
    [Tooltip("���y������s���G���W�v�]��^")]
    [SerializeField] private float updateInterval = 0.5f;

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null)
        {
            Debug.LogError("HighlightManager cannot find TeamManager!");
            enabled = false;
            return;
        }
        if (availableHighlightTemplate == null)
        {
            Debug.LogError("HighlightManager needs the Available Highlight Material Template!");
            enabled = false;
            return;
        }

        // ���������Ҧ��i���G������
        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));

        // ��զ�ҪO�ǵ��C�Ӫ���
        foreach (var obj in allHighlightables)
        {
            obj.availableHighlightTemplate = this.availableHighlightTemplate;
        }


        StartCoroutine(UpdateAvailableHighlights());
    }

    IEnumerator UpdateAvailableHighlights()
    {
        while (true)
        {
            foreach (HighlightableObject highlightable in allHighlightables)
            {
                if (highlightable != null && highlightable.enabled) // �T�O�����٦s�b�B�}���ҥ�
                {
                    // �ˬd����O�_**���b**���
                    bool shouldBeAvailable = !highlightable.IsInTeam(teamManager);
                    highlightable.SetAvailableHighlight(shouldBeAvailable);
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}