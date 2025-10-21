using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent), typeof(Animator))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Debug 強制撿拾")] // <-- 加個標題
    [Tooltip("勾選此項會在遊戲開始時強制 NPC 撿拾下方指定的物件")]
    public bool forcePickupDebug = false;
    [Tooltip("拖曳場景中你想讓 NPC 強制撿拾的物件到這裡")]
    public Transform debugPickupTarget;

    [Header("Component References")] // 養成好習慣
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

    [Header("IK 設定")]
    [Tooltip("指定右手骨骼底下的 'GrabSocket' 空物件")]
    public Transform grabSocket;

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
    [Tooltip("在中警戒度下，多久沒看到動靜就開始降警戒")]
    [SerializeField] private float timeToStartDecreasing = 3f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("速度設定")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("捕捉設定")]
    [SerializeField] private float captureDistance = 1.5f; // 捕捉距離

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
    private Transform threatTarget;
    private Transform ikTargetPoint = null;
    private Transform objectToParent = null;
    private float handIKWeight = 0f;
    private float hintIKWeight = 0f;
    private bool _ikNeedsImmediateTermination = false;
    private TeamManager teamManager;
    private Vector3 _grabOffsetPosToApply;
    private Quaternion _grabOffsetRotToApply;
    private bool _needsTransformAlignment = false;
    private Transform _heldObjectRef = null;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        if (anim == null)
        {
            Debug.LogError("Animator not found!", this);
        }
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent not found!", this);
        }

        fov = GetComponent<FieldOfView>();
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("NpcAI cannot find TeamManager!");
    }

    void Start()
    {
        // === Debug 模式檢查 ===
        if (forcePickupDebug && debugPickupTarget != null)
        {
            // 如果勾了 Debug，就只跑 Debug 協程
            Debug.LogWarning($"--- DEBUG MODE: Forcing pickup of {debugPickupTarget.name} ---", this.gameObject);
            StartCoroutine(DebugPickupRoutine()); // 啟動 Debug 協程
        }
        else
        {
            // 如果沒勾 Debug，就正常啟動 AI
            currentState = NpcState.Searching;
            agent.speed = patrolSpeed;
            StartCoroutine(AIUpdateLoop()); // 啟動正常 AI 協程
        }
    }


    void Update()
    {
        UpdateAnimator();

        bool isPickingUp = anim.GetCurrentAnimatorStateInfo(0).IsName("Pick up");

        if (isPickingUp && ikTargetPoint != null)
        {
            // 正在撿：權重 -> 1
            handIKWeight = Mathf.Lerp(handIKWeight, 1.0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 1.0f, Time.deltaTime * 5f);
        }
        else
        {
            // 沒在撿：權重 -> 0
            handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);

            if (_ikNeedsImmediateTermination)
            {
                Debug.LogWarning("Update: Immediate termination flag detected, forcing handIKWeight = 0.");
                handIKWeight = 0f;
                hintIKWeight = 0f; // 手肘也一起歸零
                _ikNeedsImmediateTermination = false; // 重置旗標
            }
            // 否則，正常 Lerp 回 0
            else
            {
                handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
                hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);
            }
        }
    }

    // NpcAI.cs
    void LateUpdate()
    {
        // 檢查旗標以及物件引用是否存在
        if (_needsTransformAlignment && _heldObjectRef != null)
        {
            // 安全檢查：確保物件還在 Socket 底下
            if (_heldObjectRef.parent != grabSocket)
            {
                Debug.LogError($"LateUpdate Error: _heldObjectRef '{_heldObjectRef.name}' lost its parent '{grabSocket.name}'!", _heldObjectRef);
                // 重置狀態避免問題
                _needsTransformAlignment = false;
                _heldObjectRef = null;
                return;
            }

            Debug.LogWarning($"--- LateUpdate: Applying final alignment to {_heldObjectRef.name} ---");

            // --- A. 應用反向旋轉 ---
            _heldObjectRef.localRotation = Quaternion.Inverse(_grabOffsetRotToApply);
            Debug.Log($"LateUpdate Step A: Set localRotation = Inverse({_grabOffsetRotToApply.eulerAngles}) = {_heldObjectRef.localRotation.eulerAngles}");

            // --- B. 應用反向位置 ---
            // 注意：localScale 已經在 Event 中被正確設定了
            Vector3 rotatedAndScaledOffset = _heldObjectRef.localRotation * Vector3.Scale(_grabOffsetPosToApply, _heldObjectRef.localScale);
            _heldObjectRef.localPosition = -rotatedAndScaledOffset;
            Debug.Log($"LateUpdate Step B: Rotated/Scaled Offset = {rotatedAndScaledOffset}. Set localPosition = {-rotatedAndScaledOffset}");

            // --- Log 最終結果 ---
            Debug.LogError($"!!! LateUpdate Alignment Complete: Final LocalScale = {_heldObjectRef.localScale}, Final LossyScale = {_heldObjectRef.lossyScale}, Final LocalPos = {_heldObjectRef.localPosition} !!!");
            if (float.IsNaN(_heldObjectRef.localPosition.x)) { Debug.LogError("!!! LateUpdate Final Pos NaN !!!"); }

            // --- C. 重置狀態 ---
            _needsTransformAlignment = false;
            _heldObjectRef = null; // 清除引用
            _grabOffsetPosToApply = Vector3.zero; // 清除偏移量
            _grabOffsetRotToApply = Quaternion.identity;
            Debug.LogWarning("--- LateUpdate: Alignment finished and state reset ---");
        }
    }

    // ▼▼▼ 新增：AI 邏輯協程 ▼▼▼
    private IEnumerator AIUpdateLoop()
    {
        // 等待一小段時間讓場景完全載入
        yield return new WaitForSeconds(aiUpdateInterval);

        while (true)
        {
            if (teamManager == null)
            {
                // 如果找不到 TeamManager，就持續等待
                yield return new WaitForSeconds(aiUpdateInterval);
                continue;
            }

            // 執行當前狀態的邏輯
            switch (currentState)
            {
                case NpcState.Searching:
                    SearchingState();
                    break;
                case NpcState.Alerted:
                    AlertedState();
                    break;
            }

            // 確保警戒值在 0-200 之間
            currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);

            // 等待固定的時間後再執行下一次更新
            yield return new WaitForSeconds(aiUpdateInterval);
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private IEnumerator DebugPickupRoutine()
    {
        // 等待 NavMeshAgent 準備就緒
        //yield return new WaitForSeconds(0.1f);

        if (agent == null || debugPickupTarget == null)
        {
            Debug.LogError("Agent or Debug Target is null. Aborting debug pickup.", this.gameObject);
            yield break; // 結束協程
        }

        // 1. 設置目標並轉向
        //agent.SetDestination(debugPickupTarget.position);
        //transform.LookAt(debugPickupTarget.position);
        //Debug.Log($"--- DEBUG: Moving to {debugPickupTarget.name} at {debugPickupTarget.position} ---");

        // 2. 等待抵達
        //    (agent.pathPending 檢查它是否還在計算路徑)
        //while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            //yield return null; // 每幀檢查一次
        }

        // 3. 已抵達，執行撿拾
        Debug.Log("--- DEBUG: Reached target, triggering pickup animation ---");

        // 呼叫我們修改過的 TriggerPickup 函式
        // (它現在會自動停止 agent)
        TriggerPickup(debugPickupTarget);
    }

    public void TriggerPickup(Transform targetRoot)
    {
        if (agent != null) agent.isStopped = true;

        // --- NEW LOGIC ---
        objectToParent = targetRoot; // 儲存要 parent 的根物件

        // 嘗試尋找 "GrabPoint"
        Transform grabPoint = targetRoot.Find("GrabPoint");
        if (grabPoint != null)
        {
            ikTargetPoint = grabPoint; // 找到了！IK 瞄準這裡
        }
        else
        {
            // 沒找到，就用根物件 (這會導致浮空，但至少不會 crash)
            Debug.LogWarning($"Object {targetRoot.name} lacks a 'GrabPoint' child. IK may be inaccurate.", targetRoot);
            ikTargetPoint = targetRoot;
        }
        // --- END NEW LOGIC ---

        anim.SetTrigger("Pick up"); //
        transform.LookAt(ikTargetPoint.position); // 看向抓握點
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        // --- 原有的檢查 ---
        // 如果 IK 目標是 null (已經被 Event 清掉)，或者權重是 0，就退出
        if (ikTargetPoint == null || handIKWeight <= 0)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            // ... (其他權重設為 0) ...
            return;
        }

        // --- ▼▼▼ 新增的雙重保險 ▼▼▼ ---
        // 即使 ikTargetPoint 還沒被 null (時機問題?)，
        // 但如果 objectToParent 已經成功被 Parent 到 grabSocket 底下，
        // 那也絕對不能再執行 SetIKPosition！
        // (注意: 我們需要檢查 objectToParent 本身是否為 null，以防萬一)
        if (objectToParent != null && objectToParent.parent == grabSocket)
        {
            Debug.LogWarning("OnAnimatorIK: Object already parented, but ikTargetPoint wasn't null? Forcing IK termination.", this.gameObject);
            // 強制把權重設為 0，阻止後續的 SetIKPosition
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            // ... (其他權重設為 0) ...
            // (我們甚至可以考慮在這裡強制 null 掉 ikTargetPoint)
            // ikTargetPoint = null; 
            return; // <--- 提前退出！
        }

        // --- 1. 設置手的 IK ---
        // 設置 IK 權重 (0 到 1)
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight); // 順便對齊旋轉

        // 設置 IK 的目標位置和旋轉
        // (你可能需要在 ikTarget 上加一個 "GrabPoint" 空物件來抓得更準)
        anim.SetIKPosition(AvatarIKGoal.RightHand, ikTargetPoint.position);
        anim.SetIKRotation(AvatarIKGoal.RightHand, ikTargetPoint.rotation);

        // --- 2. 設置手肘提示 (Hint) ---
        // 這是 Pro-Tip：告訴手肘該往哪個方向彎，才不會折到背後去
        // 在 NPC 模型的肩膀右前方放一個空物件，命名為 "RightElbowHint"
        Transform rightElbowHint = FindRecursive("RightElbowHint"); // (你需要自己實作這個查找)

        if (rightElbowHint != null)
        {
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintIKWeight);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);
        }
    }

    // (你需要一個輔助函式來找到子物件，或者直接 public 拖進來)
    private Transform FindRecursive(string name)
    {
        // 簡易版：假設它在第一層
        return transform.Find(name);
    }

    public void AnimationEvent_GrabObject()
    {
        Debug.LogError("!!! AnimationEvent_GrabObject CALLED !!!");

        if (grabSocket == null) { Debug.LogError("!!! GrabSocket IS NULL, returning !!!"); return; }

        // 檢查必要變數 (這次 ikTargetPoint 在 Parent 後才 null)
        if (objectToParent != null && ikTargetPoint != null)
        {
            Debug.Log($"Grab Logic Starting: Obj='{objectToParent.name}', Point='{ikTargetPoint.name}'");

            // --- 1. 關閉物理 ---
            // ... (Rigidbody, Collider disabled code) ...
            Debug.Log("Step 1: Physics disabled.");

            // --- 2. 在 Parent 之前，儲存本地偏移量供 LateUpdate 使用 ---
            _grabOffsetPosToApply = (ikTargetPoint == objectToParent) ? Vector3.zero : ikTargetPoint.localPosition;
            _grabOffsetRotToApply = (ikTargetPoint == objectToParent) ? Quaternion.identity : ikTargetPoint.localRotation;
            Debug.Log($"Step 2: Stored grabOffset_Pos: {_grabOffsetPosToApply}, grabOffset_Rot: {_grabOffsetRotToApply.eulerAngles}");
            if (float.IsNaN(_grabOffsetPosToApply.x)) { Debug.LogError("!!! Offset Pos NaN !!!"); return; }


            // --- 3. 執行 Parent & 儲存引用 ---
            Vector3 parentLossyScale = grabSocket.lossyScale;
            _heldObjectRef = objectToParent; // <--- 在 Parent 前儲存引用
            objectToParent.SetParent(grabSocket, true);
            Debug.LogError($"!!! SetParent Called! Parent: {_heldObjectRef.parent?.name}. Object: {_heldObjectRef.name} !!!");


            // --- 4. 只修正 Scale (防止下一幀爆炸) ---
            Vector3 inverseScale = Vector3.one;
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            if (Mathf.Abs(parentLossyScale.y) > 1e-6f) inverseScale.y = 1.0f / parentLossyScale.y;
            if (Mathf.Abs(parentLossyScale.z) > 1e-6f) inverseScale.z = 1.0f / parentLossyScale.z;
            _heldObjectRef.localScale = inverseScale; // <--- 用 _heldObjectRef
            Debug.Log($"Step 4 (Immediate): Set localScale = {inverseScale}. Current LossyScale = {_heldObjectRef.lossyScale}");

            // --- 5. 設定旗標，讓 LateUpdate 接手 ---
            _needsTransformAlignment = true;
            Debug.LogWarning("!!! Flagging _needsTransformAlignment = true for LateUpdate !!!");


            // --- 6. 立即強制關閉 IK & Null 變數 ---
            Debug.LogWarning("!!! Force setting handIKWeight = 0f !!!");
            handIKWeight = 0f;
            // _ikNeedsImmediateTermination = true; // Update 中的旗標現在不太需要了，但留著也無妨

            ikTargetPoint = null;
            objectToParent = null; // <--- objectToParent 在這裡才 null
            Debug.Log("Step 6: IK variables nulled.");

            Debug.Log("--- Grab Event ENDED (Alignment pending in LateUpdate) ---");
        }
        else
        {
            Debug.LogError($"!!! Grab Logic SKIPPED! [...] !!!");
        }
    }

    // (你還需要一個動畫事件在動畫結束時，把 ikTarget 設為 null)
    public void AnimationEvent_PickupEnd()
    {
        ikTargetPoint = null;
        objectToParent = null;
        _heldObjectRef = null; // <--- 確保這裡也清除
        _needsTransformAlignment = false; // (保險) 順便重置旗標
        if (agent != null) agent.isStopped = false;
    }

    private void UpdateAnimator()
    {
        if (agent == null || anim == null) return;

        // 1. 獲取 NavMeshAgent 想要的速度 (desiredVelocity) 或實際速度 (velocity)
        //    我們用 velocity.magnitude 來獲取當前實際的移動速率
        float currentSpeed = agent.velocity.magnitude;

        // 2. 為了讓 Animator 的 Speed 參數在 0-1 之間 (如果你的 agent speed 不是 1 的話)，
        //    最好做一個正規化 (Normalize)
        //    (假設你在 agent 設置裡 speed 是 3.5f)
        float normalizedSpeed = currentSpeed / agent.speed;

        // 3. 把這個值傳給 Animator
        //    使用 SetFloat 的 Damp Time (e.g., 0.1f) 可以讓動畫過渡更平滑，防止急停
        anim.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
    }

    private void SearchingState()
    {
        agent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // ▼▼▼ 修改：使用 aiUpdateInterval 取代 Time.deltaTime ▼▼▼
            if (currentAlertLevel < 100)
            {
                currentAlertLevel += lowAlertIncreaseRate * aiUpdateInterval;
            }
            else
            {
                currentAlertLevel += mediumAlertIncreaseRate * aiUpdateInterval;
            }
            timeSinceLastSighting = 0f;
        }
        else
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
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }

        if (currentAlertLevel >= 200)
        {
            threatTarget = movingTarget;
            if (threatTarget != null)
            {
                currentState = NpcState.Alerted;
                Debug.Log($"狀態改變: Searching -> Alerted! 鎖定目標: {threatTarget.name}");
            }
        }
    }

    private void AlertedState()
    {
        agent.speed = chaseSpeed;

        // ▼▼▼ 修改：使用 aiUpdateInterval 取代 Time.deltaTime ▼▼▼
        currentAlertLevel -= highAlertDecreaseRate * aiUpdateInterval;
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        // --- 優化：在狀態開始時先檢查一次移動目標 ---
        Transform currentlyVisibleMovingTarget = CheckForMovingTargets();
        bool threatIsVisible = (threatTarget != null && fov.visibleTargets.Contains(threatTarget));

        // --- 情況 A: 威脅目標還在視野內 ---
        if (threatIsVisible)
        {
            agent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // 持續更新最後看到它的位置

            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"抓住目標: {threatTarget.name}!");
                TriggerPickup(threatTarget.transform);
                teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0; // 抓到後清零警戒
                return; // 立刻結束此狀態邏輯
            }
        }
        // --- 情況 B: 威脅目標已丟失 (不在視野內) ---
        else
        {
            // 前往最後一次看到目標的位置進行搜索
            agent.SetDestination(lastSightingPosition);

            // --- 情況 C: 在前往途中，看到了 *新的* 移動目標 (不是原本的威脅目標) ---
            if (currentlyVisibleMovingTarget != null && currentlyVisibleMovingTarget != threatTarget)
            {
                Debug.Log($"主要目標丟失！在前往調查時發現新目標: {currentlyVisibleMovingTarget.name}");
                threatTarget = currentlyVisibleMovingTarget; // 切換到新的威WH目標
                currentAlertLevel = 200f; // 重置警戒值，開始一次全新的追擊
                return; // 結束此邏輯，下一個 AIUpdate 迴圈將會執行(情況A)
            }

            // --- 情況 D: 如果已經到達最後已知位置，並且沒有發現新目標，則返回搜索狀態 ---
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                Debug.Log("在最後已知位置未發現目標，返回搜索狀態。");
                threatTarget = null;
                currentState = NpcState.Searching;
            }
        }

        // 如果警戒值自然下降到 100 以下，也返回搜索狀態
        if (currentAlertLevel < 100)
        {
            Debug.Log("警戒值下降，解除警戒狀態。");
            threatTarget = null;
            currentState = NpcState.Searching;
        }
    }

    private Transform CheckForMovingTargets()
    {
        Transform detectedMovingTarget = null;
        foreach (Transform target in fov.visibleTargets)
        {
            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue;
            }
            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);

            // ▼▼▼ 修改：使用 aiUpdateInterval 取代 Time.deltaTime ▼▼▼
            // 偵測速度 (每秒移動距離)
            if (distanceMoved / aiUpdateInterval > movementThreshold)
            {
                detectedMovingTarget = target;
                lastSightingPosition = target.position; // 只要看到移動，就更新最後動靜位置
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            lastKnownPositions[target] = target.position;
        }

        var targetsToForget = new List<Transform>();
        foreach (var pair in lastKnownPositions) { if (!fov.visibleTargets.Contains(pair.Key)) targetsToForget.Add(pair.Key); }
        foreach (Transform target in targetsToForget) { lastKnownPositions.Remove(target); }

        return detectedMovingTarget;
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        }
    }
}