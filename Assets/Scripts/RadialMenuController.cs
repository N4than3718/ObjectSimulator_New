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

        if (isMenuOpen || teamManager == null)
        {
            return;
        }

        Debug.Log("RadialMenuController: Opening Menu...");
        isMenuOpen = true;
        SetChildrenVisibility(true);

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

        Cursor.lockState = CursorLockMode.Locked; // ��w�ƹ�
        Cursor.visible = false;
        Time.timeScale = originalTimeScale; // ��_�ɶ��y�t

        if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count && spawnedSlots[previousSelectionIndex] != null)
        {
            StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale));
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

    // ForceCloseMenu �O������...

    // PopulateSlots �O������...

    // ClearSlots �O������...

    // Update �O������ (�]�t�Y���޿�)...

    // SetScale �O������...

    // ScaleCoroutine �O������...


    // --- [�s�W] ��� Debug in Update (�Ʈ�) ---
    /*
    void Update()
    {
        // �u��b isMenuOpen = false ���ˬd�}��
        if (!isMenuOpen && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            Debug.LogWarning("MANUAL DEBUG: Tab was pressed this frame. Forcing OpenMenu...");
            // ��ʼ��� Input System �� context (���M�o�̥Τ���)
            OpenMenu(new InputAction.CallbackContext());
        }
        // �u��b isMenuOpen = true ���ˬd����
        else if (isMenuOpen && Keyboard.current != null && Keyboard.current.tabKey.wasReleasedThisFrame)
        {
             Debug.LogWarning("MANUAL DEBUG: Tab was released this frame. Forcing CloseMenu...");
             CloseMenu(new InputAction.CallbackContext());
        }

        // --- �쥻�� Update ����޿� ---
        if (!isMenuOpen) return;
        // ... (�A�쥻�p�⨤�שM�Y���޿�) ...
    }
    */


    private void ForceCloseMenu() // ��������� (�Ҧp OnDisable)
    {
        isMenuOpen = false;
        SetChildrenVisibility(false);
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
            GameObject slotGO = Instantiate(slotPrefab, slotsContainer);
            spawnedSlots.Add(slotGO);

            // �p���m (���y���ઽ���y��)
            float angleRad = (90f - (i * angleStep)) * Mathf.Deg2Rad; // 90�׶}�l (����), ���ɰw
            float x = radius * Mathf.Cos(angleRad);
            float y = radius * Mathf.Sin(angleRad);
            slotGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            // ��s Slot ���e (�Ҧp�ϥܡB�z����)
            Image slotImage = slotGO.GetComponentInChildren<Image>(); // ���] Prefab �̦� Image
            TeamUnit unit = teamManager.team[i];

            if (unit.character != null && slotImage != null)
            {
                // TODO: �ھ� unit.character �]�m�ϥ� (�ݭn�A���ϥܸ귽)
                // slotImage.sprite = GetIconForCharacter(unit.character);
                slotImage.color = Color.white; // ���`���
                slotGO.name = $"Slot_{i}_{unit.character.name}";
            }
            else if (slotImage != null)
            {
                // �ťթεL�� Slot
                slotImage.sprite = null; // �M�Źϥ�
                var tempColor = Color.white;
                tempColor.a = inactiveSlotAlpha; // �]���b�z��
                slotImage.color = tempColor;
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

        // 1. ���o�ƹ��۹襤�ߪ��V�q
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 centerPos = this.GetComponent<RectTransform>().position; // ���]�}�����b RadialMenu (��) �W
        Vector2 direction = mousePos - centerPos;

        // 2. �p�G�ƹ��Ӿa�񤤤ߡA���i����
        float deadZoneRadius = radius * 0.2f; // �Ҧp�A���� 20% �ϰ줣��
        if (direction.magnitude < deadZoneRadius)
        {
            currentSelectionIndex = -1;
            return;
        }

        // 3. �p�⨤�� (atan2 ��^����, �ର����)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // �⨤���ഫ�� 0~360 �d�� (�q�k��}�l�A�f�ɰw)
        if (angle < 0) angle += 360f;

        // 4. �p������� Index
        int teamSize = teamManager.team.Length;
        float angleStep = 360f / teamSize;
        // �����b�Ө��סA�����νu���b�ﶵ����
        float adjustedAngle = (angle + angleStep / 2f) % 360f;
        int selectedIndex = Mathf.FloorToInt(adjustedAngle / angleStep);

        // �ѩ�ڭ� UI �O�q�W�� (90��) ���ɰw�ƪ��AInput System ���׬O�q�k�� (0��) �f�ɰw�⪺
        // �ݭn���@�ӬM�g�ഫ (�o�����i��ݭn�ھڧA�� UI ��ڱƦC�L��)
        // ���] teamSize=8, angleStep=45
        // Angle 0-45 -> Index 0 (�k)
        // Angle 45-90 -> Index 1 (�k�W) ...
        // Angle 315-360 -> Index 7 (�k�U)
        // �M�g�� UI (�q�W�趶�ɰw 0-7)
        // �o�䪺�M�g�޿観�I tricky�A�����]�@��²�檺�M�g�A�A�i��ݭn�ھڵ�ı�վ�
        int uiIndex = (teamSize - selectedIndex + (teamSize / 4)) % teamSize; // ���լM�g��H������0�����ɰw����

        previousSelectionIndex = currentSelectionIndex; // ���O��o�@�V�}�l�ɿ諸�O��

        if (uiIndex >= 0 && uiIndex < teamSize && teamManager.team[uiIndex].character != null) // �����O���Ī�����
        {
            currentSelectionIndex = uiIndex;
            // if (selectorHighlight != null && spawnedSlots.Count > uiIndex) // <-- [�R��]
            // {                                                            // <-- [�R��]
            //     selectorHighlight.enabled = true;                      // <-- [�R��]
            //     selectorHighlight.rectTransform.position = spawnedSlots[uiIndex].GetComponent<RectTransform>().position; // <-- [�R��]
            // }                                                            // <-- [�R��]
        }
        else // �ƹ��b�����εL�� Slot �W
        {
            currentSelectionIndex = -1;
            // if (selectorHighlight != null) selectorHighlight.enabled = false; // <-- [�R��]
        }

        // --- [�s�W] �B�z�Y���޿� ---
        // �p�G��ܧ��ܤF
        if (currentSelectionIndex != previousSelectionIndex)
        {
            // ��W�@�ӿ襤���Y�^�h (�p�G������)
            if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count)
            {
                // SetScale(spawnedSlots[previousSelectionIndex].transform, normalScale); // �����]�w
                StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale)); // �ΰʵe
            }
            // ��{�b�襤����j (�p�G������)
            if (currentSelectionIndex != -1 && currentSelectionIndex < spawnedSlots.Count)
            {
                // SetScale(spawnedSlots[currentSelectionIndex].transform, highlightedScale); // �����]�w
                StartCoroutine(ScaleCoroutine(spawnedSlots[currentSelectionIndex].transform, highlightedScale)); // �ΰʵe
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
}