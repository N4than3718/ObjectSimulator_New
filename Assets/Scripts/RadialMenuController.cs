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
    private bool allowHoverDetection = false; // <--- [�s�W] ����O�_���\ Update �p�� Hover
    private Coroutine allowHoverCoroutine = null; // <--- [�s�W] �l�ܩ��� Coroutine
    private int currentHoverIndex = -1;       // <--- [�s�W] ��e�ƹ����V�� Index (-1 = �L��)
    private int lastValidHoverIndex = -1;     // <--- [�s�W] �̫�@�ӷƹ����V�L�� *����* Index
    private int previousRenderedHoverIndex = -1;
    private float originalTimeScale = 1f;
    private bool actionSubscribed = false; // <--- [�s�W] �l�ܭq�\���A

    private Dictionary<Transform, Coroutine> runningScaleCoroutines = new Dictionary<Transform, Coroutine>();

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
        StopAllManagedScaleCoroutines(); // �M�z����i��ݯd���°ʵe
        Debug.Log("RadialMenuController: OpenMenu ACTION TRIGGERED!");
        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;
        allowHoverDetection = false; // <-- [�s�W] ���T���

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

        // ���] Slots
        foreach (var slotGO in spawnedSlots)
        {
            if (slotGO != null)
            {
                // �������i���Q PopulateSlots �N�~�Ұʪ� Coroutine (���M���ӥi��)
                // StopManagedScaleCoroutine(slotGO.transform); // �γ\���ݭn�H
                SetScale(slotGO.transform, slotNormalScale); // <<<--- �j��]�^ Normal
            }
        }
        // ���] Segments (�A���T�O)
        foreach (var segImage in backgroundSegments)
        {
            if (segImage != null)
            {
                // StopManagedScaleCoroutine(segImage.transform); // �γ\���ݭn�H
                SetScale(segImage.transform, segmentNormalScale); // <<<--- �j��]�^ Normal
            }
        }

        if (allowHoverCoroutine != null) StopCoroutine(allowHoverCoroutine); // �H���U�@
        allowHoverCoroutine = StartCoroutine(EnableHoverDetectionAfterDelay(0.1f)); // �Ҧp���� 0.1 ��
    }

    private void CloseMenu(InputAction.CallbackContext context)
    {
        int finalIndex = -1;
        Vector2 finalPointerPos = Pointer.current.position.ReadValue();
        Vector2 centerPosNow = this.GetComponent<RectTransform>().position; // ���s��������I
        Vector2 finalDirection = finalPointerPos - centerPosNow;
        float deadZone = radius * 0.2f;

        if (finalDirection.magnitude >= deadZone)
        {
            float finalAngle = Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg;
            if (finalAngle < 0) finalAngle += 360f;
            int teamSizeNow = teamManager.team.Length; // ���s�������j�p
            if (teamSizeNow > 0)
            {
                float angleStepNow = 360f / teamSizeNow;
                float finalUiAngle = (450f - finalAngle) % 360f;
                float finalAdjustedUiAngle = (finalUiAngle + angleStepNow / 2f) % 360f;
                finalIndex = Mathf.FloorToInt(finalAdjustedUiAngle / angleStepNow);

                // �̫�����
                if (finalIndex < 0 || finalIndex >= teamSizeNow || teamManager.team[finalIndex].character == null)
                {
                    finalIndex = -1; // �p�G��X�ӬO�L�� Slot�A�٬O�]�� -1
                }
            }
        }
        // ��o�ӧY�ɺ�X�Ӫ��ȡA�@���ڭ̪��ַ�
        int indexToSwitchTo = finalIndex;

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

        if (allowHoverCoroutine != null)
        {
            StopCoroutine(allowHoverCoroutine);
            allowHoverCoroutine = null;
        }
        allowHoverDetection = false; // ���m

        Debug.Log($"CloseMenu: Attempting switch using lastValidHoverIndex = {lastValidHoverIndex}");

        // 1. �ߨ谱��Ҧ����b���檺�Y��ʵe
        StopAllManagedScaleCoroutines();

        // 2. ������̫ᰪ�G�������]�p�G���^�Y�^���`�j�p
        int indexToScaleDown = (currentHoverIndex != -1) ? currentHoverIndex : previousRenderedHoverIndex;
        if (indexToScaleDown != -1)
        {
            // �����]�w Slot �j�p
            if (indexToScaleDown < spawnedSlots.Count && spawnedSlots[indexToScaleDown] != null)
            {
                SetScale(spawnedSlots[indexToScaleDown].transform, slotNormalScale); // �ϥΪ����]�w
            }
            // �����]�w Segment �j�p
            if (indexToScaleDown < backgroundSegments.Count && backgroundSegments[indexToScaleDown] != null)
            {
                SetScale(backgroundSegments[indexToScaleDown].transform, segmentNormalScale); // �ϥΪ����]�w
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
        if (allowHoverCoroutine != null)
        {
            StopCoroutine(allowHoverCoroutine);
            allowHoverCoroutine = null;
        }
        allowHoverDetection = false; // ���m

        StopAllManagedScaleCoroutines();
        isMenuOpen = false;
        SetChildrenVisibility(false);
        SetCameraInputPause(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = originalTimeScale > 0 ? originalTimeScale : 1f; // �קK TimeScale �� 0

        int indexToScaleDown = (currentHoverIndex != -1) ? currentHoverIndex : previousRenderedHoverIndex;
        if (indexToScaleDown != -1)
        {
            if (indexToScaleDown < spawnedSlots.Count && spawnedSlots[indexToScaleDown] != null) { SetScale(spawnedSlots[indexToScaleDown].transform, slotNormalScale); }
            if (indexToScaleDown < backgroundSegments.Count && backgroundSegments[indexToScaleDown] != null) { SetScale(backgroundSegments[indexToScaleDown].transform, segmentNormalScale); }
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

        foreach (var segImage in backgroundSegments) // <-- ��� segImage �ܼƦW
        {
            if (segImage != null)
            {
                //Debug.Log($"PopulateSlots: Resetting Segment '{segImage.name}' to scale {segmentNormalScale}");
                SetScale(segImage.transform, segmentNormalScale); // �����]�w
            }
        }

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

            SetScale(slotGO.transform, slotNormalScale);
            slotGO.SetActive(true);
        }
    }

    private void ClearSlots()
    {
        for (int i = spawnedSlots.Count - 1; i >= 0; i--) // �ϦV���N����w��
        {
            GameObject slotGO = spawnedSlots[i];
            if (slotGO != null)
            {
                Transform slotTransform = slotGO.transform;
                // 1. ���� Slot �W���ʵe
                StopManagedScaleCoroutine(slotTransform);
                // 2. �j��]�^ Normal Scale
                SetScale(slotTransform, slotNormalScale);

                // 3. ������� Segment �W���ʵe (�p�G�s�b)
                if (i >= 0 && i < backgroundSegments.Count && backgroundSegments[i] != null)
                {
                    Transform segTransform = backgroundSegments[i].transform;
                    StopManagedScaleCoroutine(segTransform);
                    SetScale(segTransform, segmentNormalScale);
                }

                // 4. �̫�~ Destroy
                // Debug.Log($"Destroying slot {i}");
                Destroy(slotGO);
            }
        }
        spawnedSlots.Clear();
        runningScaleCoroutines.Clear(); // <-- [�s�W] �T�O�r��]�Q�M��
    }

    // --- Selection Logic (�b Update ������) ---
    void Update()
    {
        if (!isMenuOpen || !allowHoverDetection) return;

        // --- �B�J 1-3 ����: ���o direction, �ˬd dead zone, �p�� atan2 angle ---
        Vector2 pointerPos = Pointer.current.position.ReadValue();
        Vector2 centerPos = this.GetComponent<RectTransform>().position;
        Vector2 direction = pointerPos - centerPos; // <-- [�ק�]
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

        if (currentHoverIndex != previousRenderedHoverIndexForScale) // �Y�� Index ����
        {
            // --- �Y�^�W�@�� ---
            if (previousRenderedHoverIndexForScale != -1)
            {
                // �Y�^ Slot
                if (previousRenderedHoverIndexForScale < spawnedSlots.Count && spawnedSlots[previousRenderedHoverIndexForScale] != null)
                {
                    // [�ק�] �ɤW slotNormalScale
                    StartManagedScaleCoroutine(spawnedSlots[previousRenderedHoverIndexForScale].transform, slotNormalScale);
                }
                // �P�B�Y�^ Segment
                if (previousRenderedHoverIndexForScale < backgroundSegments.Count && backgroundSegments[previousRenderedHoverIndexForScale] != null)
                {
                    // [�ק�] �ɤW segmentNormalScale
                    StartManagedScaleCoroutine(backgroundSegments[previousRenderedHoverIndexForScale].transform, segmentNormalScale);
                }
            }

            // --- ��j�{�b�� ---
            if (currentHoverIndex != -1)
            {
                // ��j Slot
                if (currentHoverIndex < spawnedSlots.Count && spawnedSlots[currentHoverIndex] != null)
                {
                    // [�ק�] �ɤW slotHighlightedScale
                    StartManagedScaleCoroutine(spawnedSlots[currentHoverIndex].transform, slotHighlightedScale);
                }
                // �P�B��j Segment
                if (currentHoverIndex < backgroundSegments.Count && backgroundSegments[currentHoverIndex] != null)
                {
                    // [�ק�] �ɤW segmentHighlightedScale
                    StartManagedScaleCoroutine(backgroundSegments[currentHoverIndex].transform, segmentHighlightedScale);
                }
            }
            previousRenderedHoverIndex = currentHoverIndex; // ��s�O�� (�ηs�W����n)
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
        if (targetTransform != null)
        {
            targetTransform.localScale = targetScale;
            runningScaleCoroutines.Remove(targetTransform);
        }
    }

    /// <summary>
    /// �Ұʤ@�ӷs���Y�� Coroutine�A�ð���� Transform �W�ª� Coroutine�C
    /// </summary>
    private void StartManagedScaleCoroutine(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform == null) return;

        // ����ò����ª� Coroutine (�p�G�s�b)
        if (runningScaleCoroutines.TryGetValue(targetTransform, out Coroutine existingCoroutine))
        {
            if (existingCoroutine != null) // Coroutine �i��w�۵M������������
            {
                // Debug.Log($"Stopping existing ScaleCoroutine on {targetTransform.name}");
                StopCoroutine(existingCoroutine);
            }
            runningScaleCoroutines.Remove(targetTransform);
        }

        // --- [�֤߭ק�] �b�Ұʷs�ʵe *���e*�A�j��]�w�@�� Scale ---
        // �P�_�O�n��j�٬O�Y�p�A�]�w�������_�l Scale (�קK�q�������A�}�l Lerp)
        // Vector3 startScale = (targetScale == slotHighlightedScale || targetScale == segmentHighlightedScale)
        //                    ? (targetTransform.GetComponent<Image>() != null && backgroundSegments.Contains(targetTransform.GetComponent<Image>()) ? segmentNormalScale : slotNormalScale) // If scaling up, start from normal
        //                    : (targetTransform.GetComponent<Image>() != null && backgroundSegments.Contains(targetTransform.GetComponent<Image>()) ? segmentHighlightedScale : slotHighlightedScale); // If scaling down, start from highlighted? No, Coroutine calculates start scale internally. Let's just set the target directly briefly? Or maybe not needed if Stop works well.

        // �Ұʷs�� Coroutine �ðO��
        Coroutine newCoroutine = StartCoroutine(ScaleCoroutine(targetTransform, targetScale));
        runningScaleCoroutines[targetTransform] = newCoroutine;
        // Debug.Log($"Started new ScaleCoroutine on {targetTransform.name} targeting {targetScale}");
    }

    /// <summary>
    /// ������w Transform �W���Y�� Coroutine�C
    /// </summary>
    private void StopManagedScaleCoroutine(Transform targetTransform)
    {
        if (targetTransform != null && runningScaleCoroutines.TryGetValue(targetTransform, out Coroutine coroutine))
        {
            if (coroutine != null)
            {
                // Debug.Log($"Manually stopping ScaleCoroutine on {targetTransform.name}");
                StopCoroutine(coroutine);
            }
            runningScaleCoroutines.Remove(targetTransform);
        }
    }

    /// <summary>
    /// ����Ҧ����b�l�ܪ��Y�� Coroutine�C
    /// </summary>
    private void StopAllManagedScaleCoroutines()
    {
        // Debug.Log($"Stopping all {runningScaleCoroutines.Count} managed ScaleCoroutines...");
        // �ƻs�@�� Keys �ӭ��N�A�]�� StopCoroutine �i��|����Ĳ�o�ק� Dictionary
        List<Transform> keys = new List<Transform>(runningScaleCoroutines.Keys);
        foreach (var transform in keys)
        {
            StopManagedScaleCoroutine(transform);
        }
        runningScaleCoroutines.Clear(); // �T�O�M��
    }

    private System.Collections.IEnumerator EnableHoverDetectionAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // �ϥ� Realtime �קK�� TimeScale �v�T
        allowHoverDetection = true;
        // Debug.Log("Hover detection enabled.");
        allowHoverCoroutine = null;
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