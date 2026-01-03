using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // 引用 UI

[RequireComponent(typeof(PlayerMovement), typeof(Collider), typeof(ObjectStats))]
public class CardboardSkill : BaseSkill // 1. 改為繼承 BaseSkill
{
    [Header("元件參考 (Cardboard 專用)")]
    private PlayerMovement playerMovement;
    private InputSystem_Actions playerActions;
    private TeamManager teamManager;
    private Animator animator;

    [Header("Heavy Push Settings")]
    [SerializeField] private float weightThreshold = 50f;
    [SerializeField] private float heavyPushForce = 50f;
    [SerializeField] private float pushInterval = 0.8f;
    [SerializeField] private float animationBaseSpeed = 5.0f;

    [Header("倉儲設定")]
    [SerializeField] private int maxStorage = 3;
    [SerializeField] private LayerMask possessableLayer;
    [SerializeField] private Transform spitOutPoint;

    [Header("輸入設定")]
    [Tooltip("長按 F 鍵觸發「吐出全部」所需的時間")]
    [SerializeField] private float holdDuration = 0.8f;

    [Header("庫存狀態")]
    [SerializeField]
    private Stack<ObjectStats> storedItems = new Stack<ObjectStats>();

    private ObjectStats selfObjectStats;

    // --- 輸入處理變數 ---
    private bool isHoldingButton = false;
    private float currentHoldTime = 0f;

    // --- 初始化 ---
    void Awake()
    {
        // 不需要 base.Awake() 因為 BaseSkill 沒有 Awake，但如果有就要加

        if (animator == null) animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        selfObjectStats = GetComponent<ObjectStats>();
        playerActions = new InputSystem_Actions();
        teamManager = FindAnyObjectByType<TeamManager>();

        if (spitOutPoint == null)
        {
            spitOutPoint = this.transform;
        }

        // 初始化 BaseSkill 的設定 (如果 Inspector 沒設)
        if (string.IsNullOrEmpty(skillName)) skillName = "紙箱收納";

        UpdateTotalWeight();
    }

    // --- 繼承 BaseSkill 的 Update ---
    protected override void Update()
    {
        base.Update(); // 2. 這行最重要！它負責更新 UI 的冷卻圈圈

        // 我們在 Update 裡檢查按住的時間，這樣比 Coroutine 更容易控制
        if (isHoldingButton && isReady) // 只有在技能冷卻好時才允許蓄力
        {
            currentHoldTime += Time.deltaTime;

            // 如果按住時間超過設定，觸發「吐出全部」
            if (currentHoldTime >= holdDuration)
            {
                HandleSkillHold();

                // 重置狀態，避免下一幀重複觸發
                isHoldingButton = false;
                currentHoldTime = 0f;
            }
        }
    }

    // --- 輸入處理邏輯 ---

    // 覆寫 BaseSkill 的 OnInput，因為我們有自己的複雜輸入邏輯，
    // 不希望 BaseSkill 的通用邏輯干擾我們
    public override void OnInput(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
        {
            // 按下按鈕：開始計時
            isHoldingButton = true;
            currentHoldTime = 0f;
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            // 放開按鈕
            if (isHoldingButton)
            {
                // 如果還在 Holding 狀態 (代表還沒觸發長按技能)，那就視為「短按」
                // 呼叫 TryActivate 走標準冷卻流程
                TryActivate();

                isHoldingButton = false;
                currentHoldTime = 0f;
            }
        }
    }

    // --- 實作 BaseSkill 要求的方法 (對應短按) ---
    protected override void Activate()
    {
        HandleSkillTap();
    }

    // --- 具體邏輯 ---

    private void HandleSkillTap() // 短按：吃一個 或 吐一個
    {
        GameObject target = GetTarget();

        // 邏輯：如果有瞄準到東西 且 沒裝滿 -> 吃
        if (target != null && storedItems.Count < maxStorage)
        {
            StoreObject(target.gameObject);
        }
        else // 否則 -> 吐
        {
            SpitOutLastObject();
        }
    }

    private void HandleSkillHold() // 長按：吐全部
    {
        if (storedItems.Count > 0)
        {
            SpitOutAllObjects();
            Debug.Log("[Cardboard] 🤮 嘔嘔嘔 (全部吐出)");

            // 手動觸發冷卻 (因為這是繞過 TryActivate 直接執行的)
            StartCooldown();
        }
        else
        {
            Debug.Log("[Cardboard] 沒東西好吐了");
        }
    }

    // --- 物品操作邏輯 (保持原樣，稍微整理) ---

    private void StoreObject(GameObject obj)
    {
        ObjectStats item = obj.GetComponent<ObjectStats>();
        if (item != null && item != selfObjectStats && !item.isInsideContainer)
        {
            item.isInsideContainer = true;
            storedItems.Push(item);
            obj.SetActive(false);
            UpdateTotalWeight();
            Debug.Log($"[Cardboard] 吞入: {obj.name}");
        }
    }

    private void SpitOutLastObject()
    {
        if (storedItems.Count > 0)
        {
            ObjectStats item = storedItems.Pop();
            GameObject obj = item.gameObject;
            item.isInsideContainer = false;

            obj.transform.position = spitOutPoint.position;
            obj.SetActive(true);

            UpdateTotalWeight();
            Debug.Log($"[Cardboard] 吐出: {obj.name}");
        }
        else
        {
            Debug.Log("[Cardboard] 空空如也。");
        }
    }

    private void SpitOutAllObjects()
    {
        if (storedItems.Count > 0)
        {
            StartCoroutine(SpitAllCoroutine());
        }
    }

    private IEnumerator SpitAllCoroutine()
    {
        float offsetDistance = 1.0f;
        while (storedItems.Count > 0)
        {
            ObjectStats item = storedItems.Pop();
            GameObject obj = item.gameObject;
            item.isInsideContainer = false;

            // 1. 先設定位置在箱子口
            obj.transform.position = spitOutPoint.position;
            obj.SetActive(true);

            // 2. 獲取剛體並施加推力
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero; // 重置速度
                                                  // 往前方 + 隨機上拋一點
                Vector3 forceDir = transform.forward + Vector3.up * 0.5f + Random.insideUnitSphere * 0.2f;
                rb.AddForce(forceDir.normalized * 5f, ForceMode.Impulse); // 5f 是噴射力道

                // 加一點隨機旋轉
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            yield return new WaitForSeconds(0.1f); // 縮短間隔，像機關槍一樣吐出來
        }
        UpdateTotalWeight();
    }

    /// <summary>
    /// 直接向 PlayerMovement 詢問當前瞄準的對象，並驗證是否可收納
    /// </summary>
    private GameObject GetTarget()
    {
        // 1. 直接取得 PlayerMovement 已經算好的目標 (省下一次 Raycast 效能，且保證視覺一致)
        GameObject target = playerMovement.CurrentTargetedObject;

        // 如果當前沒瞄準任何東西，直接回傳 null
        if (target == null) return null;

        // 2. 雖然 PlayerMovement 說有目標，但我們還是要檢查這個目標「合不合胃口」
        // (例如：可能瞄準到隊友，但隊友不能被吃；或者瞄準到不可互動的物件)

        // 檢查 Layer (利用位元運算檢查是否在 possessableLayer 清單內)
        if (((1 << target.layer) & possessableLayer) == 0)
        {
            return null; // 層級不對 (例如瞄準到牆壁或地板)
        }

        // 3. 執行原本的防呆檢查

        // 排除自己
        if (target.transform.root == transform.root) return null;

        // 排除另一個紙箱 (假設設定上不能吃紙箱)
        if (target.GetComponent<CardboardSkill>() != null) return null;

        // 必須有 ObjectStats 且不在容器內
        ObjectStats stats = target.GetComponent<ObjectStats>();
        if (stats != null && !stats.isInsideContainer)
        {
            // 通過所有檢查，這就是我們要吃的東西！
            return target;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        // 確保 playerMovement 存在 (編輯器模式下可能需要 GetComponent)
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement != null && playerMovement.cameraTransform != null)
        {
            Gizmos.color = Color.red;
            // 使用 playerMovement.interactionDistance
            Gizmos.DrawRay(playerMovement.cameraTransform.position, playerMovement.cameraTransform.forward * playerMovement.interactionDistance);
        }
    }

    // --- 重量與動畫 ---

    private void UpdateTotalWeight()
    {
        float totalWeight = selfObjectStats.weight;
        totalWeight += storedItems.Sum(item => item.weight);
        bool isOver = (totalWeight > weightThreshold);
        playerMovement.SetWeightAndPushStats(totalWeight, isOver, heavyPushForce, pushInterval);
    }

    public void UpdateAnimationState(Rigidbody rb, bool isOverEncumbered, bool isPushing)
    {
        if (animator == null || rb == null) return;

        animator.SetBool("isOverEncumbered", isOverEncumbered);

        if (isOverEncumbered)
        {
            if (!isPushing)
            {
                animator.SetFloat("Speed", 0f);
                animator.speed = 1.0f;
            }
        }
        else
        {
            float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
            animator.SetFloat("Speed", horizontalSpeed);

            if (horizontalSpeed > 0.1f && animationBaseSpeed > 0f)
            {
                animator.speed = horizontalSpeed / animationBaseSpeed;
            }
            else
            {
                animator.speed = 1.0f;
            }
        }
    }
}