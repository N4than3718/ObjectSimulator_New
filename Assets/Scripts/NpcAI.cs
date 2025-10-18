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
    [SerializeField] private float lowAlertIncreaseRate = 20f;
    [SerializeField] private float lowAlertDecreaseRate = 10f;
    [SerializeField] private float mediumAlertIncreaseRate = 40f;
    [SerializeField] private float mediumAlertDecreaseRate = 15f;
    [SerializeField] private float highAlertDecreaseRate = 10f;
    [Tooltip("�b��ĵ�٫פU�A�h�[�S�ݨ���R�N�}�l��ĵ��")]
    [SerializeField] private float timeToStartDecreasing = 3f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("�t�׳]�w")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("�����]�w")]
    [SerializeField] private float captureDistance = 1.5f; // �����Z��

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    // --- �p���ܼ� ---
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
                Debug.Log($"���A����: Searching -> Alerted! ��w�ؼ�: {threatTarget.name}");
            }
        }
    }

    private void AlertedState()
    {
        navAgent.speed = chaseSpeed;
        currentAlertLevel -= highAlertDecreaseRate * Time.deltaTime;

        // �P�_��e��w���¯٥ؼЬO�_�٦b������
        if (threatTarget != null && fov.visibleTargets.Contains(threatTarget))
        {
            // --- ���p A: �ؼ��٦b������ ---
            navAgent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // �����s�̫�ݨ쥦����m

            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"���ؼ�: {threatTarget.name}!");
                // �q�� TeamManager �����o�Ө���
                teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                // ����A�M���¯٥ؼШê�^�j�����A
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0; // �i�H��ܲM�s�Τ��M�s
                return; // ���n�G�ߨ赲���o�@�V�� AlertedState �޿�
            }
        }
        else
        {
            // --- ���p B: �ؼФw�ᥢ (���b�������Τ��s�b�F) ---

            // �e���̫�@���ݨ�ؼЪ���m�i��j��
            navAgent.SetDestination(lastSightingPosition);

            // �b�e���j�����~���A�ˬd�O�_���s�����R
            Transform newMovingTarget = CheckForMovingTargets();
            if (newMovingTarget != null)
            {
                Debug.Log($"�D�n�ؼХᥢ�I�b�e���լd�ɵo�{�s�ؼ�: {newMovingTarget.name}");
                threatTarget = newMovingTarget; // ������s���¯٥ؼ�
                currentAlertLevel = 200f; // ���mĵ�٭ȡA�}�l�@�����s���l��
                // ������^�A�קK����U�����u��F�ت��a�v�P�_
                return;
            }

            // �p�G�w�g��F�̫�w����m�A�åB�S���o�{�s�ؼСA�h��^�j�����A
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                Debug.Log("�b�̫�w����m���o�{�ؼСA��^�j�����A�C");
                threatTarget = null;
                currentState = NpcState.Searching;
            }
        }

        // �p�Gĵ�٭Ȧ۵M�U���� 100 �H�U�A�]��^�j�����A
        if (currentAlertLevel < 100)
        {
            Debug.Log("ĵ�٭ȤU���A�Ѱ�ĵ�٪��A�C");
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
                lastSightingPosition = target.position; // �u�n�ݨ첾�ʡA�N��s�̫���R��m
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