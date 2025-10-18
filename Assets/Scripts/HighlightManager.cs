using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("�զⰪ�G����ҪO")]
    [SerializeField] private Material availableHighlightTemplate;
    // ���A�ݭn spectatorCameraTransform �����F�ATeamManager �|����

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.2f;

    [Header("Dynamic Outline (Available)")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.03f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null || availableHighlightTemplate == null)
        {
            Debug.LogError("HighlightManager is missing TeamManager or Available Template!");
            enabled = false;
            return;
        }

        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));
        foreach (var obj in allHighlightables)
        {
            if (obj != null && obj.enabled)
            {
                obj.availableHighlightTemplate = this.availableHighlightTemplate;
            }
        }
        StartCoroutine(UpdateAvailableHighlights());
    }

    IEnumerator UpdateAvailableHighlights()
    {
        while (true)
        {
            // ������ �֤߭ק�G�����e���D����v�� ������
            Transform currentCameraTransform = teamManager.CurrentCameraTransform;
            if (currentCameraTransform == null)
            { // �p�G�s��v�����䤣��A�N���L�o�@��
                Debug.LogWarning("HighlightManager could not get current camera transform from TeamManager.");
                yield return new WaitForSeconds(updateInterval);
                continue;
            }
            // ����������������������������������������������

            foreach (HighlightableObject highlightable in allHighlightables)
            {
                if (highlightable != null && highlightable.enabled)
                {
                    bool shouldBeAvailable = !highlightable.IsInTeam(teamManager);
                    highlightable.SetAvailableHighlight(shouldBeAvailable);

                    if (shouldBeAvailable)
                    {
                        // ������ �֤߭ק�G�ϥη�e���D��v���p��Z�� ������
                        float distance = Vector3.Distance(currentCameraTransform.position, highlightable.transform.position);
                        // ����������������������������������������������
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightable.SetOutlineWidth(newWidth);
                    }
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}