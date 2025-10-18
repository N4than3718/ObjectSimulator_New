using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

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

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    // --- 私有變數 ---
    private FieldOfView fov;
    private NavMeshAgent navAgent;
    private Dictionary<Transform, Vector3> lastKnownPositions = new Dictionary<Transform, Vector3>();
    private float timeSinceLastSighting = 0f;
    private Vector3 lastSightingPosition;
    private Transform threatTarget;
    private TeamManager teamManager;

    void Awake()
    {
        fov = GetComponent<FieldOfView>();
        navAgent = GetComponent<NavMeshAgent>();
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("NpcAI cannot find TeamManager!");
    }

    void Start()
    {
        currentState = NpcState.Searching;
        navAgent.speed = patrolSpeed;
    }

    void Update()
    {
        if (teamManager == null) return;

        switch (currentState)
        {
            case NpcState.Searching:
                SearchingState();
                break;
            case NpcState.Alerted:
                AlertedState();
                break;
        }
        currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);
    }

    private void SearchingState()
    {
        navAgent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            if (currentAlertLevel < 100)
            {
                currentAlertLevel += lowAlertIncreaseRate * Time.deltaTime;
            }
            else
            {
                currentAlertLevel += mediumAlertIncreaseRate * Time.deltaTime;
            }
            timeSinceLastSighting = 0f;
        }
        else
        {
            if (currentAlertLevel < 100)
            {
                currentAlertLevel -= lowAlertDecreaseRate * Time.deltaTime;
            }
            else
            {
                timeSinceLastSighting += Time.deltaTime;
                if (timeSinceLastSighting >= timeToStartDecreasing)
                {
                    currentAlertLevel -= mediumAlertDecreaseRate * Time.deltaTime;
                }
            }
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
        navAgent.speed = chaseSpeed;
        currentAlertLevel -= highAlertDecreaseRate * Time.deltaTime;

        // 判斷當前鎖定的威脅目標是否還在視野內
        if (threatTarget != null && fov.visibleTargets.Contains(threatTarget))
        {
            // --- 情況 A: 目標還在視野內 ---
            navAgent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // 持續更新最後看到它的位置

            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"抓住目標: {threatTarget.name}!");
                // 通知 TeamManager 移除這個角色
                teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                // 抓住後，清除威脅目標並返回搜索狀態
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0; // 可以選擇清零或不清零
                return; // 重要：立刻結束這一幀的 AlertedState 邏輯
            }
        }
        else
        {
            // --- 情況 B: 目標已丟失 (不在視野內或不存在了) ---

            // 前往最後一次看到目標的位置進行搜索
            navAgent.SetDestination(lastSightingPosition);

            // 在前往搜索的途中，檢查是否有新的動靜
            Transform newMovingTarget = CheckForMovingTargets();
            if (newMovingTarget != null)
            {
                Debug.Log($"主要目標丟失！在前往調查時發現新目標: {newMovingTarget.name}");
                threatTarget = newMovingTarget; // 切換到新的威脅目標
                currentAlertLevel = 200f; // 重置警戒值，開始一次全新的追擊
                // 直接返回，避免執行下面的「到達目的地」判斷
                return;
            }

            // 如果已經到達最後已知位置，並且沒有發現新目標，則返回搜索狀態
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
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
            if (distanceMoved / Time.deltaTime > movementThreshold)
            {
                detectedMovingTarget = target;
                lastSightingPosition = target.position; // 只要看到移動，就更新最後動靜位置
            }
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

        if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
        {
            navAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        }
    }
}