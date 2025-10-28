using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections; // <--- [新增] 為了 Coroutine

public class RadialMenuController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private TeamManager teamManager;
    [SerializeField] private InputActionReference openMenuActionRef; // 把 Input Action 拖進來

    [Header("UI 元素")]
    [SerializeField] private Transform slotsContainer; // 所有 Slot UI 的父物件
    [SerializeField] private GameObject slotPrefab; // 代表一個選項的 UI Prefab

    [Header("子物件引用 (代替 MenuRoot)")]
    [SerializeField] private GameObject slotsContainerObject;

    [Header("背景區塊 (Segments)")] // <-- [新增]
    [SerializeField] private GameObject backgroundSegmentsContainer; // <-- [新增] 把 BackgroundSegments 父物件拖到這裡
    private List<Image> backgroundSegments = new List<Image>();

    [Header("輪盤設定")]
    [SerializeField] private float radius = 150f;
    [SerializeField] private float inactiveSlotAlpha = 0.5f;
    [SerializeField][Range(0f, 1f)] private float timeScaleWhenOpen = 0.1f;

    [Header("選中效果")]
    [SerializeField] private Vector3 slotNormalScale = Vector3.one;       // <-- [修改] 改名，更清晰
    [SerializeField] private Vector3 slotHighlightedScale = new Vector3(1.3f, 1.3f, 1.3f); // <-- [修改] 改名
    [SerializeField] private Vector3 segmentNormalScale = Vector3.one;    // <-- [新增] Segment 正常大小
    [SerializeField] private Vector3 segmentHighlightedScale = new Vector3(1.1f, 1.1f, 1.1f); // <-- [新增] Segment 放大倍率
    [SerializeField] private float scaleLerpSpeed = 10f;

    private List<GameObject> spawnedSlots = new List<GameObject>();
    private bool isMenuOpen = false;
    private bool allowHoverDetection = false; // <--- [新增] 控制是否允許 Update 計算 Hover
    private Coroutine allowHoverCoroutine = null; // <--- [新增] 追蹤延遲 Coroutine
    private int currentHoverIndex = -1;       // <--- [新增] 當前滑鼠指向的 Index (-1 = 無效)
    private int lastValidHoverIndex = -1;     // <--- [新增] 最後一個滑鼠指向過的 *有效* Index
    private int previousRenderedHoverIndex = -1;
    private float originalTimeScale = 1f;
    private bool actionSubscribed = false; // <--- [新增] 追蹤訂閱狀態

    private Dictionary<Transform, Coroutine> runningScaleCoroutines = new Dictionary<Transform, Coroutine>();

    // --- Input System Setup ---
    private void Awake()
    {
        Debug.Log($"RadialMenuController: Awake() on {this.gameObject.name}. SHOULD RUN AT START.", this.gameObject);

        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();

        SubscribeToAction(); // Input System 訂閱

        PopulateBackgroundSegments();

        // [修改] 確保 UI 一開始是隱藏的 (透過子物件)
        SetChildrenVisibility(false);

        // 重置狀態
        isMenuOpen = false;
        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;
        originalTimeScale = 1f;

        // 驗證引用
        if (backgroundSegmentsContainer == null || slotsContainerObject == null || slotPrefab == null)
        {
            Debug.LogError("RadialMenuController: 子物件引用 (Background, SlotsContainer, SlotPrefab) 未在 Inspector 中設定!", this.gameObject);
        }
    }

    // [新增] 獨立的訂閱方法
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
                openMenuActionRef.action.Enable(); // 先啟用 Action
                openMenuActionRef.action.started += OpenMenu; // 按下 Tab (Hold 開始)
                openMenuActionRef.action.canceled += CloseMenu; // 放開 Tab (Hold 結束)
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
            // 假設 Segment Image 直接掛在 Container 底下
            backgroundSegmentsContainer.GetComponentsInChildren<Image>(true, backgroundSegments); // true 包含 Inactive
            Debug.Log($"Found {backgroundSegments.Count} background segment images.");
            // 你可能需要根據名稱排序，確保索引 0 對應 Segment_0
            backgroundSegments.Sort((a, b) => a.name.CompareTo(b.name));
        }
        else
        {
            Debug.LogError("Background Segments Container 未設定!");
        }
    }

    // [修改] OnEnable 現在只做 Debug
    private void OnEnable()
    {
        Debug.Log($"RadialMenuController: OnEnable() called on {this.gameObject.name}. SHOULD NOT HAPPEN IF STARTING INACTIVE.", this.gameObject);
        // 如果 Awake 裡的訂閱因為某些原因失敗了，這裡可以再試一次 (備案)
        // SubscribeToAction();
    }

    // [修改] OnDisable 現在只做 Debug
    private void OnDisable()
    {
        Debug.Log($"RadialMenuController: OnDisable() called on {this.gameObject.name}.", this.gameObject);
        // If the menu was forced closed by disabling the object, ensure state resets
        // if (isMenuOpen) ForceCloseMenu(); // 這可能導致重複呼叫 ForceCloseMenu
    }

    // [修改] 把 Input System 的取消訂閱邏輯移到 OnDestroy
    private void OnDestroy()
    {
        Debug.Log($"RadialMenuController: OnDestroy() called on {this.gameObject.name}. Unsubscribing.", this.gameObject);
        if (openMenuActionRef == null || openMenuActionRef.action == null || !actionSubscribed) return;

        try
        {
            openMenuActionRef.action.started -= OpenMenu;
            openMenuActionRef.action.canceled -= CloseMenu;
            // 考慮是否 Disable Action，取決於你的 Action 管理方式
            // openMenuActionRef.action.Disable();
            actionSubscribed = false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RadialMenuController: Error unsubscribing from input action: {e.Message}", this.gameObject);
        }

        // 確保 TimeScale 恢復正常
        if (Time.timeScale != 1f && originalTimeScale != 0) // 避免 Time.timeScale 被卡住
        {
            Debug.LogWarning($"RadialMenuController: Resetting TimeScale from {Time.timeScale} to {originalTimeScale} in OnDestroy.");
            Time.timeScale = originalTimeScale > 0 ? originalTimeScale : 1f;
        }
    }
    

    // --- Menu Logic ---
    private void OpenMenu(InputAction.CallbackContext context)
    {
        StopAllManagedScaleCoroutines(); // 清理任何可能殘留的舊動畫
        Debug.Log("RadialMenuController: OpenMenu ACTION TRIGGERED!");
        currentHoverIndex = -1;
        lastValidHoverIndex = -1;
        previousRenderedHoverIndex = -1;
        allowHoverDetection = false; // <-- [新增] 先禁止偵測

        if (teamManager != null && teamManager.CurrentGameState == TeamManager.GameState.Spectator)
        {
            Debug.Log("OpenMenu prevented: Currently in Spectator mode.");
            return; // 在觀察者模式下，不給開輪盤
        }

        if (isMenuOpen || teamManager == null)
        {
            return;
        }

        Debug.Log("RadialMenuController: Opening Menu...");
        isMenuOpen = true;
        SetChildrenVisibility(true);
        SetCameraInputPause(true);
        Cursor.lockState = CursorLockMode.None; // 解鎖滑鼠
        Cursor.visible = true;

        originalTimeScale = Time.timeScale; // 儲存目前時間流速
        Time.timeScale = timeScaleWhenOpen; // 減慢時間

        PopulateSlots(); // 根據隊伍動態生成選項

        // 重設 Slots
        foreach (var slotGO in spawnedSlots)
        {
            if (slotGO != null)
            {
                // 先停掉可能剛被 PopulateSlots 意外啟動的 Coroutine (雖然不太可能)
                // StopManagedScaleCoroutine(slotGO.transform); // 或許不需要？
                SetScale(slotGO.transform, slotNormalScale); // <<<--- 強制設回 Normal
            }
        }
        // 重設 Segments (再次確保)
        foreach (var segImage in backgroundSegments)
        {
            if (segImage != null)
            {
                // StopManagedScaleCoroutine(segImage.transform); // 或許不需要？
                SetScale(segImage.transform, segmentNormalScale); // <<<--- 強制設回 Normal
            }
        }

        if (allowHoverCoroutine != null) StopCoroutine(allowHoverCoroutine); // 以防萬一
        allowHoverCoroutine = StartCoroutine(EnableHoverDetectionAfterDelay(0.1f)); // 例如延遲 0.1 秒
    }

    private void CloseMenu(InputAction.CallbackContext context)
    {
        int finalIndex = -1;
        Vector2 finalPointerPos = Pointer.current.position.ReadValue();
        Vector2 centerPosNow = this.GetComponent<RectTransform>().position; // 重新獲取中心點
        Vector2 finalDirection = finalPointerPos - centerPosNow;
        float deadZone = radius * 0.2f;

        if (finalDirection.magnitude >= deadZone)
        {
            float finalAngle = Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg;
            if (finalAngle < 0) finalAngle += 360f;
            int teamSizeNow = teamManager.team.Length; // 重新獲取隊伍大小
            if (teamSizeNow > 0)
            {
                float angleStepNow = 360f / teamSizeNow;
                float finalUiAngle = (450f - finalAngle) % 360f;
                float finalAdjustedUiAngle = (finalUiAngle + angleStepNow / 2f) % 360f;
                finalIndex = Mathf.FloorToInt(finalAdjustedUiAngle / angleStepNow);

                // 最後驗證
                if (finalIndex < 0 || finalIndex >= teamSizeNow || teamManager.team[finalIndex].character == null)
                {
                    finalIndex = -1; // 如果算出來是無效 Slot，還是設為 -1
                }
            }
        }
        // 把這個即時算出來的值，作為我們的快照
        int indexToSwitchTo = finalIndex;

        if (!isMenuOpen || teamManager == null)
        {
            return;
        }

        Debug.Log($"RadialMenuController: Closing Menu... Selected Index: {currentHoverIndex}");
        isMenuOpen = false;
        SetChildrenVisibility(false);
        SetCameraInputPause(false);
        Cursor.lockState = CursorLockMode.Locked; // 鎖定滑鼠
        Cursor.visible = false;

        if (allowHoverCoroutine != null)
        {
            StopCoroutine(allowHoverCoroutine);
            allowHoverCoroutine = null;
        }
        allowHoverDetection = false; // 重置

        Debug.Log($"CloseMenu: Attempting switch using lastValidHoverIndex = {lastValidHoverIndex}");

        // 1. 立刻停止所有正在執行的縮放動畫
        StopAllManagedScaleCoroutines();

        // 2. 直接把最後高亮的元素（如果有）縮回正常大小
        int indexToScaleDown = (currentHoverIndex != -1) ? currentHoverIndex : previousRenderedHoverIndex;
        if (indexToScaleDown != -1)
        {
            // 直接設定 Slot 大小
            if (indexToScaleDown < spawnedSlots.Count && spawnedSlots[indexToScaleDown] != null)
            {
                SetScale(spawnedSlots[indexToScaleDown].transform, slotNormalScale); // 使用直接設定
            }
            // 直接設定 Segment 大小
            if (indexToScaleDown < backgroundSegments.Count && backgroundSegments[indexToScaleDown] != null)
            {
                SetScale(backgroundSegments[indexToScaleDown].transform, segmentNormalScale); // 使用直接設定
            }
        }

        // 根據鎖定的 Index 執行切換
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

        // 如果關閉，也確保清除可能殘留的 Slot
        if (!isVisible)
        {
            ClearSlots(); // 確保關閉時清除

            // 確保重置縮放狀態 (如果動畫被打斷)
            if (previousRenderedHoverIndex != -1 && previousRenderedHoverIndex < spawnedSlots.Count && spawnedSlots[previousRenderedHoverIndex] != null) { spawnedSlots[previousRenderedHoverIndex].transform.localScale = slotNormalScale; }
            currentHoverIndex = -1;
            lastValidHoverIndex = -1;
            previousRenderedHoverIndex = -1;
        }
    }

    private void ForceCloseMenu() // 緊急關閉用 (例如 OnDisable)
    {
        if (allowHoverCoroutine != null)
        {
            StopCoroutine(allowHoverCoroutine);
            allowHoverCoroutine = null;
        }
        allowHoverDetection = false; // 重置

        StopAllManagedScaleCoroutines();
        isMenuOpen = false;
        SetChildrenVisibility(false);
        SetCameraInputPause(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = originalTimeScale > 0 ? originalTimeScale : 1f; // 避免 TimeScale 變 0

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

        foreach (var segImage in backgroundSegments) // <-- 改用 segImage 變數名
        {
            if (segImage != null)
            {
                //Debug.Log($"PopulateSlots: Resetting Segment '{segImage.name}' to scale {segmentNormalScale}");
                SetScale(segImage.transform, segmentNormalScale); // 直接設定
            }
        }

        int teamSize = teamManager.team.Length; // 用 MaxTeamSize 作為總 Slot 數
        float angleStep = 360f / teamSize;

        for (int i = 0; i < teamSize; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsContainerObject.transform);
            spawnedSlots.Add(slotGO);

            // 計算位置 (極座標轉直角座標)
            float angleRad = (90f - (i * angleStep)) * Mathf.Deg2Rad; // 90度開始 (頂部), 順時針
            float x = radius * Mathf.Cos(angleRad);
            float y = radius * Mathf.Sin(angleRad);
            slotGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            Image iconImage = slotGO.transform.Find("Icon")?.GetComponent<Image>(); // <-- [修改] 精確找到名叫 Icon 的子物件上的 Image

            if (iconImage == null) // <-- [新增] 檢查是否找到 Icon Image
            {
                Debug.LogError($"Slot Prefab ({slotPrefab.name}) 缺少名為 'Icon' 的子物件或其上的 Image 元件!", slotGO);
                // ... (處理錯誤，例如 return 或 continue)
            }

            TeamUnit unit = teamManager.team[i];

            if (unit.character != null /* && slotImage != null */ ) // <-- iconImage 的檢查移到下面
            {
                if (iconImage != null && unit.character.radialMenuIcon != null) // <-- [修改] 改用 iconImage
                {
                    iconImage.sprite = unit.character.radialMenuIcon; // <--- [修改] 設定給 iconImage
                    iconImage.color = Color.white;
                }
                else if (iconImage != null) // 有 Image 但沒 Sprite
                {
                    Debug.LogWarning($"物件 {unit.character.name} 沒有指定 RadialMenuIcon!", unit.character.gameObject);
                    iconImage.sprite = null;
                    var tempColor = Color.gray; tempColor.a = inactiveSlotAlpha; iconImage.color = tempColor;
                }
                slotGO.name = $"Slot_{i}_{unit.character.name}";
            }
            else if (iconImage != null) // 空白 Slot
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
        for (int i = spawnedSlots.Count - 1; i >= 0; i--) // 反向迭代比較安全
        {
            GameObject slotGO = spawnedSlots[i];
            if (slotGO != null)
            {
                Transform slotTransform = slotGO.transform;
                // 1. 停止 Slot 上的動畫
                StopManagedScaleCoroutine(slotTransform);
                // 2. 強制設回 Normal Scale
                SetScale(slotTransform, slotNormalScale);

                // 3. 停止對應 Segment 上的動畫 (如果存在)
                if (i >= 0 && i < backgroundSegments.Count && backgroundSegments[i] != null)
                {
                    Transform segTransform = backgroundSegments[i].transform;
                    StopManagedScaleCoroutine(segTransform);
                    SetScale(segTransform, segmentNormalScale);
                }

                // 4. 最後才 Destroy
                // Debug.Log($"Destroying slot {i}");
                Destroy(slotGO);
            }
        }
        spawnedSlots.Clear();
        runningScaleCoroutines.Clear(); // <-- [新增] 確保字典也被清空
    }

    // --- Selection Logic (在 Update 中執行) ---
    void Update()
    {
        if (!isMenuOpen || !allowHoverDetection) return;

        // --- 步驟 1-3 不變: 取得 direction, 檢查 dead zone, 計算 atan2 angle ---
        Vector2 pointerPos = Pointer.current.position.ReadValue();
        Vector2 centerPos = this.GetComponent<RectTransform>().position;
        Vector2 direction = pointerPos - centerPos; // <-- [修改]
        float deadZoneRadius = radius * 0.2f;

        int calculatedIndex = -1; // 預設無效

        int previousRenderedHoverIndexForScale = previousRenderedHoverIndex;

        if (direction.magnitude >= deadZoneRadius) // 只有在 Dead Zone 外才計算
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

        // --- [修改] 更新 Hover 狀態 ---
        previousRenderedHoverIndex = currentHoverIndex; // 記住上一幀是誰被放大

        // 檢查 calculatedIndex 是否指向有效隊友
        if (calculatedIndex >= 0 && calculatedIndex < teamManager.team.Length && teamManager.team[calculatedIndex].character != null)
        {
            currentHoverIndex = calculatedIndex; // 更新當前指向
            lastValidHoverIndex = calculatedIndex; // **鎖定**最後一個有效的指向
            // Debug.Log($"Update: Valid Hover {currentHoverIndex}. Last Valid {lastValidHoverIndex}");
        }
        else
        {
            currentHoverIndex = -1; // 指向無效區域
            Debug.Log($"Update: currentHoverIndex set to -1. lastValidHoverIndex remains {lastValidHoverIndex}"); // 檢查 Log
        }

        if (currentHoverIndex != previousRenderedHoverIndexForScale) // 縮放 Index 改變
        {
            // --- 縮回上一個 ---
            if (previousRenderedHoverIndexForScale != -1)
            {
                // 縮回 Slot
                if (previousRenderedHoverIndexForScale < spawnedSlots.Count && spawnedSlots[previousRenderedHoverIndexForScale] != null)
                {
                    // [修改] 補上 slotNormalScale
                    StartManagedScaleCoroutine(spawnedSlots[previousRenderedHoverIndexForScale].transform, slotNormalScale);
                }
                // 同步縮回 Segment
                if (previousRenderedHoverIndexForScale < backgroundSegments.Count && backgroundSegments[previousRenderedHoverIndexForScale] != null)
                {
                    // [修改] 補上 segmentNormalScale
                    StartManagedScaleCoroutine(backgroundSegments[previousRenderedHoverIndexForScale].transform, segmentNormalScale);
                }
            }

            // --- 放大現在的 ---
            if (currentHoverIndex != -1)
            {
                // 放大 Slot
                if (currentHoverIndex < spawnedSlots.Count && spawnedSlots[currentHoverIndex] != null)
                {
                    // [修改] 補上 slotHighlightedScale
                    StartManagedScaleCoroutine(spawnedSlots[currentHoverIndex].transform, slotHighlightedScale);
                }
                // 同步放大 Segment
                if (currentHoverIndex < backgroundSegments.Count && backgroundSegments[currentHoverIndex] != null)
                {
                    // [修改] 補上 segmentHighlightedScale
                    StartManagedScaleCoroutine(backgroundSegments[currentHoverIndex].transform, segmentHighlightedScale);
                }
            }
            previousRenderedHoverIndex = currentHoverIndex; // 更新記錄 (用新名比較好)
        }
    }

    private void SetScale(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform != null)
        {
            targetTransform.localScale = targetScale;
        }
    }

    // --- [新增] 輔助方法：用 Coroutine 做縮放動畫 (可選) ---
    private System.Collections.IEnumerator ScaleCoroutine(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform == null) yield break;

        Vector3 startScale = targetTransform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            // 如果在動畫過程中目標 Transform 被銷毀 (例如關閉菜單 ClearSlots)，立刻停止
            if (targetTransform == null) yield break;

            t += Time.unscaledDeltaTime * scaleLerpSpeed; // 使用 unscaledDeltaTime 避免受 TimeScale 影響
            targetTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null; // 等待下一幀
        }
        // 確保最終 scale 是精確的
        if (targetTransform != null)
        {
            targetTransform.localScale = targetScale;
            runningScaleCoroutines.Remove(targetTransform);
        }
    }

    /// <summary>
    /// 啟動一個新的縮放 Coroutine，並停止該 Transform 上舊的 Coroutine。
    /// </summary>
    private void StartManagedScaleCoroutine(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform == null) return;

        // 停止並移除舊的 Coroutine (如果存在)
        if (runningScaleCoroutines.TryGetValue(targetTransform, out Coroutine existingCoroutine))
        {
            if (existingCoroutine != null) // Coroutine 可能已自然結束但未移除
            {
                // Debug.Log($"Stopping existing ScaleCoroutine on {targetTransform.name}");
                StopCoroutine(existingCoroutine);
            }
            runningScaleCoroutines.Remove(targetTransform);
        }

        // --- [核心修改] 在啟動新動畫 *之前*，強制設定一次 Scale ---
        // 判斷是要放大還是縮小，設定對應的起始 Scale (避免從中間狀態開始 Lerp)
        // Vector3 startScale = (targetScale == slotHighlightedScale || targetScale == segmentHighlightedScale)
        //                    ? (targetTransform.GetComponent<Image>() != null && backgroundSegments.Contains(targetTransform.GetComponent<Image>()) ? segmentNormalScale : slotNormalScale) // If scaling up, start from normal
        //                    : (targetTransform.GetComponent<Image>() != null && backgroundSegments.Contains(targetTransform.GetComponent<Image>()) ? segmentHighlightedScale : slotHighlightedScale); // If scaling down, start from highlighted? No, Coroutine calculates start scale internally. Let's just set the target directly briefly? Or maybe not needed if Stop works well.

        // 啟動新的 Coroutine 並記錄
        Coroutine newCoroutine = StartCoroutine(ScaleCoroutine(targetTransform, targetScale));
        runningScaleCoroutines[targetTransform] = newCoroutine;
        // Debug.Log($"Started new ScaleCoroutine on {targetTransform.name} targeting {targetScale}");
    }

    /// <summary>
    /// 停止指定 Transform 上的縮放 Coroutine。
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
    /// 停止所有正在追蹤的縮放 Coroutine。
    /// </summary>
    private void StopAllManagedScaleCoroutines()
    {
        // Debug.Log($"Stopping all {runningScaleCoroutines.Count} managed ScaleCoroutines...");
        // 複製一份 Keys 來迭代，因為 StopCoroutine 可能會間接觸發修改 Dictionary
        List<Transform> keys = new List<Transform>(runningScaleCoroutines.Keys);
        foreach (var transform in keys)
        {
            StopManagedScaleCoroutine(transform);
        }
        runningScaleCoroutines.Clear(); // 確保清空
    }

    private System.Collections.IEnumerator EnableHoverDetectionAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // 使用 Realtime 避免受 TimeScale 影響
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