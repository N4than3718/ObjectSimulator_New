using System.Collections;
using System.Collections.Generic;
using System.Linq; // 這是 C# 的好東西，用來加總 (Sum)
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement), typeof(Collider), typeof(ObjectStats))]
public class Cardboard : MonoBehaviour
{
    [Header("元件參考")]
    private PlayerMovement playerMovement;
    private InputSystem_Actions playerActions;
    private TeamManager teamManager;

    [Header("Heavy Push Settings")]
    [Tooltip("超過這個重量，移動方式變為'重推'")]
    [SerializeField] private float weightThreshold = 50f;
    [Tooltip("重推的單次爆發力")]
    [SerializeField] private float heavyPushForce = 50f;
    [Tooltip("每次重推的間隔/動畫時長 (秒)")]
    [SerializeField] private float pushInterval = 0.8f;

    [Header("倉儲設定")]
    [SerializeField] private int maxStorage = 3;
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private LayerMask possessableLayer; // <--- 在 Inspector 設為 "Player"
    [SerializeField] private Transform spitOutPoint; // <--- 在 Prefab 建立一個空物件作為吐出點

    [Header("輸入設定")]
    [SerializeField]
    [Tooltip("長按 F 鍵觸發「吐出全部」所需的時間")]
    private float holdDuration = 0.8f;

    [Header("庫存狀態")]
    [Tooltip("目前裝在裡面的物品清單 (除錯用)")]
    [SerializeField]
    private Stack<ObjectStats> storedItems = new Stack<ObjectStats>();

    private ObjectStats selfObjectStats;
    private Collider[] detectedObjectsBuffer = new Collider[10];
    private float fKeyStartTime = 0f;

    void Awake()
    {
        // 找到在同一個物件上的 PlayerMovement
        playerMovement = GetComponent<PlayerMovement>();
        selfObjectStats = GetComponent<ObjectStats>();
        playerActions = new InputSystem_Actions();
        teamManager = FindAnyObjectByType<TeamManager>(); // 找到 TeamManager

        if (spitOutPoint == null)
        {
            Debug.LogWarning("Cardboard: SpitOutPoint 未設定，將使用自身位置。", this);
            spitOutPoint = this.transform;
        }
        if (teamManager == null)
        {
            Debug.LogError("Cardboard: 找不到 TeamManager！倉儲功能將失效。", this);
        }

        // 遊戲一開始，先把紙箱的基礎重量設定好
        UpdateTotalWeight();
    }

    // 當 PlayerMovement 被 TeamManager 啟用時，這個也會被啟用
    void OnEnable()
    {
        // 確保 OnEnable/OnDisable 與 PlayerMovement (line 150) 一致
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();

        playerActions.Player.Interact.started += OnSkillPress; // 監聽按下
        playerActions.Player.Interact.canceled += OnSkillRelease; // 監聽放開
    }

    // 當 PlayerMovement 被 TeamManager 禁用時...
    void OnDisable()
    {
        if (playerActions != null) playerActions.Player.Disable();

        playerActions.Player.Interact.started -= OnSkillPress;
        playerActions.Player.Interact.canceled -= OnSkillRelease;
    }

    void Update()
    {   }

    /// <summary>
    /// 當 F 鍵 (Interact) 按下時呼叫
    /// </summary>
    private void OnSkillPress(InputAction.CallbackContext context)
    {
        // 只在被操控時才記錄
        if (!playerMovement.enabled) return;

        // 記錄按下的精確時間
        fKeyStartTime = Time.time;
    }

    /// <summary>
    /// 當 F 鍵 (Interact) 放開時呼叫
    /// </summary>
    private void OnSkillRelease(InputAction.CallbackContext context)
    {
        // 只在被操控時才處理
        if (!playerMovement.enabled) return;

        // 計算按了多久
        float pressDuration = Time.time - fKeyStartTime;

        if (pressDuration < holdDuration)
        {
            // 這是一次「點擊」
            HandleSkillTap();
        }
        else
        {
            // 這是一次「長按」結束
            HandleSkillHold();
        }
        // (fKeyStartTime 不需要重置，下次按下時會自動覆蓋)
    }

    /// <summary>
    /// 處理「點擊 F」：嘗試儲存，如果不行就吐出最後一個
    /// </summary>
    private void HandleSkillTap()
    {
        Collider target = FindClosestNearbyObject();

        if (target != null && storedItems.Count < maxStorage)
        {
            // 情況 1: 附近有東西，且還有空間 -> 儲存
            StoreObject(target.gameObject);
        }
        else
        {
            // 情況 2: 附近沒東西，或空間已滿 -> 吐出
            SpitOutLastObject();
        }
    }

    /// <summary>
    /// 處理「長按 F」：吐出所有
    /// </summary>
    private void HandleSkillHold()
    {
        SpitOutAllObjects();
    }

    /// <summary>
    /// 儲存一個物件
    /// </summary>
    private void StoreObject(GameObject obj)
    {
        ObjectStats item = obj.GetComponent<ObjectStats>();

        // 確保 1. 它是物品 2. 它還沒在任何容器裡 3. 它不是自己
        if (item != null && item != selfObjectStats && !item.isInsideContainer)
        {
            Debug.Log($"[Cardboard] 儲存: {obj.name}。 目前容量: {storedItems.Count + 1}/{maxStorage}"); // [修改]

            // 1. 推入堆疊
            item.isInsideContainer = true;
            storedItems.Push(item); // [修改]

            // 2. 通知 TeamManager 移除
            teamManager.RemoveCharacterFromTeam(obj);

            // 3. 重新計算總重量
            UpdateTotalWeight();
        }
    }

    /// <summary>
    /// 吐出最後一個物件
    /// </summary>
    private void SpitOutLastObject()
    {
        if (storedItems.Count > 0) // [修改]
        {
            // 1. 彈出堆疊
            ObjectStats item = storedItems.Pop(); // [修改]
            GameObject obj = item.gameObject;
            item.isInsideContainer = false;

            Debug.Log($"[Cardboard] 吐出: {obj.name}。 剩餘: {storedItems.Count}"); // [修改]

            // 2. 在吐出點啟用物件
            obj.transform.position = spitOutPoint.position;
            obj.SetActive(true);

            // 3. 重新計算總重量
            UpdateTotalWeight();
            Debug.Log($"[BoxContainer] {obj.name} 離開。");
        }
        else
        {
            Debug.Log("[Cardboard] 箱子是空的，沒東西可吐出。");
        }
    }

    /// <summary>
    /// 透過 Coroutine 吐出所有物件
    /// </summary>
    private void SpitOutAllObjects()
    {
        if (storedItems.Count > 0)
        {
            Debug.Log($"[Cardboard] 吐出全部 {storedItems.Count} 個物件...");
            StartCoroutine(SpitAllCoroutine());
        }
    }

    private IEnumerator SpitAllCoroutine()
    {
        float offsetDistance = 1.0f;
        while (storedItems.Count > 0)
        {
            ObjectStats item = storedItems.Pop(); // [修改]
            GameObject obj = item.gameObject;
            item.isInsideContainer = false;

            // 計算吐出位置 (在前方散開)
            Vector3 spitPos = spitOutPoint.position + (transform.forward * offsetDistance) + (Random.insideUnitSphere * 0.1f);
            spitPos.y = spitOutPoint.position.y; // 保持在同一水平面

            obj.transform.position = spitPos;
            obj.SetActive(true);

            Debug.Log($"[BoxContainer] {obj.name} 離開。");

            offsetDistance += 0.5f; // 下一個吐遠一點
            yield return new WaitForSeconds(0.15f); // 稍微間隔
        }
        UpdateTotalWeight();
    }

    /// <summary>
    /// 尋找最近的、可儲存的物件
    /// </summary>
    private Collider FindClosestNearbyObject()
    {
        int hits = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRadius,
            detectedObjectsBuffer,
            possessableLayer, // 只偵測 "Player" Layer
            QueryTriggerInteraction.Ignore
        );

        Collider closest = null;
        float minSqrDist = Mathf.Infinity;

        for (int i = 0; i < hits; i++)
        {
            Collider hit = detectedObjectsBuffer[i];

            // 排除自己 (檢查 attachedRigidbody 是最準確的)
            if (hit.attachedRigidbody == playerMovement.GetComponent<Rigidbody>())
            {
                continue;
            }

            // 確保對方也是一個可操控的物件 (有 PlayerMovement 腳本)
            if (hit.GetComponent<PlayerMovement>() == null)
            {
                continue;
            }

            // [新增] 檢查對方是否也是 Cardboard，箱子不能裝箱子
            if (hit.GetComponent<Cardboard>() != null) { continue; }

            // [新增] 檢查對方是否已在容器中 (雖然 PlayerMovement enabled 應該=false了，但雙重保險)
            ObjectStats stats = hit.GetComponent<ObjectStats>();
            if (stats != null && stats.isInsideContainer) { continue; }

            // 找到最近的
            float sqrDist = (transform.position - hit.transform.position).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = hit;
            }
        }
        return closest;
    }

    // 在 Scene 視窗繪製偵測範圍，方便 Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.2f, 0f, 0.3f); // 咖啡色
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    /// <summary>
    /// 計算總重量並更新 PlayerMovement
    /// </summary>
    private void UpdateTotalWeight()
    {
        // 總重 = 紙箱自重 + (清單中所有物品的重量)
        float totalWeight = selfObjectStats.weight;

        // 使用 Linq.Sum() 快速加總
        totalWeight += storedItems.Sum(item => item.weight);

        bool isOver = (totalWeight > weightThreshold);

        // **這就是關鍵：**
        // 把計算好的總重，"餵" 給 PlayerMovement 腳本
        playerMovement.SetWeightAndPushStats(totalWeight, isOver, heavyPushForce, pushInterval);

        //Debug.Log($"[BoxContainer] 總重量更新為: {totalWeight}kg");
    }
}