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
    [Tooltip("低警戒度(0-99)下的警戒值上升速度")]
    [SerializeField] private float lowAlertIncreaseRate = 20f;
    [Tooltip("低警戒度(0-99)下的警戒值下降速度")]
    [SerializeField] private float lowAlertDecreaseRate = 10f;
    [Tooltip("中警戒度(100-199)下的警戒值上升速度")]
    [SerializeField] private float mediumAlertIncreaseRate = 40f; // 速度提升
    [Tooltip("中警戒度(100-199)下的警戒值下降速度")]
    [SerializeField] private float mediumAlertDecreaseRate = 15f;
    [Tooltip("高警戒度(200)下的警戒值下降速度")]
    [SerializeField] private float highAlertDecreaseRate = 10f;
    [Tooltip("在中警戒度下，多久沒看到動靜就開始降警戒")]
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
    private Transform threatTarget;

    void Awake()
    {
        fov = GetComponent<FieldOfView>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
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

    private void SearchingState()
    {
        navAgent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // --- 核心修改：根據警戒等級使用不同上升速度 ---
            if (currentAlertLevel < 100)
            {
                currentAlertLevel += lowAlertIncreaseRate * Time.deltaTime;
            }
            else
            {
                // 進入中警戒，加速上升
                currentAlertLevel += mediumAlertIncreaseRate * Time.deltaTime;
            }
            timeSinceLastSighting = 0f;
        }
        else
        {
            // --- 核心修改：根據警戒等級使用不同下降邏輯 ---
            if (currentAlertLevel < 100)
            {
                // 低警戒，直接下降
                currentAlertLevel -= lowAlertDecreaseRate * Time.deltaTime;
            }
            else
            {
                // 中警戒，計時後才下降
                timeSinceLastSighting += Time.deltaTime;
                if (timeSinceLastSighting >= timeToStartDecreasing)
                {
                    currentAlertLevel -= mediumAlertDecreaseRate * Time.deltaTime;
                }
            }
        }

        // 狀態轉換
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

        if (threatTarget != null && fov.visibleTargets.Contains(threatTarget))
        {
            navAgent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position;
            if (Vector3.Distance(transform.position, threatTarget.position) < 1.5f)
            {
                Debug.Log($"抓住目標: {threatTarget.name}!");
            }
        }
        else
        {
            navAgent.SetDestination(lastSightingPosition);
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                threatTarget = null;
                currentState = NpcState.Searching;
            }
        }

        if (currentAlertLevel < 100)
        {
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
                lastSightingPosition = target.position;
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