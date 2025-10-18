using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("�զⰪ�G����ҪO (Available)")]
    [SerializeField] private Material availableHighlightTemplate;
    [Tooltip("��Ⱚ�G����ҪO (Inactive Team Member)")]
    [SerializeField] private Material inactiveTeamHighlightTemplate;

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.2f;

    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.03f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private HashSet<HighlightableObject> allHighlightables = new HashSet<HighlightableObject>();
    private Coroutine updateCoroutine;

    public static HighlightManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // �����
        }
    }

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null || availableHighlightTemplate == null || inactiveTeamHighlightTemplate == null)
        {
            Debug.LogError("HighlightManager is missing references!");
            enabled = false;
            return;
        }

        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));
        foreach (var obj in allHighlightables)
        {
            if (obj != null && obj.enabled)
            {
                obj.availableHighlightTemplate = this.availableHighlightTemplate;
                obj.inactiveTeamHighlightTemplate = this.inactiveTeamHighlightTemplate;
            }
        }
        // �Ұʩw����s��{
        updateCoroutine = StartCoroutine(UpdateAvailableHighlightsLoop());
    }

    public void RegisterHighlightable(HighlightableObject obj)
    {
        if (obj != null && allHighlightables.Add(obj))
        {
            // �b�o�̳]�w�ҪO
            obj.availableHighlightTemplate = this.availableHighlightTemplate;
            obj.inactiveTeamHighlightTemplate = this.inactiveTeamHighlightTemplate;
        }
    }

    public void UnregisterHighlightable(HighlightableObject obj)
    {
        if (obj != null)
        {
            allHighlightables.Remove(obj);
        }
    }

    // ���}���j���s��k
    public void ForceHighlightUpdate()
    {
        // Debug.Log("HighlightManager: Forcing Highlight Update!"); // ������
        UpdateAllHighlights();
    }

    // �w����s����{�j��
    IEnumerator UpdateAvailableHighlightsLoop()
    {
        while (true)
        {
            UpdateAllHighlights();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    // �֤��޿�
    private void UpdateAllHighlights()
    {
        Transform currentCameraTransform = teamManager.CurrentCameraTransform;
        GameObject activeCharacter = teamManager.ActiveCharacterGameObject;

        if (currentCameraTransform == null)
        {
            // Debug.LogWarning("HighlightManager could not get current camera transform."); // ��� Console ����
            return; // �䤣����v���N����s
        }

        foreach (HighlightableObject highlightable in allHighlightables)
        {
            if (highlightable != null && highlightable.enabled)
            {
                GameObject currentObject = highlightable.gameObject;
                bool isInTeam = highlightable.IsInTeam(teamManager);
                bool isActive = (currentObject == activeCharacter);

                // --- ���A�P�_ ---
                if (isInTeam && !isActive)
                {
                    highlightable.SetInactiveTeamHighlight(true);
                    highlightable.SetAvailableHighlight(false);
                }
                else if (!isInTeam)
                {
                    highlightable.SetAvailableHighlight(true);
                    highlightable.SetInactiveTeamHighlight(false);
                }
                else // (isInTeam && isActive)
                {
                    highlightable.SetAvailableHighlight(false);
                    highlightable.SetInactiveTeamHighlight(false);
                }

                // --- ��s�e�� (�u��s��ܥզ�κ�⪺) ---
                // �ڭ̻ݭn HighlightableObject ���� IsTargeted ���A
                bool isTargetedNow = highlightable.IsTargeted; // Assuming IsTargeted property exists

                // Update width only if it's NOT the active character AND NOT currently targeted (yellow)
                if (!isActive && !isTargetedNow && (highlightable.IsAvailable() || highlightable.IsInactiveTeamMember())) // Assuming helper methods exist
                {
                    float distance = Vector3.Distance(currentCameraTransform.position, highlightable.transform.position);
                    float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                    float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                    highlightable.SetOutlineWidth(newWidth);
                }
                // If it IS targeted (yellow), the width is controlled by Spectator/PlayerMovement
            }
        }
    }

} // End of HighlightManager class