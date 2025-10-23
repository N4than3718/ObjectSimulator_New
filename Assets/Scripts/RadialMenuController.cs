using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // 需要引用 UI 命名空間
using System.Collections.Generic;

public class RadialMenuController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private TeamManager teamManager;
    [SerializeField] private InputActionReference openMenuActionRef; // 把 Input Action 拖進來

    [Header("UI 元素")]
    [SerializeField] private GameObject menuRoot; // 就是掛載這個腳本的物件
    [SerializeField] private Transform slotsContainer; // 所有 Slot UI 的父物件
    [SerializeField] private GameObject slotPrefab; // 代表一個選項的 UI Prefab (例如一個帶有 Image 的 GameObject)

    [Header("輪盤設定")]
    [SerializeField] private float radius = 150f; // 輪盤半徑
    [SerializeField] private float inactiveSlotAlpha = 0.5f; // 空白選項的透明度
    [SerializeField] [Range(0f, 1f)] private float timeScaleWhenOpen = 0.1f; // 開啟時的時間流速 (0=暫停, 1=正常)

    [Header("選中效果")] // <--- [新增]
    [SerializeField] private Vector3 normalScale = Vector3.one; // 正常大小 (1, 1, 1)
    [SerializeField] private Vector3 highlightedScale = new Vector3(1.3f, 1.3f, 1.3f); // 放大倍率
    [SerializeField] private float scaleLerpSpeed = 10f; // 縮放動畫速度 (可選)

    private List<GameObject> spawnedSlots = new List<GameObject>();
    private bool isMenuOpen = false;
    private int currentSelectionIndex = -1;
    private int previousSelectionIndex = -1;
    private float originalTimeScale = 1f;

    // --- Input System Setup ---
    private void Awake()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>(); // 自動找 (備案)
        // 確保一開始是關閉的
        if (menuRoot != null) menuRoot.SetActive(false);
        else Debug.LogError("Menu Root 未設定!");
    }

    private void OnEnable()
    {
        if (openMenuActionRef == null || openMenuActionRef.action == null) return;
        openMenuActionRef.action.Enable();
        openMenuActionRef.action.started += OpenMenu; // 按下 Tab (Hold 開始)
        openMenuActionRef.action.canceled += CloseMenu; // 放開 Tab (Hold 結束)
    }

    private void OnDisable()
    {
        if (openMenuActionRef == null || openMenuActionRef.action == null) return;
        openMenuActionRef.action.started -= OpenMenu;
        openMenuActionRef.action.canceled -= CloseMenu;
        openMenuActionRef.action.Disable();

        // 確保關閉時恢復正常狀態 (以防萬一)
        if (isMenuOpen) ForceCloseMenu();
    }

    // --- Menu Logic ---
    private void OpenMenu(InputAction.CallbackContext context)
    {
        if (isMenuOpen || teamManager == null) return;

        Debug.Log("Radial Menu: Opening...");
        isMenuOpen = true;
        menuRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None; // 解鎖滑鼠
        Cursor.visible = true;

        originalTimeScale = Time.timeScale; // 儲存目前時間流速
        Time.timeScale = timeScaleWhenOpen; // 減慢時間

        PopulateSlots(); // 根據隊伍動態生成選項
        currentSelectionIndex = -1; // 重置選項
        previousSelectionIndex = -1; // <--- [新增] 重置上一個選項
    }

    private void CloseMenu(InputAction.CallbackContext context)
    {
        if (!isMenuOpen || teamManager == null) return;

        Debug.Log($"Radial Menu: Closing... Selected Index: {currentSelectionIndex}");
        isMenuOpen = false;
        menuRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked; // 鎖定滑鼠
        Cursor.visible = false;
        Time.timeScale = originalTimeScale; // 恢復時間流速

        if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count)
        {
            // SetScale(spawnedSlots[previousSelectionIndex].transform, normalScale); // 直接設定
            StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale)); // 或用動畫
        }

        // --- 執行切換 ---
        if (currentSelectionIndex != -1 && currentSelectionIndex < teamManager.team.Length
            && teamManager.team[currentSelectionIndex].character != null) // 確保選中的是有效隊友
        {
            teamManager.SwitchToCharacterByIndex(currentSelectionIndex); // <--- 呼叫 TeamManager 的新方法
        }

        ClearSlots(); // 清理舊選項
    }

    private void ForceCloseMenu() // 緊急關閉用 (例如 OnDisable)
    {
        isMenuOpen = false;
        if(menuRoot != null) menuRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = originalTimeScale > 0 ? originalTimeScale : 1f; // 避免 TimeScale 變 0

        if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count)
        {
            // spawnedSlots[previousSelectionIndex].transform.localScale = normalScale; // 直接設定
        }
        previousSelectionIndex = -1; // 重置
        ClearSlots();
    }


    // --- UI Population ---
    private void PopulateSlots()
    {
        ClearSlots();
        if (slotPrefab == null || slotsContainer == null || teamManager == null) return;

        int teamSize = teamManager.team.Length; // 用 MaxTeamSize 作為總 Slot 數
        float angleStep = 360f / teamSize;

        for (int i = 0; i < teamSize; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsContainer);
            spawnedSlots.Add(slotGO);

            // 計算位置 (極座標轉直角座標)
            float angleRad = (90f - (i * angleStep)) * Mathf.Deg2Rad; // 90度開始 (頂部), 順時針
            float x = radius * Mathf.Cos(angleRad);
            float y = radius * Mathf.Sin(angleRad);
            slotGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            // 更新 Slot 內容 (例如圖示、透明度)
            Image slotImage = slotGO.GetComponentInChildren<Image>(); // 假設 Prefab 裡有 Image
            TeamUnit unit = teamManager.team[i];

            if (unit.character != null && slotImage != null)
            {
                // TODO: 根據 unit.character 設置圖示 (需要你有圖示資源)
                // slotImage.sprite = GetIconForCharacter(unit.character);
                slotImage.color = Color.white; // 正常顯示
                slotGO.name = $"Slot_{i}_{unit.character.name}";
            }
            else if (slotImage != null)
            {
                // 空白或無效 Slot
                slotImage.sprite = null; // 清空圖示
                var tempColor = Color.white;
                tempColor.a = inactiveSlotAlpha; // 設為半透明
                slotImage.color = tempColor;
                slotGO.name = $"Slot_{i}_Empty";
            }
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

    // --- Selection Logic (在 Update 中執行) ---
    void Update()
    {
        if (!isMenuOpen) return;

        // 1. 取得滑鼠相對中心的向量
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 centerPos = menuRoot.GetComponent<RectTransform>().position; // UI 中心點的世界座標
        Vector2 direction = mousePos - centerPos;

        // 2. 如果滑鼠太靠近中心，不進行選擇
        float deadZoneRadius = radius * 0.2f; // 例如，中心 20% 區域不選
        if (direction.magnitude < deadZoneRadius)
        {
            currentSelectionIndex = -1;
            return;
        }

        // 3. 計算角度 (atan2 返回弧度, 轉為角度)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // 把角度轉換到 0~360 範圍 (從右邊開始，逆時針)
        if (angle < 0) angle += 360f;

        // 4. 計算對應的 Index
        int teamSize = teamManager.team.Length;
        float angleStep = 360f / teamSize;
        // 偏移半個角度，讓分割線落在選項之間
        float adjustedAngle = (angle + angleStep / 2f) % 360f;
        int selectedIndex = Mathf.FloorToInt(adjustedAngle / angleStep);

        // 由於我們 UI 是從上方 (90度) 順時針排的，Input System 角度是從右方 (0度) 逆時針算的
        // 需要做一個映射轉換 (這部分可能需要根據你的 UI 實際排列微調)
        // 假設 teamSize=8, angleStep=45
        // Angle 0-45 -> Index 0 (右)
        // Angle 45-90 -> Index 1 (右上) ...
        // Angle 315-360 -> Index 7 (右下)
        // 映射到 UI (從上方順時針 0-7)
        // 這邊的映射邏輯有點 tricky，先假設一個簡單的映射，你可能需要根據視覺調整
        int uiIndex = (teamSize - selectedIndex + (teamSize / 4)) % teamSize; // 嘗試映射到以頂部為0的順時針索引

        previousSelectionIndex = currentSelectionIndex; // 先記住這一幀開始時選的是誰

        if (uiIndex >= 0 && uiIndex < teamSize && teamManager.team[uiIndex].character != null) // 必須是有效的隊友
        {
            currentSelectionIndex = uiIndex;
            // if (selectorHighlight != null && spawnedSlots.Count > uiIndex) // <-- [刪除]
            // {                                                            // <-- [刪除]
            //     selectorHighlight.enabled = true;                      // <-- [刪除]
            //     selectorHighlight.rectTransform.position = spawnedSlots[uiIndex].GetComponent<RectTransform>().position; // <-- [刪除]
            // }                                                            // <-- [刪除]
        }
        else // 滑鼠在中間或無效 Slot 上
        {
            currentSelectionIndex = -1;
            // if (selectorHighlight != null) selectorHighlight.enabled = false; // <-- [刪除]
        }

        // --- [新增] 處理縮放邏輯 ---
        // 如果選擇改變了
        if (currentSelectionIndex != previousSelectionIndex)
        {
            // 把上一個選中的縮回去 (如果有的話)
            if (previousSelectionIndex != -1 && previousSelectionIndex < spawnedSlots.Count)
            {
                // SetScale(spawnedSlots[previousSelectionIndex].transform, normalScale); // 直接設定
                StartCoroutine(ScaleCoroutine(spawnedSlots[previousSelectionIndex].transform, normalScale)); // 用動畫
            }
            // 把現在選中的放大 (如果有的話)
            if (currentSelectionIndex != -1 && currentSelectionIndex < spawnedSlots.Count)
            {
                // SetScale(spawnedSlots[currentSelectionIndex].transform, highlightedScale); // 直接設定
                StartCoroutine(ScaleCoroutine(spawnedSlots[currentSelectionIndex].transform, highlightedScale)); // 用動畫
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
        if (targetTransform != null) targetTransform.localScale = targetScale;
    }
}