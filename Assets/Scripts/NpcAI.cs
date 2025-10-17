using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    // --- ���� Idle�ASearching �{�b�O��¦���A ---
    public enum NpcState { Searching, Alerted }

    [Header("AI ���A")]
    [SerializeField] private NpcState currentState = NpcState.Searching;

    [Header("���޳]�w")]
    public List<Transform> patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("ĵ�٭ȳ]�w")]
    [SerializeField] private float increaseRate = 30f; // �Τ@���W�ɳt��
    [SerializeField] private float searchDecreaseRate = 15f; // Searching ���A�U���U���t��
    [SerializeField] private float alertDecreaseRate = 10f;  // Alerted ���A�U���U���t��
    [Tooltip("�b Searching ���A�U�A�h�[�S�ݨ���R�N�}�l���Cĵ�٭�")]
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

    // ������ ����s�W�G�Ψ���w�¯٥ؼ� ������
    private Transform threatTarget;
    // ����������������������������������������

    void Awake()
    {
        fov = GetComponent<FieldOfView>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // �C���@�}�l�N�i�J���ު��A
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

    // --- ���A�޿� ---

    private void SearchingState()
    {
        navAgent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // �ݨ첾�ʥؼСA�W�[ĵ�٭�
            currentAlertLevel += increaseRate * Time.deltaTime;
            timeSinceLastSighting = 0f;
        }
        else
        {
            // �S�ݨ�A�}�l�p�ɨäU��ĵ�٭�
            timeSinceLastSighting += Time.deltaTime;
            if (timeSinceLastSighting >= timeToStartDecreasing)
            {
                currentAlertLevel -= searchDecreaseRate * Time.deltaTime;
            }
        }

        // ���A�ഫ�G��ĵ�٭ȹF�� 200
        if (currentAlertLevel >= 200)
        {
            // ������ ��w��e�y���¯٪��ؼ� ������
            threatTarget = movingTarget;
            if (threatTarget != null)
            {
                currentState = NpcState.Alerted;
                Debug.Log($"���A����: Searching -> Alerted! ��w�ؼ�: {threatTarget.name}");
            }
            else
            {
                // �p�G�b�F��200�������ؼЭ�n�����A���ӫO�I�A�����h�̫᪺��m
                currentState = NpcState.Alerted;
                threatTarget = null; // �M�ťؼ�
                Debug.Log("���A����: Searching -> Alerted! �ؼФw�����A�e���̫�w����m�C");
            }
        }
    }

    private void AlertedState()
    {
        navAgent.speed = chaseSpeed;
        currentAlertLevel -= alertDecreaseRate * Time.deltaTime;

        // --- �֤߭ק�G�u�l����w�� threatTarget ---
        if (threatTarget != null && fov.visibleTargets.Contains(threatTarget))
        {
            // �p�G�¯٥ؼ��٦b�������A�N����l����
            navAgent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // �����s�̫�ݨ쥦����m

            if (Vector3.Distance(transform.position, threatTarget.position) < 1.5f)
            {
                Debug.Log($"���ؼ�: {threatTarget.name}!");
                // �i�H�b�o��Ĳ�o���᪺�޿�
            }
        }
        else
        {
            // �p�G�¯٥ؼФ��b������ (���F)
            navAgent.SetDestination(lastSightingPosition);

            // �p�G�w�g��F�̫��m�A�N�Ѱ���w�æ^��j�����A
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                Debug.Log("�ؼХᥢ�A�Ѱ�ĵ�١C");
                threatTarget = null;
                currentState = NpcState.Searching;
            }
        }

        // ���A�ഫ�G��ĵ�٭ȭ��^ 100 �H�U
        if (currentAlertLevel < 100)
        {
            Debug.Log("ĵ�٭ȤU���A�Ѱ�ĵ�١C");
            threatTarget = null; // �Ѱ��ؼ���w
            currentState = NpcState.Searching;
        }
    }

    // --- ���U�禡 ---

    // �o�Ө禡�{�b�^�ǲĤ@�ӳQ�����첾�ʪ��ؼ�
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

        // �M�z���b���������ؼ�
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