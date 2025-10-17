using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // �ɤJ AI �ɯ�R�W�Ŷ�

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    // --- AI ���A�w�q ---
    public enum NpcState { Idle, Searching, Alerted }

    [Header("AI ���A")]
    [SerializeField] private NpcState currentState = NpcState.Idle;

    [Header("���޳]�w")]
    [Tooltip("NPC �b Searching ���A�U�|���ު����|�I")]
    public List<Transform> patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("ĵ�٭ȳ]�w")]
    [SerializeField] private float lowAlertDecreaseRate = 10f;  // Idle ���A�U���U���t��
    [SerializeField] private float mediumAlertIncreaseRate = 30f; // Searching ���A�U���W�ɳt��
    [SerializeField] private float mediumAlertDecreaseRate = 15f; // Searching ���A�U���U���t��
    [SerializeField] private float highAlertDecreaseRate = 5f;   // Alerted ���A�U���U���t��
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
        // --- ���A���� Update �j�� ---
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

        // ����ĵ�٭Ȧb 0-200
        currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);
    }

    // --- ���A�޿� ---

    private void IdleState()
    {
        // �b���m���A�Aĵ�٭Ȥ��_�U��
        currentAlertLevel -= lowAlertDecreaseRate * Time.deltaTime;

        // ���A�ഫ�G�p�G�ݨ���󲾰ʪ��F��A�N�i�J�j�����A
        if (CheckForMovingTargets())
        {
            currentAlertLevel = 100f; // �������줤ĵ�٫�
            currentState = NpcState.Searching;
            Debug.Log("���A����: Idle -> Searching");
        }
    }

    private void SearchingState()
    {
        navAgent.speed = patrolSpeed;
        Patrol(); // ���樵��

        if (CheckForMovingTargets())
        {
            // �ݨ첾�ʥؼСA�[�t�W�[ĵ�٭�
            currentAlertLevel += mediumAlertIncreaseRate * Time.deltaTime;
            timeSinceLastAlertIncrease = 0f; // ���m�p�ɾ�
        }
        else
        {
            // �S�ݨ�A�}�l�p��
            timeSinceLastAlertIncrease += Time.deltaTime;
            if (timeSinceLastAlertIncrease >= timeToStartDecreasing)
            {
                // �W�ɫ�A�}�l�U��ĵ�٭�
                currentAlertLevel -= mediumAlertDecreaseRate * Time.deltaTime;
            }
        }

        // ���A�ഫ
        if (currentAlertLevel >= 200)
        {
            currentState = NpcState.Alerted;
            Debug.Log("���A����: Searching -> Alerted");
        }
        else if (currentAlertLevel < 100)
        {
            currentState = NpcState.Idle;
            Debug.Log("���A����: Searching -> Idle");
        }
    }

    private void AlertedState()
    {
        navAgent.speed = chaseSpeed;

        // �bĵ�٪��A�Aĵ�٭Ƚw�C�U���^ 100
        currentAlertLevel -= highAlertDecreaseRate * Time.deltaTime;

        // �u���l��������Ĳ�oĵ�٪��ؼ�
        if (fov.visibleTargets.Count > 0)
        {
            Transform target = fov.visibleTargets[0];
            navAgent.SetDestination(target.position);
            Debug.Log("�l���������ؼ�: " + target.name);

            // �b�o�̥i�H�[�J�u���v���޿�A�Ҧp�ˬd�Z��
            if (Vector3.Distance(transform.position, target.position) < 1.5f)
            {
                Debug.Log("���ؼ�: " + target.name + "!");
                // �C�������έ��m...
            }
        }
        else
        {
            // �p�G�������S�ؼФF�A�N�e���̫�@���ݨ�ؼЪ���m
            navAgent.SetDestination(lastKnownTargetPosition);

            // �p�G�w�g��F�̫��m�A�N�^��j�����A
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                currentState = NpcState.Searching;
                Debug.Log("���A����: Alerted -> Searching (��F�̫�w����m)");
            }
        }

        // ���A�ഫ
        if (currentAlertLevel < 100)
        {
            currentState = NpcState.Searching;
            Debug.Log("���A����: Alerted -> Searching (ĵ�ٸѰ�)");
        }
    }

    // --- ���U�禡 ---

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
                lastKnownTargetPosition = target.position; // ��s�̫�Ĳ�oĵ�٪��a�I
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

        // �p�G�S�����|�Τw��F�ت��a�A�N�e���U�@���I
        if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
        {
            navAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        }
    }
}