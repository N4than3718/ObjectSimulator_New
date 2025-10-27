using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections; // <--- [�s�W] ���F Coroutine

public class RadialMenuController : MonoBehaviour
{
    [Header("�֤ߤޥ�")]
    [SerializeField] private TeamManager teamManager;
    [SerializeField] private InputActionReference openMenuActionRef; // �� Input Action ��i��

    [Header("UI ����")]
    [SerializeField] private Transform slotsContainer; // �Ҧ� Slot UI ��������
    [SerializeField] private GameObject slotPrefab; // �N��@�ӿﶵ�� UI Prefab

    [Header("�l����ޥ� (�N�� MenuRoot)")]
    [SerializeField] private GameObject slotsContainerObject;

    [Header("�I���϶� (Segments)")] // <-- [�s�W]
    [SerializeField] private GameObject backgroundSegmentsContainer; // <-- [�s�W] �� BackgroundSegments ��������o��
    private List<Image> backgroundSegments = new List<Image>();

    [Header("���L�]�w")]
    [SerializeField] private float radius = 150f;
    [SerializeField] private float inactiveSlotAlpha = 0.5f;
    [SerializeField][Range(0f, 1f)] private float timeScaleWhenOpen = 0.1f;

    [Header("�襤�ĪG")]
    [SerializeField] private Vector3 slotNormalScale = Vector3.one;       // <-- [�ק�] ��W�A��M��
    [SerializeField] private Vector3 slotHighlightedScale = new Vector3(1.3f, 1.3f, 1.3f); // <-- [�ק�] ��W
    [SerializeField] private Vector3 segmentNormalScale = Vector3.one;    // <-- [�s�W] Segment ���`�j�p
    [SerializeField] private Vector3 segmentHighlightedScale = new Vector3(1.1f, 1.1f, 1.1f); // <-- [�s�W] Segment ��j���v
    [SerializeField] private float scaleLerpSpeed = 10f;

    private List<GameObject> spawnedSlots = new List<GameObject>();
    private bool isMenuOpen = false;
    private int currentHoverIndex = -1;       // <--- [�s�W] ��e�ƹ����V�� Index (-1 = �L��)
    private int lastValidHoverIndex = -1;     // <--- [�s�W] �̫�@�ӷƹ����V�L�� *����* Index
    private int previousRenderedHoverIndex = -1;
    private float originalTimeScale = 1f;
    private bool actionSubscribed = false; // <--- [�s�W] �l�ܭq�\���A

    // --- Input System Setup ---
    private void Awake()
    {
        Debug.Log($"RadialMenuController: Awake() on {this.gameObject.name}. SHOULD RUN AT START.", this.gameObject);

        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();

        SubscribeToAction(); // Input System �q�\

        PopulateBackgroundSegments();

        // [�ק�] �T�O UI �@�}�l�O���ê� (�z�L�l����)
        SetChildrenVisibility(false);

        // ���m���A
        isMenuOpen = false;
        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;
        originalTimeScale = 1f;

        // ���Ҥޥ�
        if (backgroundSegmentsContainer == null || slotsContainerObject == null || slotPrefab == null)
        {
            Debug.LogError("RadialMenuController: �l����ޥ� (Background, SlotsContainer, SlotPrefab) ���b Inspector ���]�w!", this.gameObject);
        }
    }

    // [�s�W] �W�ߪ��q�\��k
    private void SubscribeToAction()
    {
        if (openMenuActionRef == null || openMenuActionRef.action == null)
        {
            Debug.LogError("RadialMenuController: Cannot subscribe, openMenuActionRef is not set!", this.gameObject);
            return;
        }

        if (!actionSubscribed)
        {
            try
            {
                openMenuActionRef.action.Enable(); // ���ҥ� Action
                openMenuActionRef.action.started += OpenMenu; // ���U Tab (Hold �}�l)
                openMenuActionRef.action.canceled += CloseMenu; // ��} Tab (Hold ����)
                actionSubscribed = true;
                Debug.Log("RadialMenuController: Successfully subscribed to input action events in Awake.", this.gameObject);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RadialMenuController: Error subscribing to input action: {e.Message}", this.gameObject);
            }
        }
        else
        {
            Debug.LogWarning("RadialMenuController: Already subscribed to input actions.", this.gameObject);
        }
    }

    private void PopulateBackgroundSegments()
    {
        backgroundSegments.Clear();
        if (backgroundSegmentsContainer != null)
        {
            // ���] Segment Image �������b Container ���U
            backgroundSegmentsContainer.GetComponentsInChildren<Image>(true, backgroundSegments); // true �]�t Inactive
            Debug.Log($"Found {backgroundSegments.Count} background segment images.");
            // �A�i��ݭn�ھڦW�ٱƧǡA�T�O���� 0 ���� Segment_0
            backgroundSegments.Sort((a, b) => a.name.CompareTo(b.name));
        }
        else
        {
            Debug.LogError("Background Segments Container ���]�w!");
        }
    }

    // [�ק�] OnEnable �{�b�u�� Debug
    private void OnEnable()
    {
        Debug.Log($"RadialMenuController: OnEnable() called on {this.gameObject.name}. SHOULD NOT HAPPEN IF STARTING INACTIVE.", this.gameObject);
        // �p�G Awake �̪��q�\�]���Y�ǭ�]���ѤF�A�o�̥i�H�A�դ@�� (�Ʈ�)
        // SubscribeToAction();
    }

    // [�ק�] OnDisable �{�b�u�� Debug
    private void OnDisable()
    {
        Debug.Log($"RadialMenuController: OnDisable() called on {this.gameObject.name}.", this.gameObject);
        // If the menu was forced closed by disabling the object, ensure state resets
        // if (isMenuOpen) ForceCloseMenu(); // �o�i��ɭP���ƩI�s ForceCloseMenu
    }

    // [�ק�] �� Input System �������q�\�޿貾�� OnDestroy
    private void OnDestroy()
    {
        Debug.Log($"RadialMenuController: OnDestroy() called on {this.gameObject.name}. Unsubscribing.", this.gameObject);
        if (openMenuActionRef == null || openMenuActionRef.action == null || !actionSubscribed) return;

        try
        {
            openMenuActionRef.action.started -= OpenMenu;
            openMenuActionRef.action.canceled -= CloseMenu;
            // �Ҽ{�O�_ Disable Action�A���M��A�� Action �޲z�覡
            // openMenuActionRef.action.Disable();
            actionSubscribed = false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RadialMenuController: Error unsubscribing from input action: {e.Message}", this.gameObject);
        }

        // �T�O TimeScale ��_���`
        if (Time.timeScale != 1f && originalTimeScale != 0) // �קK Time.timeScale �Q�d��
        {
            Debug.LogWarning($"RadialMenuController: Resetting TimeScale from {Time.timeScale} to {originalTimeScale} in OnDestroy.");
            Time.timeScale = originalTimeScale > 0 ? originalTimeScale : 1f;
        }
    }
    

    // --- Menu Logic ---
    private void OpenMenu(InputAction.CallbackContext context)
    {
        // [�s�W] �j�O Debug
        Debug.Log("RadialMenuController: OpenMenu ACTION TRIGGERED!");

        if (teamManager != null && teamManager.CurrentGameState == TeamManager.GameState.Spectator)
        {
            Debug.Log("OpenMenu prevented: Currently in Spectator mode.");
            return; // �b�[��̼Ҧ��U�A�����}���L
        }

        if (isMenuOpen || teamManager == null)
        {
            return;
        }

        Debug.Log("RadialMenuController: Opening Menu...");
        isMenuOpen = true;
        SetChildrenVisibility(true);
        SetCameraInputPause(true);
        Cursor.lockState = CursorLockMode.None; // ����ƹ�
        Cursor.visible = true;

        originalTimeScale = Time.timeScale; // �x�s�ثe�ɶ��y�t
        Time.timeScale = timeScaleWhenOpen; // ��C�ɶ�

        PopulateSlots(); // �ھڶ���ʺA�ͦ��ﶵ
        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;
    }

    private void CloseMenu(InputAction.CallbackContext context)
    {
        int indexToSwitchTo = lastValidHoverIndex;
        // [�s�W] �j�O Debug
        Debug.Log("RadialMenuController: CloseMenu ACTION TRIGGERED!");
        Debug.LogError($"!!!!!!!! CLOSE MENU ENTERED: lastValidHoverIndex was {lastValidHoverIndex}. Snapped value indexToSwitchTo = {indexToSwitchTo} !!!!!!!!", this.gameObject);

        if (!isMenuOpen || teamManager == null)
        {
            return;
        }

        Debug.Log($"RadialMenuController: Closing Menu... Selected Index: {currentHoverIndex}");
        isMenuOpen = false;
        SetChildrenVisibility(false);
        SetCameraInputPause(false);
        Cursor.lockState = CursorLockMode.Locked; // ��w�ƹ�
        Cursor.visible = false;

        Debug.Log($"CloseMenu: Attempting switch using lastValidHoverIndex = {lastValidHoverIndex}");

        // ���e�]�ΤW�@�V�^��j���Y�^�h
        int indexToScaleDown = (currentHoverIndex != -1) ? currentHoverIndex : previousRenderedHoverIndex;
        if (indexToScaleDown != -1)
        {
            if (indexToScaleDown < spawnedSlots.Count && spawnedSlots[indexToScaleDown] != null)
            {
                // SetScale(spawnedSlots[indexToScaleDown].transform, slotNormalScale); // <-- �ϥ� slotNormalScale
                StartCoroutine(ScaleCoroutine(spawnedSlots[indexToScaleDown].transform, slotNormalScale));
            }
            if (indexToScaleDown < backgroundSegments.Count && backgroundSegments[indexToScaleDown] != null)
            {
                // SetScale(backgroundSegments[indexToScaleDown].transform, segmentNormalScale); // <-- �ϥ� segmentNormalScale
                StartCoroutine(ScaleCoroutine(backgroundSegments[indexToScaleDown].transform, segmentNormalScale));
            }
        }

        // �ھ���w�� Index �������
        if (indexToSwitchTo != -1 && indexToSwitchTo < teamManager.team.Length
                    && teamManager.team[indexToSwitchTo].character != null)
        {
            Debug.Log($"CloseMenu: Condition PASSED using snapped index. Calling teamManager.SwitchToCharacterByIndex({indexToSwitchTo})...");
            teamManager.SwitchToCharacterByIndex(indexToSwitchTo);
        }
        else
        {
            Debug.LogWarning($"CloseMenu: No valid character selected (snapped index = {indexToSwitchTo}). No switch performed.");
        }

        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;

        if (Time.timeScale != originalTimeScale && originalTimeScale > 0)
        {
            Time.timeScale = originalTimeScale;
        }
        else if (Time.timeScale == 0)
        { // Safety net if original was 0
            Time.timeScale = 1f;
        }
    }

    private void SetChildrenVisibility(bool isVisible)
    {
        if (backgroundSegmentsContainer != null) backgroundSegmentsContainer.SetActive(isVisible);
        if (slotsContainerObject != null) slotsContainerObject.SetActive(isVisible);
        // if (centerDotObject != null) centerDotObject.SetActive(isVisible);

        // �p�G�����A�]�T�O�M���i��ݯd�� Slot
        if (!isVisible)
        {
            ClearSlots(); // �T�O�����ɲM��

            // �T�O���m�Y�񪬺A (�p�G�ʵe�Q���_)
            if (previousRenderedHoverIndex != -1 && previousRenderedHoverIndex < spawnedSlots.Count && spawnedSlots[previousRenderedHoverIndex] != null) { spawnedSlots[previousRenderedHoverIndex].transform.localScale = slotNormalScale; }
            currentHoverIndex = -1;
            lastValidHoverIndex = -1;
            previousRenderedHoverIndex = -1;
        }
    }

    private void ForceCloseMenu() // ��������� (�Ҧp OnDisable)
    {
        isMenuOpen = false;
        SetChildrenVisibility(false);
        SetCameraInputPause(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = originalTimeScale > 0 ? originalTimeScale : 1f; // �קK TimeScale �� 0

        int indexToScaleDown = (currentHoverIndex != -1) ? currentHoverIndex : previousRenderedHoverIndex;
        if (indexToScaleDown != -1)
        {
            if (indexToScaleDown < spawnedSlots.Count && spawnedSlots[indexToScaleDown] != null)
            {
                // SetScale(spawnedSlots[indexToScaleDown].transform, slotNormalScale); // <-- �ϥ� slotNormalScale
                StartCoroutine(ScaleCoroutine(spawnedSlots[indexToScaleDown].transform, slotNormalScale));
            }
            if (indexToScaleDown < backgroundSegments.Count && backgroundSegments[indexToScaleDown] != null)
            {
                // SetScale(backgroundSegments[indexToScaleDown].transform, segmentNormalScale); // <-- �ϥ� segmentNormalScale
                StartCoroutine(ScaleCoroutine(backgroundSegments[indexToScaleDown].transform, segmentNormalScale));
            }
        }
        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;
        ClearSlots();
    }


    // --- UI Population ---
    private void PopulateSlots()
    {
        ClearSlots();
        if (slotPrefab == null || slotsContainer == null || teamManager == null) return;

        int teamSize = teamManager.team.Length; // �� MaxTeamSize �@���` Slot ��
        float angleStep = 360f / teamSize;

        for (int i = 0; i < teamSize; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsContainerObject.transform);
            spawnedSlots.Add(slotGO);

            // �p���m (���y���ઽ���y��)
            float angleRad = (90f - (i * angleStep)) * Mathf.Deg2Rad; // 90�׶}�l (����), ���ɰw
            float x = radius * Mathf.Cos(angleRad);
            float y = radius * Mathf.Sin(angleRad);
            slotGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            Image iconImage = slotGO.transform.Find("Icon")?.GetComponent<Image>(); // <-- [�ק�] ��T���W�s Icon ���l����W�� Image

            if (iconImage == null) // <-- [�s�W] �ˬd�O�_��� Icon Image
            {
                Debug.LogError($"Slot Prefab ({slotPrefab.name}) �ʤ֦W�� 'Icon' ���l����Ψ�W�� Image ����!", slotGO);
                // ... (�B�z���~�A�Ҧp return �� continue)
            }

            TeamUnit unit = teamManager.team[i];

            if (unit.character != null /* && slotImage != null */ ) // <-- iconImage ���ˬd����U��
            {
                if (iconImage != null && unit.character.radialMenuIcon != null) // <-- [�ק�] ��� iconImage
                {
                    iconImage.sprite = unit.character.radialMenuIcon; // <--- [�ק�] �]�w�� iconImage
                    iconImage.color = Color.white;
                }
                else if (iconImage != null) // �� Image ���S Sprite
                {
                    Debug.LogWarning($"���� {unit.character.name} �S�����w RadialMenuIcon!", unit.character.gameObject);
                    iconImage.sprite = null;
                    var tempColor = Color.gray; tempColor.a = inactiveSlotAlpha; iconImage.color = tempColor;
                }
                slotGO.name = $"Slot_{i}_{unit.character.name}";
            }
            else if (iconImage != null) // �ť� Slot
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
                slotGO.name = $"Slot_{i}_Empty";
            }

            slotGO.transform.localScale = slotNormalScale;
            slotGO.SetActive(true);
        }
    }

    private void ClearSlots()
    {
        foreach (GameObject slot in spawnedSlots)
        {
            Destroy(slot);
        }
        spawnedSlots.Clear();
    }

    // --- Selection Logic (�b Update ������) ---
    void Update()
    {
        if (!isMenuOpen) return;

        // --- �B�J 1-3 ����: ���o direction, �ˬd dead zone, �p�� atan2 angle ---
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 centerPos = this.GetComponent<RectTransform>().position;
        Vector2 direction = mousePos - centerPos;
        float deadZoneRadius = radius * 0.2f;

        int calculatedIndex = -1; // �w�]�L��

        int previousRenderedHoverIndexForScale = previousRenderedHoverIndex;

        if (direction.magnitude >= deadZoneRadius) // �u���b Dead Zone �~�~�p��
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            int teamSize = teamManager.team.Length;
            if (teamSize > 0)
            {
                float angleStep = 360f / teamSize;
                float uiAngle = (450f - angle) % 360f;
                float adjustedUiAngle = (uiAngle + angleStep / 2f) % 360f;
                calculatedIndex = Mathf.FloorToInt(adjustedUiAngle / angleStep);
            }
        }

        // --- [�ק�] ��s Hover ���A ---
        previousRenderedHoverIndex = currentHoverIndex; // �O��W�@�V�O�ֳQ��j

        // �ˬd calculatedIndex �O�_���V���Ķ���
        if (calculatedIndex >= 0 && calculatedIndex < teamManager.team.Length && teamManager.team[calculatedIndex].character != null)
        {
            currentHoverIndex = calculatedIndex; // ��s��e���V
            lastValidHoverIndex = calculatedIndex; // **��w**�̫�@�Ӧ��Ī����V
            // Debug.Log($"Update: Valid Hover {currentHoverIndex}. Last Valid {lastValidHoverIndex}");
        }
        else
        {
            currentHoverIndex = -1; // ���V�L�İϰ�
            Debug.Log($"Update: currentHoverIndex set to -1. lastValidHoverIndex remains {lastValidHoverIndex}"); // �ˬd Log
        }

        // --- [�ק�] �ھ� Hover ���A�ܤƳB�z�Y�� ---
        if (currentHoverIndex != previousRenderedHoverIndex)
        {
            // ��W�@�ө�j���Y�^�h
            if (previousRenderedHoverIndex != -1 && previousRenderedHoverIndex < spawnedSlots.Count && spawnedSlots[previousRenderedHoverIndex] != null)
            {
                StartCoroutine(ScaleCoroutine(spawnedSlots[previousRenderedHoverIndex].transform, slotNormalScale));
            }

            if (previousRenderedHoverIndexForScale < backgroundSegments.Count && backgroundSegments[previousRenderedHoverIndexForScale] != null)
            {
                StartCoroutine(ScaleCoroutine(backgroundSegments[previousRenderedHoverIndexForScale].transform, segmentNormalScale)); // �ϥάۦP�� normalScale
            }

            // ��{�b���V����j
            if (currentHoverIndex != -1 && currentHoverIndex < spawnedSlots.Count && spawnedSlots[currentHoverIndex] != null)
            {
                StartCoroutine(ScaleCoroutine(spawnedSlots[currentHoverIndex].transform, slotHighlightedScale));
            }

            if (currentHoverIndex < backgroundSegments.Count && backgroundSegments[currentHoverIndex] != null)
            {
                StartCoroutine(ScaleCoroutine(backgroundSegments[currentHoverIndex].transform, segmentHighlightedScale)); // �ϥάۦP�� highlightedScale
            }
        }
    }

    private void SetScale(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform != null)
        {
            targetTransform.localScale = targetScale;
        }
    }

    // --- [�s�W] ���U��k�G�� Coroutine ���Y��ʵe (�i��) ---
    private System.Collections.IEnumerator ScaleCoroutine(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform == null) yield break;

        Vector3 startScale = targetTransform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            // �p�G�b�ʵe�L�{���ؼ� Transform �Q�P�� (�Ҧp������� ClearSlots)�A�ߨ谱��
            if (targetTransform == null) yield break;

            t += Time.unscaledDeltaTime * scaleLerpSpeed; // �ϥ� unscaledDeltaTime �קK�� TimeScale �v�T
            targetTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null; // ���ݤU�@�V
        }
        // �T�O�̲� scale �O��T��
        if (targetTransform != null) targetTransform.localScale = targetScale;
    }

    private void SetCameraInputPause(bool paused)
    {
        if (teamManager == null) return;

        Debug.Log($"Setting camera input pause state to: {paused}");
        List<MonoBehaviour> allControllers = teamManager.GetAllCameraControllers();
        Debug.Log($"Found {allControllers.Count} camera controllers to pause/unpause.");
        foreach (var controller in allControllers)
        {
            if (controller is CamControl charCamCtrl)
            {
                Debug.Log($"Attempting to set IsInputPaused={paused} on controller: {controller.GetType().Name}");
                charCamCtrl.IsInputPaused = paused;
            }
        }
    }
}