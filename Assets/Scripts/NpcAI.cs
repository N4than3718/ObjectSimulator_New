using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent), typeof(Animator))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Investigating, Alerted }

    [Header("Debug 強制撿拾")]
    public bool forcePickupDebug = false;
    public Transform debugPickupTarget;

    [Header("Component References")]
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;
    // fov 在 Awake 中獲取

    [Header("IK 設定")]
    [Tooltip("指定右手骨骼底下的 'GrabSocket' 空物件")]
    public Transform grabSocket;
    [Tooltip("Optional: 右手肘提示點，避免手臂穿插")]
    public Transform rightElbowHint; // 改為 public 指定，比 FindRecursive 穩定

    [Header("UI 設定")] // <--- [新增]
    [SerializeField] private NpcStatusUI statusUiPrefab; // 拖曳剛剛做的 UI Prefab

    [Header("AI 狀態")]
    [SerializeField] private NpcState currentState = NpcState.Searching;

    [Header("巡邏設定")]
    public List<Transform> patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("警戒值設定")]
    [SerializeField] private float lowAlertIncreaseRate = 20f;
    [SerializeField] private float lowAlertDecreaseRate = 10f;
    [SerializeField] private float mediumAlertIncreaseRate = 40f;
    [SerializeField] private float mediumAlertDecreaseRate = 15f;
    [SerializeField] private float highAlertDecreaseRate = 10f;
    [SerializeField] private float timeToStartDecreasing = 3f;
    [SerializeField] private float movementThreshold = 0.1f; // 物體移動速度閾值

    [Header("聽覺與調查設定")] // <--- [修改] 分類標題
    [SerializeField] private float hearingSensitivity = 1.0f;
    [SerializeField] private float investigateWaitTime = 4.0f; // [修改]稍微久一點，讓他有時間轉頭
    [Tooltip("Animator Controller 裡的 Bool 參數名稱")]
    [SerializeField] private string lookAroundAnimParam = "IsLookingAround";
    public float HearingSensitivity => hearingSensitivity;

    [Header("速度設定")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float investigateSpeed = 3.5f; // [新增] 調查時走快一點，比較緊張
    [SerializeField] private float chaseSpeed = 5f;

    [Header("捕捉設定")]
    [SerializeField] private float captureDistance = 1.5f;

    [Header("效能設定")]
    [Tooltip("AI 決策邏輯的更新間隔 (秒)")]
    [SerializeField] private float aiUpdateInterval = 0.2f;

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    public float CurrentAlertLevel => currentAlertLevel;
    public NpcState CurrentState => currentState;
    public static event Action<NpcAI, float> OnNoiseHeard;

    // --- 私有變數 ---
    private FieldOfView fov;
    private Dictionary<Transform, Vector3> lastKnownPositions = new Dictionary<Transform, Vector3>();
    private float timeSinceLastSighting = 0f;
    private Vector3 lastSightingPosition;
    private Transform threatTarget = null;

    // 調查相關
    private Vector3? noiseInvestigationTarget = null;
    private float investigationTimer = 0f;
    private Quaternion investigationStartRotation; // 用於紀錄到達時的朝向
    private float lookAroundTimer = 0f; // 用於控制轉頭節奏

    // IK & Grab 相關
    private Transform ikTargetPoint = null;     // IK 伸手瞄準的目標點 (GrabPoint 或物件 Root)
    private Transform objectToGrab = null;      // 從 TriggerPickup 傳遞到 AnimationEvent 的臨時指標
    private float handIKWeight = 0f;
    private float hintIKWeight = 0f;

    // 物件跟隨 (Follow) 相關
    private bool _isHoldingObject = false;
    private Transform _heldObjectRef = null;    // 當前實際抓著的物件引用
    // private Vector3 _holdOffsetPosition = Vector3.zero; // 不再需要預存 Offset
    // private Quaternion _holdOffsetRotation = Quaternion.identity;
    private Transform _pointToAlignWithSocket = null; // 要對齊的點 (GrabPoint 或物件 Root)

    private TeamManager teamManager;

    void Awake()
    {
        // 獲取必要的組件
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        fov = GetComponent<FieldOfView>();

        // 錯誤檢查
        if (anim == null) Debug.LogError("Animator not found!", this);
        if (agent == null) Debug.LogError("NavMeshAgent not found!", this);
        if (fov == null) Debug.LogError("FieldOfView not found!", this);

        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("NpcAI cannot find TeamManager!");
    }

    void Start()
    {
        // Debug 模式或正常啟動 AI
        if (forcePickupDebug && debugPickupTarget != null)
        {
            Debug.LogWarning($"--- DEBUG MODE: Forcing pickup of {debugPickupTarget.name} ---", this.gameObject);
            StartCoroutine(DebugPickupRoutine());
        }
        else
        {
            StealthManager.RegisterNpc(this);
            currentState = NpcState.Searching;
            agent.speed = patrolSpeed;
            if (patrolPoints != null && patrolPoints.Count > 0 && patrolPoints[0] != null) // 確保列表和第一個點存在
            {
                agent.SetDestination(patrolPoints[0].position);
            }
            else if (patrolPoints == null || patrolPoints.Count == 0)
            {
                Debug.LogWarning("No patrol points assigned. NPC will remain idle unless alerted.", this.gameObject);
            }
            else
            {
                Debug.LogWarning("First patrol point is null. Please assign patrol points.", this.gameObject);
            }
            StartCoroutine(AIUpdateLoop());
        }

        if (statusUiPrefab != null)
        {
            NpcStatusUI uiInstance = Instantiate(statusUiPrefab, transform.position, Quaternion.identity);
            // 注意：這裡不設 parent，避免 UI 跟著 NPC 旋轉變形，而是由 UI 腳本自己跟隨位置
            uiInstance.Initialize(this);
        }
    }

    void Update()
    {
        // 更新 Animator 速度參數
        UpdateAnimator();

        // 平滑更新 IK 權重 (只在伸手階段需要)
        bool isPickingUp = anim.GetCurrentAnimatorStateInfo(0).IsName("Pick up");

        // 只有在準備伸手 (ikTargetPoint 存在) 且未抓取時才增加權重
        if (isPickingUp && ikTargetPoint != null && !_isHoldingObject)
        {
            handIKWeight = Mathf.Lerp(handIKWeight, 1.0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 1.0f, Time.deltaTime * 5f);
        }
        else
        {
            // 其他情況都讓權重歸零
            handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);
        }
    }

    void LateUpdate()
    {
        // 如果正在抓著物件，更新物件 Transform 使其 _pointToAlignWithSocket 對齊 grabSocket
        if (_isHoldingObject && _heldObjectRef != null && grabSocket != null && _pointToAlignWithSocket != null)
        {
            // 1. 計算旋轉差並應用
            Quaternion rotationDifference = grabSocket.rotation * Quaternion.Inverse(_pointToAlignWithSocket.rotation);
            _heldObjectRef.rotation = rotationDifference * _heldObjectRef.rotation;

            // 2. 計算位置差並應用
            Vector3 positionDifference = grabSocket.position - _pointToAlignWithSocket.position;
            _heldObjectRef.position += positionDifference;
        }
    }

    private IEnumerator AIUpdateLoop()
    {
        yield return new WaitForSeconds(aiUpdateInterval); // 初始延遲

        while (true)
        {
            // 持續檢查 TeamManager (如果遊戲中可能重新加載或生成)
            if (teamManager == null)
            {
                teamManager = FindAnyObjectByType<TeamManager>();
                if (teamManager == null)
                {
                    Debug.LogWarning("AIUpdateLoop still waiting for TeamManager...");
                    yield return new WaitForSeconds(1f);
                    continue;
                }
            }

            // 執行狀態邏輯
            switch (currentState)
            {
                case NpcState.Searching:
                    SearchingState();
                    break;
                case NpcState.Investigating:
                    InvestigatingState();
                    break;
                case NpcState.Alerted:
                    AlertedState();
                    break;
            }

            currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f); // 限制警戒值
            yield return new WaitForSeconds(aiUpdateInterval); // 等待下一次更新
        }
    }

    // Debug 模式下直接觸發撿拾 (不移動)
    private IEnumerator DebugPickupRoutine()
    {
        if (debugPickupTarget == null)
        {
            Debug.LogError("Debug Target is null.", this.gameObject);
            yield break;
        }

        // 可選：等待一小段時間確保場景初始化完成
        yield return new WaitForSeconds(0.5f);

        // 可選：轉向目標
        if (ikTargetPoint != null) // 使用 TriggerPickup 找到的 ikTargetPoint 來轉向
        {
            transform.LookAt(ikTargetPoint.position);
            yield return null; // 等待一幀讓旋轉應用
        }
        else // 如果 TriggerPickup 尚未執行或失敗，至少轉向根物件
        {
            transform.LookAt(debugPickupTarget.position);
            yield return null;
        }

        Debug.Log("--- DEBUG: Triggering pickup animation ---");
        TriggerPickup(debugPickupTarget);
    }

    // 啟動撿拾流程
    public void TriggerPickup(Transform targetInput)
    {
        if (targetInput == null) { Debug.LogError("TriggerPickup called with null targetRoot!"); return; }

        Transform targetRoot = targetInput;
        PlayerMovement pm = targetInput.GetComponentInParent<PlayerMovement>();

        if (pm != null)
        {
            targetRoot = pm.transform; // 找到了！這才是真正的鬧鐘
        }
        else
        {
            targetRoot = targetInput.root; // 如果沒找到 PM，就用最上層的 root 保底
        }

        Debug.Log($"NPC 修正抓取目標: {targetInput.name} -> {targetRoot.name}");

        // 停止 Agent 移動
        if (agent != null && agent.enabled) agent.isStopped = true;

        objectToGrab = targetRoot; // 設定要抓取的物件根節點

        // 尋找抓握點 (GrabPoint) 或使用根節點
        Transform grabPoint = targetRoot.Find("GrabPoint");
        ikTargetPoint = (grabPoint != null) ? grabPoint : targetRoot; // 設定 IK 瞄準點

        if (grabPoint == null)
        {
            Debug.LogWarning($"Object {targetRoot.name} lacks a 'GrabPoint' child. IK targeting object root.", targetRoot);
        }

        // 觸發撿拾動畫
        if (anim != null) anim.SetTrigger("Pick up");
        else { Debug.LogError("Animator is null, cannot trigger pickup."); return; }

        // 讓 NPC 朝向 IK 目標點
        if (ikTargetPoint != null) transform.LookAt(ikTargetPoint.position);
    }

    // IK 計算 (每幀由 Animator 調用)
    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        // IK 條件檢查：目標存在且權重大於 0
        if (ikTargetPoint == null || handIKWeight <= 0)
        {
            // 不滿足條件，重置 IK 權重
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            if (rightElbowHint != null) anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0); // 檢查 Hint 是否存在
            return;
        }

        // --- 執行 IK ---
        // 設定權重
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight);
        // 設定目標
        anim.SetIKPosition(AvatarIKGoal.RightHand, ikTargetPoint.position);
        anim.SetIKRotation(AvatarIKGoal.RightHand, ikTargetPoint.rotation);

        // --- 手肘提示 ---
        if (rightElbowHint != null) // 使用 Inspector 指定的提示點
        {
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintIKWeight);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);
        }
    }

    // (FindRecursive 函數已移除，改用 Inspector 指定 rightElbowHint)

    // 動畫事件：抓取物件的瞬間
    public void AnimationEvent_GrabObject()
    {
        // Debug.Log("AnimationEvent_GrabObject CALLED"); // 保留基礎 Log
        if (grabSocket == null) { Debug.LogError("GrabSocket IS NULL!", this.gameObject); return; }

        // 檢查從 TriggerPickup 傳來的 objectToGrab
        if (objectToGrab != null)
        {
            if (teamManager != null) teamManager.RemoveCharacterFromTeam(objectToGrab.gameObject);

            _heldObjectRef = objectToGrab; // 正式持有物件

            // 確定要對齊哪個點 (GrabPoint 或 Root)
            Transform potentialGrabPoint = _heldObjectRef.Find("GrabPoint");
            _pointToAlignWithSocket = (potentialGrabPoint != null) ? potentialGrabPoint : _heldObjectRef;
            // Debug.Log($"Holding '{_heldObjectRef.name}', Aligning '{_pointToAlignWithSocket.name}'");

            // --- 關閉物理 ---
            Rigidbody rb = _heldObjectRef.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            Collider col = _heldObjectRef.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }

            // --- 設定跟隨狀態 ---
            _isHoldingObject = true;

            // --- 立即關閉 IK & 清除臨時變數 ---
            handIKWeight = 0f; // 強制權重歸零
            ikTargetPoint = null; // 清除 IK 目標
            objectToGrab = null;  // 清除臨時指標
        }
        else
        {
            Debug.LogError("Grab Logic SKIPPED! objectToGrab was NULL!");
        }
    }

    // 動畫事件：撿拾動畫結束 或 手動觸發放開
    public void AnimationEvent_PickupEnd()
    {
        // Debug.Log("AnimationEvent_PickupEnd CALLED"); // 保留基礎 Log
        Transform objectToRelease = _heldObjectRef; // 暫存引用

        // 關閉跟隨
        _isHoldingObject = false;
        _heldObjectRef = null;
        _pointToAlignWithSocket = null;

        // --- 重新啟用物理 ---
        if (objectToRelease != null)
        {
            Rigidbody rb = objectToRelease.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
            Collider col = objectToRelease.GetComponent<Collider>();
            if (col != null) { col.enabled = true; }
            // 可選：施加一點力
            // if (rb != null) rb.AddForce(transform.forward * 0.1f + Vector3.up * 0.1f, ForceMode.VelocityChange);
        }

        // 清除 IK 變數 (保險)
        ikTargetPoint = null;
        objectToGrab = null;

        // 恢復 Agent 移動
        if (agent != null && agent.enabled) agent.isStopped = false;
    }

    // 更新 Animator 速度參數
    private void UpdateAnimator()
    {
        if (agent == null || anim == null) return;
        if (!agent.enabled) { anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime); return; }

        float currentSpeed = agent.velocity.magnitude;
        float normalizedSpeed = agent.speed > 0 ? (currentSpeed / agent.speed) : 0f;
        anim.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
    }

    // Searching 狀態邏輯
    private void SearchingState()
    {
        agent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // 增加警戒
            float increaseRate = (currentAlertLevel < 100) ? lowAlertIncreaseRate : mediumAlertIncreaseRate;
            currentAlertLevel += increaseRate * aiUpdateInterval;
            timeSinceLastSighting = 0f;
            lastSightingPosition = movingTarget.position;
            noiseInvestigationTarget = null;
        }
        else
        {
            // 沒看到人，慢慢降警戒
            HandleAlertDecrease();
        }

        // 狀態轉換：警戒值滿 -> Alerted
        if (currentAlertLevel >= 200 && movingTarget != null)
        {
            EnterAlertedState(movingTarget);
        }
    }

    private void InvestigatingState()
    {
        // 1. 視覺優先：如果調查途中看到東西在動，直接進入追擊！
        Transform movingTarget = CheckForMovingTargets();
        if (movingTarget != null)
        {
            currentAlertLevel += mediumAlertIncreaseRate * aiUpdateInterval; // 快速增加
            if (currentAlertLevel >= 200)
            {
                EnterAlertedState(movingTarget);
                return;
            }
        }
        else
        {
            // 沒看到人，但因為處於緊張狀態，警戒值下降得比 Searching 慢
            if (currentAlertLevel > 100)
                currentAlertLevel -= (mediumAlertDecreaseRate * 0.5f) * aiUpdateInterval;
        }

        // 2. 移動到聲音來源
        if (noiseInvestigationTarget.HasValue)
        {
            agent.speed = investigateSpeed; // 走快一點
            agent.SetDestination(noiseInvestigationTarget.Value);

            // 3. 到達檢查
            if (!agent.pathPending && agent.remainingDistance <= 0.5f)
            {
                // 到達目的地，開始「左右張望」行為
                PerformLookAroundBehavior();
            }
        }
        else
        {
            // 防呆：如果沒有目標，切回搜索
            currentState = NpcState.Searching;
        }

        // 4. 結束條件：警戒值太低，或者調查完畢
        if (currentAlertLevel < 50f) // 警戒值降很低了，放鬆
        {
            Debug.Log("NPC: 危機解除，回歸巡邏。");
            currentState = NpcState.Searching;
            noiseInvestigationTarget = null;
        }
    }

    /// <summary>
    /// 模擬「左右張望」的程序化動畫
    /// </summary>
    private void PerformLookAroundBehavior()
    {
        // --- 1. 剛到達時，觸發動畫 ---
        if (investigationTimer == 0f)
        {
            Debug.Log("NPC: 到達聲音點，播放搜索動畫...");
            if (anim != null) anim.SetBool(lookAroundAnimParam, true); // 開啟動畫

            // (可選) 確保 NPC 停下來
            if (agent.enabled) agent.isStopped = true;
        }

        investigationTimer += aiUpdateInterval; // 使用 AI Update 的間隔來計時 (注意：這裡比較粗略)
        // 為了更精準的計時，你可以改成在 Update() 裡累加 Time.deltaTime，但這裡先維持架構一致

        // --- 2. 時間到，結束調查 ---
        if (investigationTimer >= investigateWaitTime)
        {
            Debug.Log("NPC: 什麼都沒有... (切回巡邏)");

            // 關閉動畫
            if (anim != null) anim.SetBool(lookAroundAnimParam, false);

            // 恢復移動
            if (agent.enabled) agent.isStopped = false;

            // 重置狀態
            currentState = NpcState.Searching;
            noiseInvestigationTarget = null;
            investigationTimer = 0f;
        }
    }

    // Alerted 狀態邏輯
    private void AlertedState()
    {
        agent.speed = chaseSpeed;
        currentAlertLevel -= highAlertDecreaseRate * aiUpdateInterval; // 自然下降

        bool threatIsStillVisible = (threatTarget != null && fov.visibleTargets.Contains(threatTarget));

        if (threatIsStillVisible)
        {
            // A: 持續追擊
            agent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position;
            timeSinceLastSighting = 0f;

            // 檢查捕捉
            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"Capturing target: {threatTarget.name}!");
                // if (teamManager != null) teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                TriggerPickup(threatTarget); // -> isStopped = true
                currentAlertLevel = 0;
                threatTarget = null;
                currentState = NpcState.Searching;
                return; // 結束
            }
        }
        else
        {
            // B: 目標丟失，前往最後位置
            if (threatTarget != null && agent.destination != lastSightingPosition) // 檢查 threatTarget 是否存在
            {
                agent.SetDestination(lastSightingPosition);
            }

            // C: 發現新目標
            Transform newMovingTarget = CheckForMovingTargets();
            if (newMovingTarget != null && newMovingTarget != threatTarget)
            {
                Debug.Log($"Lost target {threatTarget?.name ?? "NULL"}! New target: {newMovingTarget.name}");
                threatTarget = newMovingTarget;
                currentAlertLevel = 200f; // 重置警戒
                lastSightingPosition = threatTarget.position;
                timeSinceLastSighting = 0f;
                // 不 return，下一輪追擊
            }
            // D: 到達最後位置且無新發現
            else if (threatTarget != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) // 檢查 threatTarget
            {
                timeSinceLastSighting += aiUpdateInterval;
                if (timeSinceLastSighting > 2f)
                {
                    Debug.Log("NPC: 追丟了，在附近找找 (Alerted -> Investigating)");
                    currentState = NpcState.Investigating;
                    noiseInvestigationTarget = transform.position; // 原地搜寻一下
                    investigationTimer = 0f; // 重置搜索計時
                    currentAlertLevel = 150f; // 維持一定警戒
                    threatTarget = null;
                }
            }
            // E: 如果一開始就沒有 threatTarget (例如直接被設為 Alerted?)，或者目標已被銷毀
            else if (threatTarget == null)
            {
                Debug.LogWarning("Alerted state entered without a valid threatTarget or target destroyed. Returning to search.");
                currentState = NpcState.Searching;
            }
        }

        // 警戒值過低，返回搜索
        if (currentAlertLevel < 100)
        {
            Debug.Log("Alert level dropped. Returning to search.");
            threatTarget = null;
            currentState = NpcState.Searching;
        }
    }

    private void HandleAlertDecrease()
    {
        if (currentAlertLevel < 100)
        {
            currentAlertLevel -= lowAlertDecreaseRate * aiUpdateInterval;
        }
        else
        {
            timeSinceLastSighting += aiUpdateInterval;
            if (timeSinceLastSighting >= timeToStartDecreasing)
            {
                currentAlertLevel -= mediumAlertDecreaseRate * aiUpdateInterval;
            }
        }
    }

    private void EnterAlertedState(Transform target)
    {
        if (anim != null) anim.SetBool(lookAroundAnimParam, false);
        if (agent.enabled) agent.isStopped = false;

        threatTarget = target;
        currentState = NpcState.Alerted;
        Debug.Log($"State Change: -> Alerted! Target: {threatTarget.name}");
    }

    // 檢查移動目標
    private Transform CheckForMovingTargets()
    {
        Transform detectedMovingTarget = null;
        if (fov == null || fov.visibleTargets == null) return null;

        foreach (Transform target in fov.visibleTargets)
        {
            if (target == null) continue;

            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue;
            }

            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);
            if (aiUpdateInterval > 0 && (distanceMoved / aiUpdateInterval) > movementThreshold)
            {
                detectedMovingTarget = target;
                // 更新最後位置的邏輯移到狀態機內，避免看到靜止物體也更新
                // lastSightingPosition = target.position;
            }
            lastKnownPositions[target] = target.position; // 持續更新位置
        }

        // 清理
        List<Transform> targetsToForget = new List<Transform>(lastKnownPositions.Count); // 初始化容量
        foreach (var pair in lastKnownPositions)
        {
            if (!fov.visibleTargets.Contains(pair.Key))
            {
                targetsToForget.Add(pair.Key);
            }
        }
        foreach (Transform target in targetsToForget)
        {
            lastKnownPositions.Remove(target);
        }

        return detectedMovingTarget;
    }

    private void InvestigateNoise()
    {
        if (!noiseInvestigationTarget.HasValue) return;

        agent.SetDestination(noiseInvestigationTarget.Value);

        // 檢查是否到達聲音來源附近
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            // 到達了，開始計時發呆
            investigationTimer += Time.deltaTime;
            if (investigationTimer >= investigateWaitTime)
            {
                Debug.Log("NPC: 這裡沒東西啊... (回去巡邏)");
                noiseInvestigationTarget = null; // 清除目標，回歸 Patrol
                investigationTimer = 0f;
            }
        }
    }

    public void HearNoise(Vector3 position, float range, float intensity)
    {
        // 1. 計算距離衰減 (可選，這裡先簡化)
        float distance = Vector3.Distance(transform.position, position);

        // 簡單遮擋判斷：從 NPC 頭部射向聲音來源
        // (這裡假設 NPC pivot 在腳底，所以 + Vector3.up * 1.5f)
        Vector3 earPos = transform.position + Vector3.up * 1.5f;
        if (Physics.Linecast(earPos, position, out RaycastHit hit))
        {
            // 如果中間有牆壁 (不是玩家)，聲音減弱
            if (!hit.collider.CompareTag("Player"))
            {
                intensity *= 0.5f; // 隔牆有耳，但聽不清楚
            }
        }

        // 2. 增加警戒值
        float effectiveIntensity = intensity * hearingSensitivity;
        currentAlertLevel += effectiveIntensity;

        Debug.Log($"NPC 聽到聲音! 來源: {position}, 增加警戒: {effectiveIntensity}");

        if (effectiveIntensity > 1f) // 過濾掉太小的聲音
        {
            OnNoiseHeard?.Invoke(this, effectiveIntensity);
        }

        // 3. 設定調查點 (只有在 Searching 狀態才需要去調查，Alerted 會直接追殺)
        // ▼▼▼ [核心修改] 加入警戒值門檻判斷 ▼▼▼
        if (currentState != NpcState.Alerted && currentAlertLevel >= 100f)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(position, out navHit, 5.0f, NavMesh.AllAreas))
            {
                noiseInvestigationTarget = navHit.position;

                // 切換狀態！
                if (currentState != NpcState.Investigating)
                {
                    Debug.Log("NPC: 聽到可疑聲音，切換至調查模式！");
                    currentState = NpcState.Investigating;
                    investigationTimer = 0f; // 重置搜索計時
                }
                else
                {
                    Debug.Log("NPC: 調查中又聽到聲音，更新目標！");
                    // 如果已經在調查，就更新地點，但不重置計時器 (避免被連續聲音風箏)
                }
            }
        }
        else if (currentState == NpcState.Searching)
        {
            Debug.Log("NPC: 好像有聲音... 應該是錯覺 (警戒值 < 100，無視)");
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }

    // 巡邏
    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || agent == null || !agent.enabled || agent.isStopped) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            if (patrolPoints[currentPatrolIndex] != null)
            {
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            }
            else
            {
                Debug.LogWarning($"Patrol point {currentPatrolIndex} is null!");
                // Simple skip: try next one immediately in next valid update
                // Or add more complex logic to find next valid point
            }
        }
    }
}