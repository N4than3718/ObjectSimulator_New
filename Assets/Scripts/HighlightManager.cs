using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("�զⰪ�G����ҪO")]
    [SerializeField] private Material availableHighlightTemplate;
    [Tooltip("�[�����v���� Transform (�p�G�����w�|�۰ʬd��)")]
    [SerializeField] private Transform spectatorCameraTransform;

    [Header("Settings")]
    [Tooltip("���y������s���G���W�v�]��^")]
    [SerializeField] private float updateInterval = 0.2f; // �i�H�y�L�[�֧�s�W�v

    // ������ �s�W�G�զⰪ�G���ʺA�����Ѽ� ������
    [Header("Dynamic Outline (Available)")]
    [Tooltip("�������̤p�e��")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [Tooltip("�������̤j�e��")]
    [SerializeField] private float maxOutlineWidth = 0.03f; // �զ�i�H�y�L�Ӥ@�I
    [Tooltip("�F��̤j�e�שһݪ��Z��")]
    [SerializeField] private float maxDistanceForOutline = 50f;
    // ����������������������������������������������

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        // --- ����ޥ� ---
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (spectatorCameraTransform == null)
        {
            SpectatorController sc = FindAnyObjectByType<SpectatorController>();
            if (sc != null) spectatorCameraTransform = sc.transform;
        }

        // --- ���~�ˬd ---
        if (teamManager == null || availableHighlightTemplate == null || spectatorCameraTransform == null)
        {
            Debug.LogError("HighlightManager is missing required references (TeamManager, Available Template, or Spectator Camera Transform)!");
            enabled = false;
            return;
        }

        // --- ��l�ƦC��M�ҪO ---
        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));
        foreach (var obj in allHighlightables)
        {
            if (obj != null && obj.enabled) // Check if obj is valid and enabled
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
            // �p�G���a���b�ޱ�����A�N���ݭn��s�զⰪ�G (�Υi�H��ܧ�s�A���M��]�p)
            // if (teamManager.CurrentState == TeamManager.GameState.Possessing) {
            //     yield return new WaitForSeconds(updateInterval);
            //     continue;
            // }

            foreach (HighlightableObject highlightable in allHighlightables)
            {
                if (highlightable != null && highlightable.enabled)
                {
                    bool shouldBeAvailable = !highlightable.IsInTeam(teamManager);
                    highlightable.SetAvailableHighlight(shouldBeAvailable);

                    // ������ �֤߭ק�G�p�G��ܥզⰪ�G�A�N�p��ó]�w�e�� ������
                    if (shouldBeAvailable)
                    {
                        float distance = Vector3.Distance(spectatorCameraTransform.position, highlightable.transform.position);
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightable.SetOutlineWidth(newWidth); // ��s�e��
                    }
                    // ����������������������������������������������
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}