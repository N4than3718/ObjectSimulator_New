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
    private Collider[] detectedObjectsBuffer = new Collider[10];
    private float fKeyStartTime = 0f;

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

    void OnEnable()
    {
        // 啟用輸入
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();

        // 監聽 F 鍵 (Interact)
        playerActions.Player.Interact.started += OnSkillPress;
        playerActions.Player.Interact.canceled += OnSkillRelease;
    }

    void OnDisable()
    {
        if (playerActions != null) playerActions.Player.Disable();

        playerActions.Player.Interact.started -= OnSkillPress;
        playerActions.Player.Interact.canceled -= OnSkillRelease;
    }

    // --- 繼承 BaseSkill 的 Update ---
    protected override void Update()
    {
        base.Update(); // 2. 這行最重要！它負責更新 UI 的冷卻圈圈
    }

    // --- 輸入處理邏輯 ---

    // 覆寫 BaseSkill 的 OnInput，因為我們有自己的複雜輸入邏輯，
    // 不希望 BaseSkill 的通用邏輯干擾我們
    public override void OnInput(InputAction.CallbackContext context)
    {
        // 留空，完全交給 OnSkillPress/Release 處理
    }

    private void OnSkillPress(InputAction.CallbackContext context)
    {
        if (!playerMovement.enabled) return;
        fKeyStartTime = Time.time;
    }

    private void OnSkillRelease(InputAction.CallbackContext context)
    {
        if (!playerMovement.enabled) return;

        // 檢查是否還在冷卻中 (如果有 UI，這裡就會擋住輸入)
        // 注意：這裡我選擇「如果是 SpitAll (長按) 則忽略冷卻」，
        // 或者你可以統一都檢查 !isReady。這裡示範統一檢查。
        if (!isReady)
        {
            Debug.Log("技能冷卻中...");
            return;
        }

        float pressDuration = Time.time - fKeyStartTime;

        if (pressDuration < holdDuration)
        {
            // 短按：呼叫 BaseSkill 的流程來處理「點擊行為」
            TryActivate();
            // TryActivate 會檢查 isReady -> 呼叫 Activate() -> StartCooldown()
        }
        else
        {
            // 長按：直接執行特殊邏輯 (吐出全部)
            // 視設計決定長按要不要進冷卻，這裡假設長按也要冷卻
            HandleSkillHold();
        }
    }

    // --- 實作 BaseSkill 的核心方法 ---

    /// <summary>
    /// 這是 BaseSkill 要求實作的方法。
    /// 當 TryActivate() 通過檢查時，會呼叫這裡。
    /// 對應「短按 F」的邏輯。
    /// </summary>
    protected override void Activate()
    {
        HandleSkillTap();
    }

    // --- 具體邏輯 ---

    private void HandleSkillTap()
    {
        GameObject target = GetTarget();

        if (target != null && storedItems.Count < maxStorage)
        {
            // 吃東西
            StoreObject(target.gameObject);
        }
        else
        {
            // 吐東西
            SpitOutLastObject();
        }
        // 因為是透過 TryActivate 呼叫的，BaseSkill 會自動幫我們 StartCooldown()
    }

    private void HandleSkillHold()
    {
        // 吐出全部
        if (storedItems.Count > 0)
        {
            SpitOutAllObjects();

            // 長按結束後，也手動觸發冷卻 UI
            StartCooldown();
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

            Vector3 spitPos = spitOutPoint.position + (transform.forward * offsetDistance) + (Random.insideUnitSphere * 0.1f);
            spitPos.y = spitOutPoint.position.y;

            obj.transform.position = spitPos;
            obj.SetActive(true);

            offsetDistance += 0.5f;
            yield return new WaitForSeconds(0.15f);
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