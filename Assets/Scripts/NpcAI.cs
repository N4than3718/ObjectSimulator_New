using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("AI ���A")]
    [SerializeField] private NpcState currentState = NpcState.Searching;

    [Header("���޳]�w")]
    public List<Transform> patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("ĵ�٭ȳ]�w")]
    [Tooltip("�Cĵ�٫�(0-99)�U��ĵ�٭ȤW�ɳt��")]
    [SerializeField] private float lowAlertIncreaseRate = 20f;
    [Tooltip("�Cĵ�٫�(0-99)�U��ĵ�٭ȤU���t��")]
    [SerializeField] private float lowAlertDecreaseRate = 10f;
    [Tooltip("��ĵ�٫�(100-199)�U��ĵ�٭ȤW�ɳt��")]
    [SerializeField] private float mediumAlertIncreaseRate = 40f; // �t�״���
    [Tooltip("��ĵ�٫�(100-199)�U��ĵ�٭ȤU���t��")]
    [SerializeField] private float mediumAlertDecreaseRate = 15f;
    [Tooltip("��ĵ�٫�(200)�U��ĵ�٭ȤU���t��")]
    [SerializeField] private float highAlertDecreaseRate = 10f;
    [Tooltip("�b��ĵ�٫פU�A�h�[�S�ݨ���R�N�}�l��ĵ��")]
    [SerializeField] private float timeToStartDecreasing = 3f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("�t�׳]�w")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    // --- �p���ܼ� ---
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
            // --- �֤߭ק�G�ھ�ĵ�ٵ��ŨϥΤ��P�W�ɳt�� ---
            if (currentAlertLevel < 100)
            {
                currentAlertLevel += lowAlertIncreaseRate * Time.deltaTime;
            }
            else
            {
                // �i�J��ĵ�١A�[�t�W��
                currentAlertLevel += mediumAlertIncreaseRate * Time.deltaTime;
            }
            timeSinceLastSighting = 0f;
        }
        else
        {
            // --- �֤߭ק�G�ھ�ĵ�ٵ��ŨϥΤ��P�U���޿� ---
            if (currentAlertLevel < 100)
            {
                // �Cĵ�١A�����U��
                currentAlertLevel -= lowAlertDecreaseRate * Time.deltaTime;
            }
            else
            {
                // ��ĵ�١A�p�ɫ�~�U��
                timeSinceLastSighting += Time.deltaTime;
                if (timeSinceLastSighting >= timeToStartDecreasing)
                {
                    currentAlertLevel -= mediumAlertDecreaseRate * Time.deltaTime;
                }
            }
        }

        // ���A�ഫ
        if (currentAlertLevel >= 200)
        {
            threatTarget = movingTarget;
            if (threatTarget != null)
            {
                currentState = NpcState.Alerted;
                Debug.Log($"���A����: Searching -> Alerted! ��w�ؼ�: {threatTarget.name}");
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
                Debug.Log($"���ؼ�: {threatTarget.name}!");
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