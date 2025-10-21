using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent), typeof(Animator))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Debug �j��߬B")] // <-- �[�Ӽ��D
    [Tooltip("�Ŀ惡���|�b�C���}�l�ɱj�� NPC �߬B�U����w������")]
    public bool forcePickupDebug = false;
    [Tooltip("�즲�������A�Q�� NPC �j��߬B�������o��")]
    public Transform debugPickupTarget;

    [Header("Component References")] // �i���n�ߺD
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

    [Header("IK �]�w")]
    [Tooltip("���w�k�Ⱙ�f���U�� 'GrabSocket' �Ū���")]
    public Transform grabSocket;

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
    private Transform ikTargetPoint = null;
    private Transform objectToParent = null;
    private float handIKWeight = 0f;
    private float hintIKWeight = 0f;
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
        // === Debug �Ҧ��ˬd ===
        if (forcePickupDebug && debugPickupTarget != null)
        {
            // �p�G�ĤF Debug�A�N�u�] Debug ��{
            Debug.LogWarning($"--- DEBUG MODE: Forcing pickup of {debugPickupTarget.name} ---", this.gameObject);
            StartCoroutine(DebugPickupRoutine()); // �Ұ� Debug ��{
        }
        else
        {
            // �p�G�S�� Debug�A�N���`�Ұ� AI
            currentState = NpcState.Searching;
            agent.speed = patrolSpeed;
            StartCoroutine(AIUpdateLoop()); // �Ұʥ��` AI ��{
        }
    }


    void Update()
    {
        UpdateAnimator();

        bool isPickingUp = anim.GetCurrentAnimatorStateInfo(0).IsName("Pick up");

        if (isPickingUp && ikTargetPoint != null)
        {
            // ���b�ߡG�v�� -> 1
            handIKWeight = Mathf.Lerp(handIKWeight, 1.0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 1.0f, Time.deltaTime * 5f);
        }
        else
        {
            // �S�b�ߡG�v�� -> 0
            handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);
        }
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

    private IEnumerator DebugPickupRoutine()
    {
        // ���� NavMeshAgent �ǳƴN��
        yield return new WaitForSeconds(0.1f);

        if (agent == null || debugPickupTarget == null)
        {
            Debug.LogError("Agent or Debug Target is null. Aborting debug pickup.", this.gameObject);
            yield break; // ������{
        }

        // 1. �]�m�ؼШ���V
        agent.SetDestination(debugPickupTarget.position);
        transform.LookAt(debugPickupTarget.position);
        Debug.Log($"--- DEBUG: Moving to {debugPickupTarget.name} at {debugPickupTarget.position} ---");

        // 2. ���ݩ�F
        //    (agent.pathPending �ˬd���O�_�٦b�p����|)
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null; // �C�V�ˬd�@��
        }

        // 3. �w��F�A����߬B
        Debug.Log("--- DEBUG: Reached target, triggering pickup animation ---");

        // �I�s�ڭ̭ק�L�� TriggerPickup �禡
        // (���{�b�|�۰ʰ��� agent)
        TriggerPickup(debugPickupTarget);
    }

    public void TriggerPickup(Transform targetRoot)
    {
        if (agent != null) agent.isStopped = true;

        // --- NEW LOGIC ---
        objectToParent = targetRoot; // �x�s�n parent ���ڪ���

        // ���մM�� "GrabPoint"
        Transform grabPoint = targetRoot.Find("GrabPoint");
        if (grabPoint != null)
        {
            ikTargetPoint = grabPoint; // ���F�IIK �˷ǳo��
        }
        else
        {
            // �S���A�N�ήڪ��� (�o�|�ɭP�B�šA���ܤ֤��| crash)
            Debug.LogWarning($"Object {targetRoot.name} lacks a 'GrabPoint' child. IK may be inaccurate.", targetRoot);
            ikTargetPoint = targetRoot;
        }
        // --- END NEW LOGIC ---

        anim.SetTrigger("Pick up"); //
        transform.LookAt(ikTargetPoint.position); // �ݦV�촤�I
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        // �p�G�S���ؼСA�Ϊ��v���� 0�A�N���򳣤���
        if (ikTargetPoint == null || handIKWeight <= 0)
        {
            // �T�O�v���Q�]�^ 0
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
            return;
        }

        // --- 1. �]�m�⪺ IK ---
        // �]�m IK �v�� (0 �� 1)
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight); // ���K�������

        // �]�m IK ���ؼЦ�m�M����
        // (�A�i��ݭn�b ikTarget �W�[�@�� "GrabPoint" �Ū���ӧ�o���)
        anim.SetIKPosition(AvatarIKGoal.RightHand, ikTargetPoint.position);
        anim.SetIKRotation(AvatarIKGoal.RightHand, ikTargetPoint.rotation);

        // --- 2. �]�m��y���� (Hint) ---
        // �o�O Pro-Tip�G�i�D��y�ө����Ӥ�V�s�A�~���|���I��h
        // �b NPC �ҫ����ӻH�k�e���@�ӪŪ���A�R�W�� "RightElbowHint"
        Transform rightElbowHint = FindRecursive("RightElbowHint"); // (�A�ݭn�ۤv��@�o�Ӭd��)

        if (rightElbowHint != null)
        {
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintIKWeight);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);
        }
    }

    // (�A�ݭn�@�ӻ��U�禡�ӧ��l����A�Ϊ̪��� public ��i��)
    private Transform FindRecursive(string name)
    {
        // ²�����G���]���b�Ĥ@�h
        return transform.Find(name);
    }

    public void AnimationEvent_GrabObject()
    {
        if (grabSocket == null) { Debug.LogError("grabSocket not assigned!", this.gameObject); return; }

        // �ڭ̭n parent ���O objectToParent
        if (objectToParent != null)
        {
            Debug.Log("NPC Grabbed: " + objectToParent.name);

            // 1. �������z (�b "objectToParent" �W)
            Rigidbody rb = objectToParent.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            Collider col = objectToParent.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }

            // 2. ���� Parent
            objectToParent.SetParent(grabSocket, true);

            // 3. �k�� (�o�~�O������)
            // �ڭ̭n�� "objectToParent" ���ʨ�@�� "local position"
            // �ϱo�����l���� "ikTargetPoint" ��n��� "grabSocket" (�]�N�O localPosition 0,0,0)

            // �p�� "GrabPoint" �۹�� "Root" �� local position
            // (�`�N: ikTargetPoint �i��O objectToParent �ۤv)
            Vector3 grabOffset = (ikTargetPoint == objectToParent) ?
                                  Vector3.zero :
                                  ikTargetPoint.localPosition;

            // �� "Root" ���쨺�� offset ���u�t�ȡv
            // �o�� "GrabPoint" �N�|�Q���� (0,0,0)
            // (�`�N: �o�̰��] GrabPoint �S���Q����L)
            objectToParent.localPosition = -grabOffset;

            // 4. �j��ץ� Scale �M Rotation
            objectToParent.localRotation = Quaternion.identity;
            objectToParent.localScale = Vector3.zero;

            // 5. ���� IK
            ikTargetPoint = null;
            objectToParent = null;
        }
    }

    // (�A�ٻݭn�@�Ӱʵe�ƥ�b�ʵe�����ɡA�� ikTarget �]�� null)
    public void AnimationEvent_PickupEnd()
    {
        // �T�O���̬O null
        ikTargetPoint = null;
        objectToParent = null;

        if (agent != null) agent.isStopped = false;
    }

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
                TriggerPickup(threatTarget.transform);
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