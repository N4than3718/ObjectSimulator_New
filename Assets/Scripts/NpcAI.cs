using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    // --- 移除 Idle，Searching 現在是基礎狀態 ---
    public enum NpcState { Searching, Alerted }

    [Header("AI 狀態")]
    [SerializeField] private NpcState currentState = NpcState.Searching;

    [Header("巡邏設定")]
    public List<Transform> patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("警戒值設定")]
    [SerializeField] private float increaseRate = 30f; // 統一的上升速度
    [SerializeField] private float searchDecreaseRate = 15f; // Searching 狀態下的下降速度
    [SerializeField] private float alertDecreaseRate = 10f;  // Alerted 狀態下的下降速度
    [Tooltip("在 Searching 狀態下，多久沒看到動靜就開始降低警戒值")]
    [SerializeField] private float timeToStartDecreasing = 3f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("速度設定")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    // --- 私有變數 ---
    private FieldOfView fov;
    private NavMeshAgent navAgent;
    private Dictionary<Transform, Vector3> lastKnownPositions = new Dictionary<Transform, Vector3>();
    private float timeSinceLastSighting = 0f;
    private Vector3 lastSightingPosition;

    // ▼▼▼ 關鍵新增：用來鎖定威脅目標 ▼▼▼
    private Transform threatTarget;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void Awake()
    {
        fov = GetComponent<FieldOfView>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // 遊戲一開始就進入巡邏狀態
        currentState = NpcState.Searching;
        navAgent.speed = patrolSpeed;
    }

    void Update()
    {
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

    // --- 狀態邏輯 ---

    private void SearchingState()
    {
        navAgent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // 看到移動目標，增加警戒值
            currentAlertLevel += increaseRate * Time.deltaTime;
            timeSinceLastSighting = 0f;
        }
        else
        {
            // 沒看到，開始計時並下降警戒值
            timeSinceLastSighting += Time.deltaTime;
            if (timeSinceLastSighting >= timeToStartDecreasing)
            {
                currentAlertLevel -= searchDecreaseRate * Time.deltaTime;
            }
        }

        // 狀態轉換：當警戒值達到 200
        if (currentAlertLevel >= 200)
        {
            // ▼▼▼ 鎖定當前造成威脅的目標 ▼▼▼
            threatTarget = movingTarget;
            if (threatTarget != null)
            {
                currentState = NpcState.Alerted;
                Debug.Log($"狀態改變: Searching -> Alerted! 鎖定目標: {threatTarget.name}");
            }
            else
            {
                // 如果在達到200的瞬間目標剛好消失，做個保險，直接去最後的位置
                currentState = NpcState.Alerted;
                threatTarget = null; // 清空目標
                Debug.Log("狀態改變: Searching -> Alerted! 目標已消失，前往最後已知位置。");
            }
        }
    }

    private void AlertedState()
    {
        navAgent.speed = chaseSpeed;
        currentAlertLevel -= alertDecreaseRate * Time.deltaTime;

        // --- 核心修改：只追擊鎖定的 threatTarget ---
        if (threatTarget != null && fov.visibleTargets.Contains(threatTarget))
        {
            // 如果威脅目標還在視野內，就持續追擊它
            navAgent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // 持續更新最後看到它的位置

            if (Vector3.Distance(transform.position, threatTarget.position) < 1.5f)
            {
                Debug.Log($"抓住目標: {threatTarget.name}!");
                // 可以在這裡觸發抓住後的邏輯
            }
        }
        else
        {
            // 如果威脅目標不在視野內 (跟丟了)
            navAgent.SetDestination(lastSightingPosition);

            // 如果已經到達最後位置，就解除鎖定並回到搜索狀態
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                Debug.Log("目標丟失，解除警戒。");
                threatTarget = null;
                currentState = NpcState.Searching;
            }
        }

        // 狀態轉換：當警戒值降回 100 以下
        if (currentAlertLevel < 100)
        {
            Debug.Log("警戒值下降，解除警戒。");
            threatTarget = null; // 解除目標鎖定
            currentState = NpcState.Searching;
        }
    }

    // --- 輔助函式 ---

    // 這個函式現在回傳第一個被偵測到移動的目標
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
                lastSightingPosition = target.position;
            }
            lastKnownPositions[target] = target.position;
        }

        // 清理不在視野內的目標
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