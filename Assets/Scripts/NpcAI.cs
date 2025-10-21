using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Component References")] // 養成好習慣
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

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
    private TeamManager teamManager;

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
        currentState = NpcState.Searching;
        agent.speed = patrolSpeed;

        // ▼▼▼ 修改：啟動 AI 邏輯協程，取代 Update() ▼▼▼
        StartCoroutine(AIUpdateLoop());
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }


    void Update()
    {
        UpdateAnimator();
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