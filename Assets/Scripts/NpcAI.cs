using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent), typeof(Animator))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Debug �j��߬B")]
    public bool forcePickupDebug = false;
    public Transform debugPickupTarget;

    [Header("Component References")]
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;
    // fov �b Awake �����

    [Header("IK �]�w")]
    [Tooltip("���w�k�Ⱙ�f���U�� 'GrabSocket' �Ū���")]
    public Transform grabSocket;
    [Tooltip("Optional: �k��y�����I�A�קK���u�ﴡ")]
    public Transform rightElbowHint; // �אּ public ���w�A�� FindRecursive í�w

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
    [SerializeField] private float timeToStartDecreasing = 3f;
    [SerializeField] private float movementThreshold = 0.1f; // ���鲾�ʳt���H��

    [Header("�t�׳]�w")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("�����]�w")]
    [SerializeField] private float captureDistance = 1.5f;

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
    private Transform threatTarget = null;

    // IK & Grab ����
    private Transform ikTargetPoint = null;     // IK ����˷Ǫ��ؼ��I (GrabPoint �Ϊ��� Root)
    private Transform objectToGrab = null;      // �q TriggerPickup �ǻ��� AnimationEvent ���{�ɫ���
    private float handIKWeight = 0f;
    private float hintIKWeight = 0f;

    // ������H (Follow) ����
    private bool _isHoldingObject = false;
    private Transform _heldObjectRef = null;    // ��e��ڧ�۪�����ޥ�
    // private Vector3 _holdOffsetPosition = Vector3.zero; // ���A�ݭn�w�s Offset
    // private Quaternion _holdOffsetRotation = Quaternion.identity;
    private Transform _pointToAlignWithSocket = null; // �n������I (GrabPoint �Ϊ��� Root)

    private TeamManager teamManager;

    void Awake()
    {
        // ������n���ե�
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        fov = GetComponent<FieldOfView>();

        // ���~�ˬd
        if (anim == null) Debug.LogError("Animator not found!", this);
        if (agent == null) Debug.LogError("NavMeshAgent not found!", this);
        if (fov == null) Debug.LogError("FieldOfView not found!", this);

        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("NpcAI cannot find TeamManager!");
    }

    void Start()
    {
        // Debug �Ҧ��Υ��`�Ұ� AI
        if (forcePickupDebug && debugPickupTarget != null)
        {
            Debug.LogWarning($"--- DEBUG MODE: Forcing pickup of {debugPickupTarget.name} ---", this.gameObject);
            StartCoroutine(DebugPickupRoutine());
        }
        else
        {
            currentState = NpcState.Searching;
            agent.speed = patrolSpeed;
            if (patrolPoints != null && patrolPoints.Count > 0 && patrolPoints[0] != null) // �T�O�C��M�Ĥ@���I�s�b
            {
                agent.SetDestination(patrolPoints[0].position);
            }
            else if (patrolPoints == null || patrolPoints.Count == 0)
            {
                Debug.LogWarning("No patrol points assigned. NPC will remain idle unless alerted.", this.gameObject);
            }
            else
            {
                Debug.LogWarning("First patrol point is null. Please assign patrol points.", this.gameObject);
            }
            StartCoroutine(AIUpdateLoop());
        }
    }

    void Update()
    {
        // ��s Animator �t�װѼ�
        UpdateAnimator();

        // ���Ƨ�s IK �v�� (�u�b���ⶥ�q�ݭn)
        bool isPickingUp = anim.GetCurrentAnimatorStateInfo(0).IsName("Pick up");

        // �u���b�ǳƦ��� (ikTargetPoint �s�b) �B������ɤ~�W�[�v��
        if (isPickingUp && ikTargetPoint != null && !_isHoldingObject)
        {
            handIKWeight = Mathf.Lerp(handIKWeight, 1.0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 1.0f, Time.deltaTime * 5f);
        }
        else
        {
            // ��L���p�����v���k�s
            handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);
        }
    }

    void LateUpdate()
    {
        // �p�G���b��۪���A��s���� Transform �Ϩ� _pointToAlignWithSocket ��� grabSocket
        if (_isHoldingObject && _heldObjectRef != null && grabSocket != null && _pointToAlignWithSocket != null)
        {
            // 1. �p�����t������
            Quaternion rotationDifference = grabSocket.rotation * Quaternion.Inverse(_pointToAlignWithSocket.rotation);
            _heldObjectRef.rotation = rotationDifference * _heldObjectRef.rotation;

            // 2. �p���m�t������
            Vector3 positionDifference = grabSocket.position - _pointToAlignWithSocket.position;
            _heldObjectRef.position += positionDifference;

            // 3. (����) ����ץ� Scale (��P NPC Model �� Scale �v�T)
            //    �b LateUpdate �̰��i�H����ʵe�Ψ�L�]���N�~�ק� Scale
            /*
            Vector3 parentLossyScale = grabSocket.lossyScale;
            Vector3 inverseScale = Vector3.one;
            Debug.Log($"LateUpdate - Checking Socket LossyScale: {grabSocket.lossyScale.ToString("F3")}");
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            if (Mathf.Abs(parentLossyScale.y) > 1e-6f) inverseScale.y = 1.0f / parentLossyScale.y;
            if (Mathf.Abs(parentLossyScale.z) > 1e-6f) inverseScale.z = 1.0f / parentLossyScale.z;
            Debug.Log($"LateUpdate - Calculated inverseScale: {inverseScale.ToString("F3")}");
            _heldObjectRef.localScale = inverseScale;
            Debug.Log($"LateUpdate - AFTER setting localScale: {_heldObjectRef.localScale.ToString("F3")}");
            Debug.Log($"LateUpdate Final State: Held LocalScale={_heldObjectRef.localScale.ToString("F3")}, Held LossyScale={_heldObjectRef.lossyScale.ToString("F3")}, Socket LossyScale={grabSocket.lossyScale.ToString("F3")}");
            */
            // --- ���� Log (Debug �ɨ�������) ---
            Debug.Log($"LateUpdate: Socket Pos={grabSocket.position.ToString("F3")}, PointToAlign Pos={_pointToAlignWithSocket.position.ToString("F3")}");
            Debug.Log($"LateUpdate: Held Scale={_heldObjectRef.localScale.ToString("F3")}, LossyScale={_heldObjectRef.lossyScale.ToString("F3")}");
        }
    }

    private IEnumerator AIUpdateLoop()
    {
        yield return new WaitForSeconds(aiUpdateInterval); // ��l����

        while (true)
        {
            // �����ˬd TeamManager (�p�G�C�����i�୫�s�[���Υͦ�)
            if (teamManager == null)
            {
                teamManager = FindAnyObjectByType<TeamManager>();
                if (teamManager == null)
                {
                    Debug.LogWarning("AIUpdateLoop still waiting for TeamManager...");
                    yield return new WaitForSeconds(1f);
                    continue;
                }
            }

            // ���檬�A�޿�
            switch (currentState)
            {
                case NpcState.Searching:
                    SearchingState();
                    break;
                case NpcState.Alerted:
                    AlertedState();
                    break;
            }

            currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f); // ����ĵ�٭�
            yield return new WaitForSeconds(aiUpdateInterval); // ���ݤU�@����s
        }
    }

    // Debug �Ҧ��U����Ĳ�o�߬B (������)
    private IEnumerator DebugPickupRoutine()
    {
        if (debugPickupTarget == null)
        {
            Debug.LogError("Debug Target is null.", this.gameObject);
            yield break;
        }

        // �i��G���ݤ@�p�q�ɶ��T�O������l�Ƨ���
        yield return new WaitForSeconds(0.5f);

        // �i��G��V�ؼ�
        if (ikTargetPoint != null) // �ϥ� TriggerPickup ��쪺 ikTargetPoint ����V
        {
            transform.LookAt(ikTargetPoint.position);
            yield return null; // ���ݤ@�V����������
        }
        else // �p�G TriggerPickup �|������Υ��ѡA�ܤ���V�ڪ���
        {
            transform.LookAt(debugPickupTarget.position);
            yield return null;
        }

        Debug.Log("--- DEBUG: Triggering pickup animation ---");
        TriggerPickup(debugPickupTarget);
    }

    // �Ұʾ߬B�y�{
    public void TriggerPickup(Transform targetRoot)
    {
        if (targetRoot == null) { Debug.LogError("TriggerPickup called with null targetRoot!"); return; }

        // ���� Agent ����
        if (agent != null && agent.enabled) agent.isStopped = true;

        objectToGrab = targetRoot; // �]�w�n���������ڸ`�I

        // �M��촤�I (GrabPoint) �Ψϥήڸ`�I
        Transform grabPoint = targetRoot.Find("GrabPoint");
        ikTargetPoint = (grabPoint != null) ? grabPoint : targetRoot; // �]�w IK �˷��I

        if (grabPoint == null)
        {
            Debug.LogWarning($"Object {targetRoot.name} lacks a 'GrabPoint' child. IK targeting object root.", targetRoot);
        }

        // Ĳ�o�߬B�ʵe
        if (anim != null) anim.SetTrigger("Pick up");
        else { Debug.LogError("Animator is null, cannot trigger pickup."); return; }

        // �� NPC �¦V IK �ؼ��I
        if (ikTargetPoint != null) transform.LookAt(ikTargetPoint.position);
    }

    // IK �p�� (�C�V�� Animator �ե�)
    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        // IK �����ˬd�G�ؼЦs�b�B�v���j�� 0
        if (ikTargetPoint == null || handIKWeight <= 0)
        {
            // ����������A���m IK �v��
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            if (rightElbowHint != null) anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0); // �ˬd Hint �O�_�s�b
            return;
        }

        // --- ���� IK ---
        // �]�w�v��
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight);
        // �]�w�ؼ�
        anim.SetIKPosition(AvatarIKGoal.RightHand, ikTargetPoint.position);
        anim.SetIKRotation(AvatarIKGoal.RightHand, ikTargetPoint.rotation);

        // --- ��y���� ---
        if (rightElbowHint != null) // �ϥ� Inspector ���w�������I
        {
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintIKWeight);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);
        }
    }

    // (FindRecursive ��Ƥw�����A��� Inspector ���w rightElbowHint)

    // �ʵe�ƥ�G�����������
    public void AnimationEvent_GrabObject()
    {
        // Debug.Log("AnimationEvent_GrabObject CALLED"); // �O�d��¦ Log
        if (grabSocket == null) { Debug.LogError("GrabSocket IS NULL!", this.gameObject); return; }

        // �ˬd�q TriggerPickup �ǨӪ� objectToGrab
        if (objectToGrab != null)
        {
            _heldObjectRef = objectToGrab; // ������������

            // �T�w�n��������I (GrabPoint �� Root)
            Transform potentialGrabPoint = _heldObjectRef.Find("GrabPoint");
            _pointToAlignWithSocket = (potentialGrabPoint != null) ? potentialGrabPoint : _heldObjectRef;
            // Debug.Log($"Holding '{_heldObjectRef.name}', Aligning '{_pointToAlignWithSocket.name}'");

            // --- 1. �������z ---
            Rigidbody rb = _heldObjectRef.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            Collider col = _heldObjectRef.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }

            // --- 2. �]�w��l Scale (��P�� Scale) ---
            //    (���ݭn�p�� Offset�A�� LateUpdate �B�z���)
            /*
            Vector3 parentLossyScale = grabSocket.lossyScale;
            Vector3 inverseScale = Vector3.one;
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            if (Mathf.Abs(parentLossyScale.y) > 1e-6f) inverseScale.y = 1.0f / parentLossyScale.y;
            if (Mathf.Abs(parentLossyScale.z) > 1e-6f) inverseScale.z = 1.0f / parentLossyScale.z;
            _heldObjectRef.localScale = inverseScale; // �]�w Scale
            */

            // --- 3. �]�w���H���A ---
            _isHoldingObject = true;

            // --- 4. �ߧY���� IK & �M���{���ܼ� ---
            handIKWeight = 0f; // �j���v���k�s
            ikTargetPoint = null; // �M�� IK �ؼ�
            objectToGrab = null;  // �M���{�ɫ���
        }
        else
        {
            Debug.LogError("Grab Logic SKIPPED! objectToGrab was NULL!");
        }
    }

    // �ʵe�ƥ�G�߬B�ʵe���� �� ���Ĳ�o��}
    public void AnimationEvent_PickupEnd()
    {
        // Debug.Log("AnimationEvent_PickupEnd CALLED"); // �O�d��¦ Log
        Transform objectToRelease = _heldObjectRef; // �Ȧs�ޥ�

        // �������H
        _isHoldingObject = false;
        _heldObjectRef = null;
        _pointToAlignWithSocket = null;

        // --- ���s�ҥΪ��z ---
        if (objectToRelease != null)
        {
            Rigidbody rb = objectToRelease.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
            Collider col = objectToRelease.GetComponent<Collider>();
            if (col != null) { col.enabled = true; }
            // �i��G�I�[�@�I�O
            // if (rb != null) rb.AddForce(transform.forward * 0.1f + Vector3.up * 0.1f, ForceMode.VelocityChange);
        }

        // �M�� IK �ܼ� (�O�I)
        ikTargetPoint = null;
        objectToGrab = null;

        // ��_ Agent ����
        if (agent != null && agent.enabled) agent.isStopped = false;
    }

    // ��s Animator �t�װѼ�
    private void UpdateAnimator()
    {
        if (agent == null || anim == null) return;
        if (!agent.enabled) { anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime); return; }

        float currentSpeed = agent.velocity.magnitude;
        float normalizedSpeed = agent.speed > 0 ? (currentSpeed / agent.speed) : 0f;
        anim.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
    }

    // Searching ���A�޿�
    private void SearchingState()
    {
        agent.speed = patrolSpeed;
        Patrol();

        Transform movingTarget = CheckForMovingTargets();

        if (movingTarget != null)
        {
            // �W�[ĵ��
            float increaseRate = (currentAlertLevel < 100) ? lowAlertIncreaseRate : mediumAlertIncreaseRate;
            currentAlertLevel += increaseRate * aiUpdateInterval;
            timeSinceLastSighting = 0f;
            lastSightingPosition = movingTarget.position;
        }
        else
        {
            // ���Cĵ��
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
        }

        // �ˬd�O�_�i�Jĵ�٪��A
        if (currentAlertLevel >= 200 && movingTarget != null)
        {
            threatTarget = movingTarget;
            currentState = NpcState.Alerted;
            Debug.Log($"State Change: Searching -> Alerted! Target: {threatTarget.name}");
        }
    }

    // Alerted ���A�޿�
    private void AlertedState()
    {
        agent.speed = chaseSpeed;
        currentAlertLevel -= highAlertDecreaseRate * aiUpdateInterval; // �۵M�U��

        bool threatIsStillVisible = (threatTarget != null && fov.visibleTargets.Contains(threatTarget));

        if (threatIsStillVisible)
        {
            // A: ����l��
            agent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position;
            timeSinceLastSighting = 0f;

            // �ˬd����
            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"Capturing target: {threatTarget.name}!");
                if (teamManager != null) teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                TriggerPickup(threatTarget); // -> isStopped = true
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0;
                return; // ����
            }
        }
        else
        {
            // B: �ؼХᥢ�A�e���̫��m
            if (threatTarget != null && agent.destination != lastSightingPosition) // �ˬd threatTarget �O�_�s�b
            {
                agent.SetDestination(lastSightingPosition);
            }

            // C: �o�{�s�ؼ�
            Transform newMovingTarget = CheckForMovingTargets();
            if (newMovingTarget != null && newMovingTarget != threatTarget)
            {
                Debug.Log($"Lost target {threatTarget?.name ?? "NULL"}! New target: {newMovingTarget.name}");
                threatTarget = newMovingTarget;
                currentAlertLevel = 200f; // ���mĵ��
                lastSightingPosition = threatTarget.position;
                timeSinceLastSighting = 0f;
                // �� return�A�U�@���l��
            }
            // D: ��F�̫��m�B�L�s�o�{
            else if (threatTarget != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) // �ˬd threatTarget
            {
                timeSinceLastSighting += aiUpdateInterval;
                if (timeSinceLastSighting > timeToStartDecreasing * 2f)
                {
                    Debug.Log("Target lost at last known position. Returning to search.");
                    threatTarget = null;
                    currentState = NpcState.Searching;
                }
            }
            // E: �p�G�@�}�l�N�S�� threatTarget (�Ҧp�����Q�]�� Alerted?)�A�Ϊ̥ؼФw�Q�P��
            else if (threatTarget == null)
            {
                Debug.LogWarning("Alerted state entered without a valid threatTarget or target destroyed. Returning to search.");
                currentState = NpcState.Searching;
            }
        }

        // ĵ�٭ȹL�C�A��^�j��
        if (currentAlertLevel < 100)
        {
            Debug.Log("Alert level dropped. Returning to search.");
            threatTarget = null;
            currentState = NpcState.Searching;
        }
    }

    // �ˬd���ʥؼ�
    private Transform CheckForMovingTargets()
    {
        Transform detectedMovingTarget = null;
        if (fov == null || fov.visibleTargets == null) return null;

        foreach (Transform target in fov.visibleTargets)
        {
            if (target == null) continue;

            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue;
            }

            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);
            if (aiUpdateInterval > 0 && (distanceMoved / aiUpdateInterval) > movementThreshold)
            {
                detectedMovingTarget = target;
                // ��s�̫��m���޿貾�쪬�A�����A�קK�ݨ��R���]��s
                // lastSightingPosition = target.position;
            }
            lastKnownPositions[target] = target.position; // �����s��m
        }

        // �M�z
        List<Transform> targetsToForget = new List<Transform>(lastKnownPositions.Count); // ��l�Ʈe�q
        foreach (var pair in lastKnownPositions)
        {
            if (!fov.visibleTargets.Contains(pair.Key))
            {
                targetsToForget.Add(pair.Key);
            }
        }
        foreach (Transform target in targetsToForget)
        {
            lastKnownPositions.Remove(target);
        }

        return detectedMovingTarget;
    }

    // ����
    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || agent == null || !agent.enabled || agent.isStopped) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            if (patrolPoints[currentPatrolIndex] != null)
            {
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            }
            else
            {
                Debug.LogWarning($"Patrol point {currentPatrolIndex} is null!");
                // Simple skip: try next one immediately in next valid update
                // Or add more complex logic to find next valid point
            }
        }
    }
}