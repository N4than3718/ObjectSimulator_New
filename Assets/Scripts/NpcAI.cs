using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // 導入 AI 導航命名空間

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    // --- AI 狀態定義 ---
    public enum NpcState { Idle, Searching, Alerted }

    [Header("AI 狀態")]
    [SerializeField] private NpcState currentState = NpcState.Idle;

    [Header("巡邏設定")]
    [Tooltip("NPC 在 Searching 狀態下會巡邏的路徑點")]
    public List<Transform> patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("警戒值設定")]
    [SerializeField] private float lowAlertDecreaseRate = 10f;  // Idle 狀態下的下降速度
    [SerializeField] private float mediumAlertIncreaseRate = 30f; // Searching 狀態下的上升速度
    [SerializeField] private float mediumAlertDecreaseRate = 15f; // Searching 狀態下的下降速度
    [SerializeField] private float highAlertDecreaseRate = 5f;   // Alerted 狀態下的下降速度
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
    private List<Transform> targetsToForget = new List<Transform>();
    private float timeSinceLastAlertIncrease = 0f;
    private Vector3 lastKnownTargetPosition;

    void Awake()
    {
        fov = GetComponent<FieldOfView>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // --- 狀態機的 Update 迴圈 ---
        switch (currentState)
        {
            case NpcState.Idle:
                IdleState();
                break;
            case NpcState.Searching:
                SearchingState();
                break;
            case NpcState.Alerted:
                AlertedState();
                break;
        }

        // 限制警戒值在 0-200
        currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);
    }

    // --- 狀態邏輯 ---

    private void IdleState()
    {
        // 在閒置狀態，警戒值不斷下降
        currentAlertLevel -= lowAlertDecreaseRate * Time.deltaTime;

        // 狀態轉換：如果看到任何移動的東西，就進入搜索狀態
        if (CheckForMovingTargets())
        {
            currentAlertLevel = 100f; // 直接跳到中警戒度
            currentState = NpcState.Searching;
            Debug.Log("狀態改變: Idle -> Searching");
        }
    }

    private void SearchingState()
    {
        navAgent.speed = patrolSpeed;
        Patrol(); // 執行巡邏

        if (CheckForMovingTargets())
        {
            // 看到移動目標，加速增加警戒值
            currentAlertLevel += mediumAlertIncreaseRate * Time.deltaTime;
            timeSinceLastAlertIncrease = 0f; // 重置計時器
        }
        else
        {
            // 沒看到，開始計時
            timeSinceLastAlertIncrease += Time.deltaTime;
            if (timeSinceLastAlertIncrease >= timeToStartDecreasing)
            {
                // 超時後，開始下降警戒值
                currentAlertLevel -= mediumAlertDecreaseRate * Time.deltaTime;
            }
        }

        // 狀態轉換
        if (currentAlertLevel >= 200)
        {
            currentState = NpcState.Alerted;
            Debug.Log("狀態改變: Searching -> Alerted");
        }
        else if (currentAlertLevel < 100)
        {
            currentState = NpcState.Idle;
            Debug.Log("狀態改變: Searching -> Idle");
        }
    }

    private void AlertedState()
    {
        navAgent.speed = chaseSpeed;

        // 在警戒狀態，警戒值緩慢下降回 100
        currentAlertLevel -= highAlertDecreaseRate * Time.deltaTime;

        // 優先追擊視野內觸發警戒的目標
        if (fov.visibleTargets.Count > 0)
        {
            Transform target = fov.visibleTargets[0];
            navAgent.SetDestination(target.position);
            Debug.Log("追擊視野內目標: " + target.name);

            // 在這裡可以加入「抓住」的邏輯，例如檢查距離
            if (Vector3.Distance(transform.position, target.position) < 1.5f)
            {
                Debug.Log("抓住目標: " + target.name + "!");
                // 遊戲結束或重置...
            }
        }
        else
        {
            // 如果視野內沒目標了，就前往最後一次看到目標的位置
            navAgent.SetDestination(lastKnownTargetPosition);

            // 如果已經到達最後位置，就回到搜索狀態
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                currentState = NpcState.Searching;
                Debug.Log("狀態改變: Alerted -> Searching (到達最後已知位置)");
            }
        }

        // 狀態轉換
        if (currentAlertLevel < 100)
        {
            currentState = NpcState.Searching;
            Debug.Log("狀態改變: Alerted -> Searching (警戒解除)");
        }
    }

    // --- 輔助函式 ---

    private bool CheckForMovingTargets()
    {
        bool detectedMovement = false;
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
                detectedMovement = true;
                lastKnownTargetPosition = target.position; // 更新最後觸發警戒的地點
            }
            lastKnownPositions[target] = target.position;
        }

        targetsToForget.Clear();
        foreach (var pair in lastKnownPositions) { if (!fov.visibleTargets.Contains(pair.Key)) targetsToForget.Add(pair.Key); }
        foreach (Transform target in targetsToForget) { lastKnownPositions.Remove(target); }

        return detectedMovement;
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0) return;

        // 如果沒有路徑或已到達目的地，就前往下一個點
        if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
        {
            navAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        }
    }
}