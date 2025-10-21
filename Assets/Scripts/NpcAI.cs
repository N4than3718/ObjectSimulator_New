using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent), typeof(Animator))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Debug 強制撿拾")]
    [Tooltip("勾選此項會在遊戲開始時強制 NPC 撿拾下方指定的物件")]
    public bool forcePickupDebug = false;
    [Tooltip("拖曳場景中你想讓 NPC 強制撿拾的物件到這裡")]
    public Transform debugPickupTarget;

    [Header("Component References")]
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

    [Header("IK 設定")]
    [Tooltip("指定右手骨骼底下的 'GrabSocket' 空物件")]
    public Transform grabSocket;
    [Tooltip("Optional: For elbow hint calculation")] // 可以加上手肘提示點的說明
    public Transform rightElbowHint; // 如果 FindRecursive 不可靠，可以考慮用 public 欄位指定

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
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("速度設定")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("捕捉設定")]
    [SerializeField] private float captureDistance = 1.5f;

    [Header("效能設定")]
    [Tooltip("AI 決策邏輯的更新間隔 (秒)")]
    [SerializeField] private float aiUpdateInterval = 0.2f;

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    // --- 私有變數 ---
    private FieldOfView fov;
    private Dictionary<Transform, Vector3> lastKnownPositions = new Dictionary<Transform, Vector3>();
    private float timeSinceLastSighting = 0f;
    private Vector3 lastSightingPosition;
    private Transform threatTarget = null; // 初始化為 null 比較好

    // IK & Grab 相關
    private Transform ikTargetPoint = null;     // IK 伸手瞄準的目標點 (可能是 GrabPoint 或物件 Root)
    private Transform objectToGrab = null;      // 從 TriggerPickup 傳遞到 AnimationEvent 的臨時指標
    private float handIKWeight = 0f;
    private float hintIKWeight = 0f;

    // 物件跟隨 (Follow) 相關
    private bool _isHoldingObject = false;
    private Transform _heldObjectRef = null;    // 當前實際抓著的物件引用
    private Vector3 _holdOffsetPosition = Vector3.zero; // 相對於 grabSocket 的本地位置偏移
    private Quaternion _holdOffsetRotation = Quaternion.identity; // 相對於 grabSocket 的本地旋轉偏移
    private Transform _pointToAlignWithSocket = null;

    private TeamManager teamManager;

    void Awake()
    {
        // 獲取必要的組件
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        fov = GetComponent<FieldOfView>(); // 在這裡獲取就好

        // 錯誤檢查
        if (anim == null) Debug.LogError("Animator not found!", this);
        if (agent == null) Debug.LogError("NavMeshAgent not found!", this);
        if (fov == null) Debug.LogError("FieldOfView not found!", this);

        teamManager = FindAnyObjectByType<TeamManager>(); // Unity 2023+ 建議用 FindAnyObjectByType
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
            currentState = NpcState.Searching;
            agent.speed = patrolSpeed;
            if (patrolPoints.Count > 0) // 確保有巡邏點再開始
            {
                agent.SetDestination(patrolPoints[0].position);
            }
            StartCoroutine(AIUpdateLoop());
        }
    }

    void Update()
    {
        // 更新 Animator 速度參數
        UpdateAnimator();

        // 平滑更新 IK 權重 (只在伸手階段需要)
        // isPickingUp 檢查 State Name 是否精確匹配 Animator Controller 裡的 State Name
        bool isPickingUp = anim.GetCurrentAnimatorStateInfo(0).IsName("Pick up");

        // 只有在準備伸手 (ikTargetPoint 存在) 且動畫在播放時才增加權重
        if (isPickingUp && ikTargetPoint != null && !_isHoldingObject) // 添加 !_isHoldingObject 判斷
        {
            handIKWeight = Mathf.Lerp(handIKWeight, 1.0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 1.0f, Time.deltaTime * 5f); // 手肘提示
        }
        else
        {
            // 其他情況 (沒在撿 / 已經抓著 / IK 目標已 null) 都讓權重歸零
            handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);
        }
        // 注意：之前版本有個 _ikNeedsImmediateTermination 旗標，
        // 在目前的 Follow 邏輯下 (Event 直接設 handIKWeight=0)，應該不再需要
    }

    void LateUpdate()
    {
        if (_isHoldingObject && _heldObjectRef != null && grabSocket != null && _pointToAlignWithSocket != null)
        {
            // --- 計算需要施加到根物件上的位移和旋轉 ---

            // 目標：讓 _pointToAlignWithSocket 的世界座標/旋轉 等於 grabSocket 的世界座標/旋轉

            // 1. 計算當前 "對齊點" 與 "目標 Socket" 之間的旋轉差
            Quaternion rotationDifference = grabSocket.rotation * Quaternion.Inverse(_pointToAlignWithSocket.rotation);

            // 2. 將這個旋轉差應用到根物件上
            _heldObjectRef.rotation = rotationDifference * _heldObjectRef.rotation;

            // 3. 計算應用旋轉後，"對齊點" 現在的位置 與 "目標 Socket" 之間的位置差
            Vector3 positionDifference = grabSocket.position - _pointToAlignWithSocket.position;

            // 4. 將這個位置差應用到根物件上
            _heldObjectRef.position += positionDifference;

            // --- (可選) 持續強制 Scale ---
            // 如果 Scale 在其他地方被意外修改，可以在這裡再次強制
            
            Vector3 parentLossyScale = grabSocket.lossyScale;
            Vector3 inverseScale = Vector3.one;
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            // ... (y, z checks) ...
            _heldObjectRef.localScale = inverseScale;
            

            // --- Log 驗證 ---
            // Debug.Log($"LateUpdate: Socket Pos={grabSocket.position.ToString("F3")}, PointToAlign Pos={_pointToAlignWithSocket.position.ToString("F3")}, Root Pos={_heldObjectRef.position.ToString("F3")}");
        }
    }

    private IEnumerator AIUpdateLoop()
    {
        yield return new WaitForSeconds(aiUpdateInterval); // 初始延遲

        while (true)
        {
            if (teamManager == null) // 持續檢查 TeamManager 是否存在
            {
                teamManager = FindAnyObjectByType<TeamManager>();
                if (teamManager == null)
                {
                    Debug.LogWarning("AIUpdateLoop waiting for TeamManager...");
                    yield return new WaitForSeconds(1f); // 如果找不到，等待更長時間
                    continue;
                }
            }

            // 根據當前狀態執行邏輯
            switch (currentState)
            {
                case NpcState.Searching:
                    SearchingState();
                    break;
                case NpcState.Alerted:
                    AlertedState();
                    break;
            }

            // 限制警戒值範圍
            currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);

            // 等待下一次更新
            yield return new WaitForSeconds(aiUpdateInterval);
        }
    }

    private IEnumerator DebugPickupRoutine()
    {

        if (debugPickupTarget == null)
        {
            Debug.LogError("Debug Target is null.", this.gameObject);
            yield break;
        }

        // --- 直接觸發撿拾 ---
        // (可選) 讓 NPC 立刻轉向目標
        // Check if agent is enabled before using LookAt that relies on transform update potentially
        if (agent != null && agent.enabled)
        {
            // If NPC needs to turn, ensure agent updates position/rotation first
            // Maybe wait a frame or adjust update order if LookAt is unreliable here
            transform.LookAt(debugPickupTarget.position);
            yield return null; // Wait one frame for rotation to potentially apply
        }
        else if (agent == null || !agent.enabled)
        {
            // Simple LookAt if agent is off
            transform.LookAt(debugPickupTarget.position);
        }


        Debug.Log("--- DEBUG: Triggering pickup animation immediately ---");
        TriggerPickup(debugPickupTarget);
    }

    // 觸發撿拾流程 (由 AI 狀態或 Debug 調用)
    public void TriggerPickup(Transform targetRoot)
    {
        if (targetRoot == null)
        {
            Debug.LogError("TriggerPickup called with null targetRoot!");
            return;
        }

        // 停止移動 (如果 Agent 存在且啟用)
        if (agent != null && agent.enabled) // Check if agent exists and is enabled
        {
            agent.isStopped = true;
        }


        objectToGrab = targetRoot; // 儲存物件根節點，供 AnimationEvent 使用

        // 尋找抓握點 GrabPoint
        Transform grabPoint = targetRoot.Find("GrabPoint");
        if (grabPoint != null)
        {
            ikTargetPoint = grabPoint; // IK 瞄準 GrabPoint
            Debug.Log($"TriggerPickup: Found GrabPoint for {targetRoot.name}. IK targeting GrabPoint.");
        }
        else
        {
            // 沒找到 GrabPoint，IK 直接瞄準物件根節點
            Debug.LogWarning($"Object {targetRoot.name} lacks a 'GrabPoint' child. IK targeting object root. Position might be inaccurate.", targetRoot);
            ikTargetPoint = targetRoot;
        }

        // 觸發 Animator 中的 "Pick up" Trigger
        if (anim != null) // Check if anim exists
        {
            anim.SetTrigger("Pick up");
        }
        else
        {
            Debug.LogError("Animator is null, cannot set trigger 'Pick up'.");
            return; // Cannot proceed without animator
        }


        // 讓 NPC 朝向 IK 目標點 (視覺效果)
        if (ikTargetPoint != null) // Check if ikTargetPoint was successfully assigned
        {
            transform.LookAt(ikTargetPoint.position);
        }
    }

    // Unity 在 Animator IK Pass 啟用時自動調用
    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return; // 確保 Animator 存在

        // IK 條件檢查：目標點存在 且 IK權重大於0
        if (ikTargetPoint == null || handIKWeight <= 0)
        {
            // 如果不滿足條件，確保所有相關 IK 權重歸零
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
            return; // 提前退出，不執行 IK 計算
        }

        // --- 執行右手 IK ---
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight); // 同步位置和旋轉權重
        anim.SetIKPosition(AvatarIKGoal.RightHand, ikTargetPoint.position); // 設定 IK 目標位置
        anim.SetIKRotation(AvatarIKGoal.RightHand, ikTargetPoint.rotation); // 設定 IK 目標旋轉

        // --- (可選) 執行右手肘提示點 IK ---
        // 這有助於控制手臂彎曲方向，避免穿插
        // 需要在場景中 NPC 模型下創建名為 "RightElbowHint" 的空物件作為提示點
        Transform rightElbowHint = FindRecursive("RightElbowHint"); // 查找提示點
        if (rightElbowHint != null)
        {
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintIKWeight); // 設定提示點權重
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position); // 設定提示點位置
        }
    }

    // 查找子物件 (簡易版，可根據需要擴展為遞迴查找)
    private Transform FindRecursive(string name)
    {
        // 查找直接子物件
        Transform child = transform.Find(name);
        if (child != null) return child;

        // 如果需要遞迴查找所有子層級 (效能考量，謹慎使用)
        /*
        foreach (Transform t in transform)
        {
            child = t.Find(name); // 查找孫代
            if (child != null) return child;
            // 可以繼續遞迴...
        }
        */
        return null; // 找不到返回 null
    }

    // 由 "Pick up" 動畫片段中的事件觸發
    public void AnimationEvent_GrabObject()
    {
        if (grabSocket == null) { Debug.LogError("GrabSocket IS NULL!", this.gameObject); return; }

        // 檢查從 TriggerPickup 傳來的 objectToGrab (物件根)
        if (objectToGrab != null)
        {
            _heldObjectRef = objectToGrab; // 儲存根物件引用

            // --- 確定要對齊哪個點 ---
            Transform potentialGrabPoint = objectToGrab.Find("GrabPoint");
            _pointToAlignWithSocket = (potentialGrabPoint != null) ? potentialGrabPoint : objectToGrab; // 儲存要對齊的點
            Debug.Log($"Grab Logic Starting (Follow Mode): Holding '{_heldObjectRef.name}', Aligning '{_pointToAlignWithSocket.name}'");


            // --- 1. 關閉物理 ---
            Rigidbody rb = _heldObjectRef.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            Collider col = _heldObjectRef.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }
            // Debug.Log("Step 1: Physics disabled.");

            // --- 2. 計算初始 Scale 修正 (讓世界 Scale 變 1) ---
            //    (不再需要計算 Offset，LateUpdate 會處理對齊)
            Vector3 parentLossyScale = grabSocket.lossyScale;
            Vector3 inverseScale = Vector3.one;
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            if (Mathf.Abs(parentLossyScale.y) > 1e-6f) inverseScale.y = 1.0f / parentLossyScale.y;
            if (Mathf.Abs(parentLossyScale.z) > 1e-6f) inverseScale.z = 1.0f / parentLossyScale.z;
            _heldObjectRef.localScale = inverseScale; // <--- 在 Event 裡先應用 Scale
                                                      // Debug.Log($"Step 2: Applied inverse localScale = {inverseScale}");

            // --- 3. 設定跟隨狀態 ---
            _isHoldingObject = true;
            // Debug.LogWarning($"!!! Set _isHoldingObject = true. Held Object: {_heldObjectRef.name} !!!");
            // --- NO SetParent HERE ---


            // --- 4. 立即強制關閉 IK & 清除臨時變數 ---
            // Debug.LogWarning("!!! Force setting handIKWeight = 0f !!!");
            handIKWeight = 0f;

            ikTargetPoint = null; // 清除 IK 目標
            objectToGrab = null;  // 清除臨時傳遞變數
            // Debug.Log("Step 4: IK variables nulled.");

            // Debug.Log("--- Grab Event ENDED (Follow Mode) ---");
        }
        else
        {
            Debug.LogError($"!!! Grab Logic SKIPPED! objectToGrab was NULL when event triggered !!!");
        }
    }

    // 由 "Pick up" 動畫片段結尾的事件觸發 (或由其他邏輯調用以放開物體)
    public void AnimationEvent_PickupEnd()
    {
        Transform objectToRelease = _heldObjectRef;

        _isHoldingObject = false;
        _heldObjectRef = null;
        _pointToAlignWithSocket = null; // <-- 清除對齊點引用

        // ... (重新啟用物理的程式碼) ...

        ikTargetPoint = null;
        objectToGrab = null;

        if (agent != null && agent.enabled) agent.isStopped = false;
    }

    // 更新 Animator 中的 Speed 參數
    private void UpdateAnimator()
    {
        // 檢查組件是否存在
        if (agent == null || anim == null) return;

        // 如果 Agent 被禁用，速度視為 0
        if (!agent.enabled)
        {
            anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
            return;
        }

        // 計算當前速度和正規化速度
        float currentSpeed = agent.velocity.magnitude;
        // 防止除以零 (如果 agent.speed 是 0)
        float normalizedSpeed = agent.speed > 0 ? (currentSpeed / agent.speed) : 0f;

        // 更新 Animator 參數，使用 Damp Time 平滑過渡
        anim.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
    }

    // Searching 狀態邏輯
    private void SearchingState()
    {
        agent.speed = patrolSpeed; // 確保速度是巡邏速度
        Patrol(); // 執行巡邏

        // 檢查視野內是否有移動目標
        Transform movingTarget = CheckForMovingTargets();

        // 根據是否看到移動目標，調整警戒值
        if (movingTarget != null)
        {
            // 看到目標，增加警戒
            float increaseRate = (currentAlertLevel < 100) ? lowAlertIncreaseRate : mediumAlertIncreaseRate;
            currentAlertLevel += increaseRate * aiUpdateInterval;
            timeSinceLastSighting = 0f; // 重置上次看到時間
            lastSightingPosition = movingTarget.position; // 更新最後動靜位置
        }
        else
        {
            // 沒看到目標，降低警戒
            if (currentAlertLevel < 100)
            {
                currentAlertLevel -= lowAlertDecreaseRate * aiUpdateInterval;
            }
            else
            {
                // 中警戒度以上，需要一段時間沒動靜才開始降低
                timeSinceLastSighting += aiUpdateInterval;
                if (timeSinceLastSighting >= timeToStartDecreasing)
                {
                    currentAlertLevel -= mediumAlertDecreaseRate * aiUpdateInterval;
                }
            }
        }

        // 如果警戒值達到最高，切換到 Alerted 狀態
        if (currentAlertLevel >= 200 && movingTarget != null) // 確保有目標才切換
        {
            threatTarget = movingTarget; // 設定威脅目標
            currentState = NpcState.Alerted;
            Debug.Log($"狀態改變: Searching -> Alerted! 鎖定目標: {threatTarget.name}");
            // (可選) 在這裡可以觸發一些進入警戒狀態的音效或動畫
        }
    }

    // Alerted 狀態邏輯
    private void AlertedState()
    {
        agent.speed = chaseSpeed; // 切換到追擊速度

        // 警戒值隨時間自然下降
        currentAlertLevel -= highAlertDecreaseRate * aiUpdateInterval;

        // 檢查威脅目標是否存在且可見
        bool threatIsStillVisible = (threatTarget != null && fov.visibleTargets.Contains(threatTarget));

        if (threatIsStillVisible)
        {
            // A: 目標可見，持續追擊
            agent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // 更新最後看到位置
            timeSinceLastSighting = 0f; // 重置計時器

            // 檢查是否進入捕捉範圍
            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"抓住目標: {threatTarget.name}!");
                // (重要) 抓取前先從 TeamManager 移除，避免潛在衝突
                if (teamManager != null) teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                TriggerPickup(threatTarget); // 觸發撿拾 (注意: TriggerPickup 會 stop agent)

                // 重置狀態
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0;
                // 注意: agent.isStopped 在 PickupEnd 事件中會設回 false
                return; // 結束本次 Alerted 邏輯
            }
        }
        else
        {
            // B: 目標丟失
            // 前往最後已知位置
            if (agent.destination != lastSightingPosition) // 避免重複設置
            {
                agent.SetDestination(lastSightingPosition);
            }


            // C: 在前往途中看到新的移動目標
            Transform newMovingTarget = CheckForMovingTargets(); // 再次檢查視野
            if (newMovingTarget != null && newMovingTarget != threatTarget) // 確保是新目標
            {
                Debug.Log($"主要目標 {threatTarget?.name ?? "NULL"} 丟失！發現新目標: {newMovingTarget.name}");
                threatTarget = newMovingTarget; // 切換威脅
                currentAlertLevel = 200f; // 重置警戒，全力追擊新目標
                lastSightingPosition = threatTarget.position; // 更新最後位置
                timeSinceLastSighting = 0f; // 重置計時
                // 不需要 return，下一輪會直接進入情況 A
            }
            // D: 已到達最後位置，且未發現新目標
            else if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance) // 用 stoppingDistance 判斷更準確
            {
                // 如果一小段時間內沒再看到目標，則放棄
                timeSinceLastSighting += aiUpdateInterval;
                if (timeSinceLastSighting > timeToStartDecreasing * 2f) // 放棄時間可以設長一點
                {
                    Debug.Log("在最後已知位置搜索無果，返回搜索狀態。");
                    threatTarget = null;
                    currentState = NpcState.Searching;
                    // agent.isStopped 在 SearchingState 開始時會被 Patrol() 處理
                }
            }
        }

        // 如果警戒值自然降到一定程度，也返回搜索 (例如 < 50?)
        if (currentAlertLevel < 100) // 閾值可以調整
        {
            Debug.Log("警戒值下降，解除警戒狀態。");
            threatTarget = null;
            currentState = NpcState.Searching;
        }
    }

    // 檢查視野內是否有物體在移動
    private Transform CheckForMovingTargets()
    {
        Transform detectedMovingTarget = null;
        if (fov == null || fov.visibleTargets == null) return null; // 防錯

        foreach (Transform target in fov.visibleTargets)
        {
            if (target == null) continue; // 防錯

            // 首次看到，記錄位置
            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue;
            }

            // 計算移動距離
            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);

            // 判斷是否超過移動閾值 (考慮更新間隔)
            if (aiUpdateInterval > 0 && (distanceMoved / aiUpdateInterval) > movementThreshold)
            {
                // 發現移動目標！
                detectedMovingTarget = target;
                // lastSightingPosition = target.position; // 在狀態邏輯裡更新更合適
                // Debug.Log($"Detected moving target: {target.name}, Speed: {distanceMoved / aiUpdateInterval}");
            }

            // 更新目標的已知位置
            lastKnownPositions[target] = target.position;
        }

        // --- 清理已離開視野的目標記錄 ---
        // 使用 List 避免在迭代時修改 Dictionary
        List<Transform> targetsToForget = new List<Transform>();
        foreach (var pair in lastKnownPositions)
        {
            // 如果記錄中的目標不在當前可見目標列表裡
            if (!fov.visibleTargets.Contains(pair.Key))
            {
                targetsToForget.Add(pair.Key);
            }
        }
        // 從 Dictionary 中移除這些目標
        foreach (Transform target in targetsToForget)
        {
            lastKnownPositions.Remove(target);
        }

        return detectedMovingTarget;
    }

    // 控制 NPC 在巡邏點之間移動
    private void Patrol()
    {
        // 檢查是否有巡邏點以及 Agent 是否就緒
        if (patrolPoints == null || patrolPoints.Count == 0 || agent == null || !agent.enabled || agent.isStopped) return;

        // 如果已到達目的地 (或接近目的地)
        // 使用 stoppingDistance 判斷更可靠
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // 前往下一個巡邏點
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            // 檢查下一個巡邏點是否存在
            if (patrolPoints[currentPatrolIndex] != null)
            {
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                // Debug.Log($"Patrolling to point {currentPatrolIndex}: {patrolPoints[currentPatrolIndex].name}");
            }
            else
            {
                Debug.LogWarning($"Patrol point at index {currentPatrolIndex} is null!");
                // 可以選擇跳過這個點或停止巡邏
                // currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count; // 跳過
            }

        }
    }
}