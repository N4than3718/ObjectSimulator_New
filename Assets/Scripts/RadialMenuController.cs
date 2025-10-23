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
    [SerializeField] private GameObject backgroundObject; // �� Background ����
    [SerializeField] private GameObject slotsContainerObject;

    [Header("���L�]�w")]
    [SerializeField] private float radius = 150f;
    [SerializeField] private float inactiveSlotAlpha = 0.5f;
    [SerializeField][Range(0f, 1f)] private float timeScaleWhenOpen = 0.1f;

    [Header("�襤�ĪG")]
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 highlightedScale = new Vector3(1.3f, 1.3f, 1.3f);
    [SerializeField] private float scaleLerpSpeed = 10f;

    private List<GameObject> spawnedSlots = new List<GameObject>();
    private bool isMenuOpen = false;
    private int currentSelectionIndex = -1;
    private int previousSelectionIndex = -1;
    private float originalTimeScale = 1f;
    private bool actionSubscribed = false; // <--- [�s�W] �l�ܭq�\���A

    // --- Input System Setup ---
    private void Awake()
    {
        Debug.Log($"RadialMenuController: Awake() on {this.gameObject.name}. SHOULD RUN AT START.", this.gameObject);

        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();

        SubscribeToAction(); // Input System �q�\

        // [�ק�] �T�O UI �@�}�l�O���ê� (�z�L�l����)
        SetChildrenVisibility(false);

        // ���m���A
        isMenuOpen = false;
        currentSelectionIndex = -1;
        previousSelectionIndex = -1;
        originalTimeScale = 1f;

        // ���Ҥޥ�
        if (backgroundObject == null || slotsContainerObject == null || slotPrefab == null)
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
        currentSelectionIndex = -1; // ���m�ﶵ
        previousSelectionIndex = -1; // ���m�W�@�ӿﶵ
    }

    private void CloseMenu(InputAction.CallbackContext context)
    {
        // [�s�W] �j�O Debug
        Debug.Log("RadialMenuController: CloseMenu ACTION TRIGGERED!");

        if (!isMenuOpen || teamManager == null)
        {
            return;
        }

        Debug.Log($"RadialMenuController: Closing Menu... Selected Index: {currentSelectionIndex}");
        isMenuOpen = false;
        SetChildrenVisibility(false);
        SetCameraInputPause(false);
        Cursor.lockState = CursorLockMode.Locked; // ��w�ƹ�
        Cursor.visible = false;
        Time.timeScale = originalTimeScale; // ��_�ɶ��y�t

        if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count && spawnedSlots[previousSelectionIndex] != null)
        {
            StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale));
        }

        Debug.Log($"CloseMenu: Attempting switch. currentSelectionIndex = {currentSelectionIndex}");
        if (currentSelectionIndex != -1 && currentSelectionIndex < teamManager.team.Length)
        {
            Debug.Log($"CloseMenu: Checking team slot {currentSelectionIndex}. Character is: {(teamManager.team[currentSelectionIndex].character == null ? "NULL" : teamManager.team[currentSelectionIndex].character.name)}");
        }
        else if (currentSelectionIndex != -1)
        {
            Debug.LogWarning($"CloseMenu: currentSelectionIndex {currentSelectionIndex} seems out of bounds for team length {teamManager.team.Length}");
        }

        if (currentSelectionIndex != -1 && currentSelectionIndex < teamManager.team.Length
            && teamManager.team[currentSelectionIndex].character != null)
        {
            teamManager.SwitchToCharacterByIndex(currentSelectionIndex);
        }

        ClearSlots();
    }

    private void SetChildrenVisibility(bool isVisible)
    {
        if (backgroundObject != null) backgroundObject.SetActive(isVisible);
        if (slotsContainerObject != null) slotsContainerObject.SetActive(isVisible);
        // if (centerDotObject != null) centerDotObject.SetActive(isVisible);

        // �p�G�����A�]�T�O�M���i��ݯd�� Slot
        if (!isVisible)
        {
            ClearSlots(); // �T�O�����ɲM��
                          // �T�O���m�Y�񪬺A (�p�G�ʵe�Q���_)
            if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count && spawnedSlots[previousSelectionIndex] != null) { spawnedSlots[previousSelectionIndex].transform.localScale = normalScale; }
            previousSelectionIndex = -1;
            currentSelectionIndex = -1;
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

        if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count)
        {
            // spawnedSlots[previousSelectionIndex].transform.localScale = normalScale; // �����]�w
        }
        previousSelectionIndex = -1; // ���m
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
                var tempColor = Color.white; tempColor.a = inactiveSlotAlpha; iconImage.color = tempColor;
                slotGO.name = $"Slot_{i}_Empty";
            }

            slotGO.transform.localScale = normalScale;
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

        // [�s�W Debug] ��ܭ�l�ƾ�
        // Debug.Log($"Mouse: {mousePos}, Center: {centerPos}, Dir: {direction}, Mag: {direction.magnitude}");

        if (direction.magnitude < deadZoneRadius)
        {
            // �b Dead Zone ���A�������
            currentSelectionIndex = -1;
            // ... (�B�z�Y��) ... // <-- ���Y�^�W�@�ӿﶵ���޿��b�o��
            if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count && spawnedSlots[previousSelectionIndex] != null)
            {
                StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale));
                previousSelectionIndex = -1; // ���m previous
            }
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f; // �ഫ�� 0~360

        // --- �B�J 4: [�ק�] �p�� Index ---
        int teamSize = teamManager.team.Length;
        if (teamSize == 0) return; // ����H�s
        float angleStep = 360f / teamSize;

        // [�ק�] ���׮ե��G�� atan2 ������ (0 �צb�k) �ഫ�� UI ������ (0 �צb�W)
        // atan2 �� 90 �׬O UI �� 0 ��
        // atan2 �� 0 �׬O UI �� 90 ��
        // atan2 �� 270 �׬O UI �� 180 ��
        // atan2 �� 180 �׬O UI �� 270 ��
        // �����G uiAngle = (450 - atan2Angle) % 360
        float uiAngle = (450f - angle) % 360f;

        // [�ק�] �ھ� UI ���׭p����� (0 �b�W��A���ɰw)
        // �����b�Ө��סA�����νu���b�ﶵ����
        float adjustedUiAngle = (uiAngle + angleStep / 2f) % 360f; // <-- �ήե��᪺ uiAngle
        int calculatedIndex = Mathf.FloorToInt(adjustedUiAngle / angleStep); // <-- �o�N�O�ڭ̪� uiIndex

        // [�s�W Debug]
        // Debug.Log($"Atan2 Angle: {angle:F1}, UI Angle: {uiAngle:F1}, Adjusted UI Angle: {adjustedUiAngle:F1}, Calculated Index: {calculatedIndex}");


        // --- �B�J 5: ��s��ܩM�Y�� ---
        previousSelectionIndex = currentSelectionIndex; // �O���ª�

        // �ϥ� calculatedIndex �@���̲ׯ���
        if (calculatedIndex >= 0 && calculatedIndex < teamSize && teamManager.team[calculatedIndex].character != null)
        {
            currentSelectionIndex = calculatedIndex; // <--- �����ϥκ�X�Ӫ� index
        }
        else
        {
            currentSelectionIndex = -1;
        }

        // --- �Y���޿� (�O������) ---
        if (currentSelectionIndex != previousSelectionIndex)
        {
            if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count && spawnedSlots[previousSelectionIndex] != null) { StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale)); }
            if (currentSelectionIndex != -1 && currentSelectionIndex < spawnedSlots.Count && spawnedSlots[currentSelectionIndex] != null) { StartCoroutine(ScaleCoroutine(spawnedSlots[currentSelectionIndex].transform, highlightedScale)); }
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