using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Component References")] // �i���n�ߺD
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

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

    [Header("�į�]�w")]
    [Tooltip("AI �M���޿誺��s���j (��)")]
    [SerializeField] private float aiUpdateInterval = 0.2f;

    [Header("Debug")]
    [SerializeField][Range(0, 200)] private float currentAlertLevel = 0f;

    // --- �p���ܼ� ---
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

        // ������ �ק�G�Ұ� AI �޿��{�A���N Update() ������
        StartCoroutine(AIUpdateLoop());
        // ������������������������������������
    }


    void Update()
    {
        UpdateAnimator();
    }

    // ������ �s�W�GAI �޿��{ ������
    private IEnumerator AIUpdateLoop()
    {
        // ���ݤ@�p�q�ɶ��������������J
        yield return new WaitForSeconds(aiUpdateInterval);

        while (true)
        {
            if (teamManager == null)
            {
                // �p�G�䤣�� TeamManager�A�N���򵥫�
                yield return new WaitForSeconds(aiUpdateInterval);
                continue;
            }

            // �����e���A���޿�
            switch (currentState)
            {
                case NpcState.Searching:
                    SearchingState();
                    break;
                case NpcState.Alerted:
                    AlertedState();
                    break;
            }

            // �T�Oĵ�٭Ȧb 0-200 ����
            currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);

            // ���ݩT�w���ɶ���A����U�@����s
            yield return new WaitForSeconds(aiUpdateInterval);
        }
    }
    // ������������������������������������

    private void UpdateAnimator()
    {
        if (agent == null || anim == null) return;

        // 1. ��� NavMeshAgent �Q�n���t�� (desiredVelocity) �ι�ڳt�� (velocity)
        //    �ڭ̥� velocity.magnitude �������e��ڪ����ʳt�v
        float currentSpeed = agent.velocity.magnitude;

        // 2. ���F�� Animator �� Speed �ѼƦb 0-1 ���� (�p�G�A�� agent speed ���O 1 ����)�A
        //    �̦n���@�ӥ��W�� (Normalize)
        //    (���]�A�b agent �]�m�� speed �O 3.5f)
        float normalizedSpeed = currentSpeed / agent.speed;

        // 3. ��o�ӭȶǵ� Animator
        //    �ϥ� SetFloat �� Damp Time (e.g., 0.1f) �i�H���ʵe�L��󥭷ơA����氱
        anim.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
    }

    private void SearchingState()
    {
        agent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // ������ �ק�G�ϥ� aiUpdateInterval ���N Time.deltaTime ������
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
            // ������������������������������������
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
        agent.speed = chaseSpeed;

        // ������ �ק�G�ϥ� aiUpdateInterval ���N Time.deltaTime ������
        currentAlertLevel -= highAlertDecreaseRate * aiUpdateInterval;
        // ������������������������������������

        // --- �u�ơG�b���A�}�l�ɥ��ˬd�@�����ʥؼ� ---
        Transform currentlyVisibleMovingTarget = CheckForMovingTargets();
        bool threatIsVisible = (threatTarget != null && fov.visibleTargets.Contains(threatTarget));

        // --- ���p A: �¯٥ؼ��٦b������ ---
        if (threatIsVisible)
        {
            agent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // �����s�̫�ݨ쥦����m

            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"���ؼ�: {threatTarget.name}!");
                teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0; // ����M�sĵ��
                return; // �ߨ赲�������A�޿�
            }
        }
        // --- ���p B: �¯٥ؼФw�ᥢ (���b������) ---
        else
        {
            // �e���̫�@���ݨ�ؼЪ���m�i��j��
            agent.SetDestination(lastSightingPosition);

            // --- ���p C: �b�e���~���A�ݨ�F *�s��* ���ʥؼ� (���O�쥻���¯٥ؼ�) ---
            if (currentlyVisibleMovingTarget != null && currentlyVisibleMovingTarget != threatTarget)
            {
                Debug.Log($"�D�n�ؼХᥢ�I�b�e���լd�ɵo�{�s�ؼ�: {currentlyVisibleMovingTarget.name}");
                threatTarget = currentlyVisibleMovingTarget; // ������s����WH�ؼ�
                currentAlertLevel = 200f; // ���mĵ�٭ȡA�}�l�@�����s���l��
                return; // �������޿�A�U�@�� AIUpdate �j��N�|����(���pA)
            }

            // --- ���p D: �p�G�w�g��F�̫�w����m�A�åB�S���o�{�s�ؼСA�h��^�j�����A ---
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
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

            // ������ �ק�G�ϥ� aiUpdateInterval ���N Time.deltaTime ������
            // �����t�� (�C���ʶZ��)
            if (distanceMoved / aiUpdateInterval > movementThreshold)
            {
                detectedMovingTarget = target;
                lastSightingPosition = target.position; // �u�n�ݨ첾�ʡA�N��s�̫���R��m
            }
            // ������������������������������������

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