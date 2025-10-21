using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfView), typeof(NavMeshAgent), typeof(Animator))]
public class NpcAI : MonoBehaviour
{
    public enum NpcState { Searching, Alerted }

    [Header("Debug �j��߬B")]
    [Tooltip("�Ŀ惡���|�b�C���}�l�ɱj�� NPC �߬B�U����w������")]
    public bool forcePickupDebug = false;
    [Tooltip("�즲�������A�Q�� NPC �j��߬B�������o��")]
    public Transform debugPickupTarget;

    [Header("Component References")]
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

    [Header("IK �]�w")]
    [Tooltip("���w�k�Ⱙ�f���U�� 'GrabSocket' �Ū���")]
    public Transform grabSocket;
    [Tooltip("Optional: For elbow hint calculation")] // �i�H�[�W��y�����I������
    public Transform rightElbowHint; // �p�G FindRecursive ���i�a�A�i�H�Ҽ{�� public �����w

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
    [SerializeField] private float movementThreshold = 0.1f;

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
    private Transform threatTarget = null; // ��l�Ƭ� null ����n

    // IK & Grab ����
    private Transform ikTargetPoint = null;     // IK ����˷Ǫ��ؼ��I (�i��O GrabPoint �Ϊ��� Root)
    private Transform objectToGrab = null;      // �q TriggerPickup �ǻ��� AnimationEvent ���{�ɫ���
    private float handIKWeight = 0f;
    private float hintIKWeight = 0f;

    // ������H (Follow) ����
    private bool _isHoldingObject = false;
    private Transform _heldObjectRef = null;    // ��e��ڧ�۪�����ޥ�
    private Vector3 _holdOffsetPosition = Vector3.zero; // �۹�� grabSocket �����a��m����
    private Quaternion _holdOffsetRotation = Quaternion.identity; // �۹�� grabSocket �����a���ా��
    private Transform _pointToAlignWithSocket = null;

    private TeamManager teamManager;

    void Awake()
    {
        // ������n���ե�
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        fov = GetComponent<FieldOfView>(); // �b�o������N�n

        // ���~�ˬd
        if (anim == null) Debug.LogError("Animator not found!", this);
        if (agent == null) Debug.LogError("NavMeshAgent not found!", this);
        if (fov == null) Debug.LogError("FieldOfView not found!", this);

        teamManager = FindAnyObjectByType<TeamManager>(); // Unity 2023+ ��ĳ�� FindAnyObjectByType
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
            if (patrolPoints.Count > 0) // �T�O�������I�A�}�l
            {
                agent.SetDestination(patrolPoints[0].position);
            }
            StartCoroutine(AIUpdateLoop());
        }
    }

    void Update()
    {
        // ��s Animator �t�װѼ�
        UpdateAnimator();

        // ���Ƨ�s IK �v�� (�u�b���ⶥ�q�ݭn)
        // isPickingUp �ˬd State Name �O�_��T�ǰt Animator Controller �̪� State Name
        bool isPickingUp = anim.GetCurrentAnimatorStateInfo(0).IsName("Pick up");

        // �u���b�ǳƦ��� (ikTargetPoint �s�b) �B�ʵe�b����ɤ~�W�[�v��
        if (isPickingUp && ikTargetPoint != null && !_isHoldingObject) // �K�[ !_isHoldingObject �P�_
        {
            handIKWeight = Mathf.Lerp(handIKWeight, 1.0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 1.0f, Time.deltaTime * 5f); // ��y����
        }
        else
        {
            // ��L���p (�S�b�� / �w�g��� / IK �ؼФw null) �����v���k�s
            handIKWeight = Mathf.Lerp(handIKWeight, 0f, Time.deltaTime * 5f);
            hintIKWeight = Mathf.Lerp(hintIKWeight, 0f, Time.deltaTime * 5f);
        }
        // �`�N�G���e�������� _ikNeedsImmediateTermination �X�СA
        // �b�ثe�� Follow �޿�U (Event �����] handIKWeight=0)�A���Ӥ��A�ݭn
    }

    void LateUpdate()
    {
        if (_isHoldingObject && _heldObjectRef != null && grabSocket != null && _pointToAlignWithSocket != null)
        {
            // --- �p��ݭn�I�[��ڪ���W���첾�M���� ---

            // �ؼСG�� _pointToAlignWithSocket ���@�ɮy��/���� ���� grabSocket ���@�ɮy��/����

            // 1. �p���e "����I" �P "�ؼ� Socket" ����������t
            Quaternion rotationDifference = grabSocket.rotation * Quaternion.Inverse(_pointToAlignWithSocket.rotation);

            // 2. �N�o�ӱ���t���Ψ�ڪ���W
            _heldObjectRef.rotation = rotationDifference * _heldObjectRef.rotation;

            // 3. �p�����α����A"����I" �{�b����m �P "�ؼ� Socket" ��������m�t
            Vector3 positionDifference = grabSocket.position - _pointToAlignWithSocket.position;

            // 4. �N�o�Ӧ�m�t���Ψ�ڪ���W
            _heldObjectRef.position += positionDifference;

            // --- (�i��) ����j�� Scale ---
            // �p�G Scale �b��L�a��Q�N�~�ק�A�i�H�b�o�̦A���j��
            
            Vector3 parentLossyScale = grabSocket.lossyScale;
            Vector3 inverseScale = Vector3.one;
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            // ... (y, z checks) ...
            _heldObjectRef.localScale = inverseScale;
            

            // --- Log ���� ---
            // Debug.Log($"LateUpdate: Socket Pos={grabSocket.position.ToString("F3")}, PointToAlign Pos={_pointToAlignWithSocket.position.ToString("F3")}, Root Pos={_heldObjectRef.position.ToString("F3")}");
        }
    }

    private IEnumerator AIUpdateLoop()
    {
        yield return new WaitForSeconds(aiUpdateInterval); // ��l����

        while (true)
        {
            if (teamManager == null) // �����ˬd TeamManager �O�_�s�b
            {
                teamManager = FindAnyObjectByType<TeamManager>();
                if (teamManager == null)
                {
                    Debug.LogWarning("AIUpdateLoop waiting for TeamManager...");
                    yield return new WaitForSeconds(1f); // �p�G�䤣��A���ݧ���ɶ�
                    continue;
                }
            }

            // �ھڷ�e���A�����޿�
            switch (currentState)
            {
                case NpcState.Searching:
                    SearchingState();
                    break;
                case NpcState.Alerted:
                    AlertedState();
                    break;
            }

            // ����ĵ�٭Ƚd��
            currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 200f);

            // ���ݤU�@����s
            yield return new WaitForSeconds(aiUpdateInterval);
        }
    }

    private IEnumerator DebugPickupRoutine()
    {

        if (debugPickupTarget == null)
        {
            Debug.LogError("Debug Target is null.", this.gameObject);
            yield break;
        }

        // --- ����Ĳ�o�߬B ---
        // (�i��) �� NPC �ߨ���V�ؼ�
        // Check if agent is enabled before using LookAt that relies on transform update potentially
        if (agent != null && agent.enabled)
        {
            // If NPC needs to turn, ensure agent updates position/rotation first
            // Maybe wait a frame or adjust update order if LookAt is unreliable here
            transform.LookAt(debugPickupTarget.position);
            yield return null; // Wait one frame for rotation to potentially apply
        }
        else if (agent == null || !agent.enabled)
        {
            // Simple LookAt if agent is off
            transform.LookAt(debugPickupTarget.position);
        }


        Debug.Log("--- DEBUG: Triggering pickup animation immediately ---");
        TriggerPickup(debugPickupTarget);
    }

    // Ĳ�o�߬B�y�{ (�� AI ���A�� Debug �ե�)
    public void TriggerPickup(Transform targetRoot)
    {
        if (targetRoot == null)
        {
            Debug.LogError("TriggerPickup called with null targetRoot!");
            return;
        }

        // ����� (�p�G Agent �s�b�B�ҥ�)
        if (agent != null && agent.enabled) // Check if agent exists and is enabled
        {
            agent.isStopped = true;
        }


        objectToGrab = targetRoot; // �x�s����ڸ`�I�A�� AnimationEvent �ϥ�

        // �M��촤�I GrabPoint
        Transform grabPoint = targetRoot.Find("GrabPoint");
        if (grabPoint != null)
        {
            ikTargetPoint = grabPoint; // IK �˷� GrabPoint
            Debug.Log($"TriggerPickup: Found GrabPoint for {targetRoot.name}. IK targeting GrabPoint.");
        }
        else
        {
            // �S��� GrabPoint�AIK �����˷Ǫ���ڸ`�I
            Debug.LogWarning($"Object {targetRoot.name} lacks a 'GrabPoint' child. IK targeting object root. Position might be inaccurate.", targetRoot);
            ikTargetPoint = targetRoot;
        }

        // Ĳ�o Animator ���� "Pick up" Trigger
        if (anim != null) // Check if anim exists
        {
            anim.SetTrigger("Pick up");
        }
        else
        {
            Debug.LogError("Animator is null, cannot set trigger 'Pick up'.");
            return; // Cannot proceed without animator
        }


        // �� NPC �¦V IK �ؼ��I (��ı�ĪG)
        if (ikTargetPoint != null) // Check if ikTargetPoint was successfully assigned
        {
            transform.LookAt(ikTargetPoint.position);
        }
    }

    // Unity �b Animator IK Pass �ҥήɦ۰ʽե�
    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return; // �T�O Animator �s�b

        // IK �����ˬd�G�ؼ��I�s�b �B IK�v���j��0
        if (ikTargetPoint == null || handIKWeight <= 0)
        {
            // �p�G����������A�T�O�Ҧ����� IK �v���k�s
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
            return; // ���e�h�X�A������ IK �p��
        }

        // --- ����k�� IK ---
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight); // �P�B��m�M�����v��
        anim.SetIKPosition(AvatarIKGoal.RightHand, ikTargetPoint.position); // �]�w IK �ؼЦ�m
        anim.SetIKRotation(AvatarIKGoal.RightHand, ikTargetPoint.rotation); // �]�w IK �ؼб���

        // --- (�i��) ����k��y�����I IK ---
        // �o���U�󱱨���u�s����V�A�קK�ﴡ
        // �ݭn�b������ NPC �ҫ��U�ЫئW�� "RightElbowHint" ���Ū���@�������I
        Transform rightElbowHint = FindRecursive("RightElbowHint"); // �d�䴣���I
        if (rightElbowHint != null)
        {
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintIKWeight); // �]�w�����I�v��
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position); // �]�w�����I��m
        }
    }

    // �d��l���� (²�����A�i�ھڻݭn�X�i�����j�d��)
    private Transform FindRecursive(string name)
    {
        // �d�䪽���l����
        Transform child = transform.Find(name);
        if (child != null) return child;

        // �p�G�ݭn���j�d��Ҧ��l�h�� (�į�Ҷq�A�ԷV�ϥ�)
        /*
        foreach (Transform t in transform)
        {
            child = t.Find(name); // �d��]�N
            if (child != null) return child;
            // �i�H�~�򻼰j...
        }
        */
        return null; // �䤣���^ null
    }

    // �� "Pick up" �ʵe���q�����ƥ�Ĳ�o
    public void AnimationEvent_GrabObject()
    {
        if (grabSocket == null) { Debug.LogError("GrabSocket IS NULL!", this.gameObject); return; }

        // �ˬd�q TriggerPickup �ǨӪ� objectToGrab (�����)
        if (objectToGrab != null)
        {
            _heldObjectRef = objectToGrab; // �x�s�ڪ���ޥ�

            // --- �T�w�n��������I ---
            Transform potentialGrabPoint = objectToGrab.Find("GrabPoint");
            _pointToAlignWithSocket = (potentialGrabPoint != null) ? potentialGrabPoint : objectToGrab; // �x�s�n������I
            Debug.Log($"Grab Logic Starting (Follow Mode): Holding '{_heldObjectRef.name}', Aligning '{_pointToAlignWithSocket.name}'");


            // --- 1. �������z ---
            Rigidbody rb = _heldObjectRef.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            Collider col = _heldObjectRef.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }
            // Debug.Log("Step 1: Physics disabled.");

            // --- 2. �p���l Scale �ץ� (���@�� Scale �� 1) ---
            //    (���A�ݭn�p�� Offset�ALateUpdate �|�B�z���)
            Vector3 parentLossyScale = grabSocket.lossyScale;
            Vector3 inverseScale = Vector3.one;
            if (Mathf.Abs(parentLossyScale.x) > 1e-6f) inverseScale.x = 1.0f / parentLossyScale.x;
            if (Mathf.Abs(parentLossyScale.y) > 1e-6f) inverseScale.y = 1.0f / parentLossyScale.y;
            if (Mathf.Abs(parentLossyScale.z) > 1e-6f) inverseScale.z = 1.0f / parentLossyScale.z;
            _heldObjectRef.localScale = inverseScale; // <--- �b Event �̥����� Scale
                                                      // Debug.Log($"Step 2: Applied inverse localScale = {inverseScale}");

            // --- 3. �]�w���H���A ---
            _isHoldingObject = true;
            // Debug.LogWarning($"!!! Set _isHoldingObject = true. Held Object: {_heldObjectRef.name} !!!");
            // --- NO SetParent HERE ---


            // --- 4. �ߧY�j������ IK & �M���{���ܼ� ---
            // Debug.LogWarning("!!! Force setting handIKWeight = 0f !!!");
            handIKWeight = 0f;

            ikTargetPoint = null; // �M�� IK �ؼ�
            objectToGrab = null;  // �M���{�ɶǻ��ܼ�
            // Debug.Log("Step 4: IK variables nulled.");

            // Debug.Log("--- Grab Event ENDED (Follow Mode) ---");
        }
        else
        {
            Debug.LogError($"!!! Grab Logic SKIPPED! objectToGrab was NULL when event triggered !!!");
        }
    }

    // �� "Pick up" �ʵe���q�������ƥ�Ĳ�o (�ΥѨ�L�޿�եΥH��}����)
    public void AnimationEvent_PickupEnd()
    {
        Transform objectToRelease = _heldObjectRef;

        _isHoldingObject = false;
        _heldObjectRef = null;
        _pointToAlignWithSocket = null; // <-- �M������I�ޥ�

        // ... (���s�ҥΪ��z���{���X) ...

        ikTargetPoint = null;
        objectToGrab = null;

        if (agent != null && agent.enabled) agent.isStopped = false;
    }

    // ��s Animator ���� Speed �Ѽ�
    private void UpdateAnimator()
    {
        // �ˬd�ե�O�_�s�b
        if (agent == null || anim == null) return;

        // �p�G Agent �Q�T�ΡA�t�׵��� 0
        if (!agent.enabled)
        {
            anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
            return;
        }

        // �p���e�t�שM���W�Ƴt��
        float currentSpeed = agent.velocity.magnitude;
        // ����H�s (�p�G agent.speed �O 0)
        float normalizedSpeed = agent.speed > 0 ? (currentSpeed / agent.speed) : 0f;

        // ��s Animator �ѼơA�ϥ� Damp Time ���ƹL��
        anim.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
    }

    // Searching ���A�޿�
    private void SearchingState()
    {
        agent.speed = patrolSpeed; // �T�O�t�׬O���޳t��
        Patrol(); // ���樵��

        // �ˬd�������O�_�����ʥؼ�
        Transform movingTarget = CheckForMovingTargets();

        // �ھڬO�_�ݨ첾�ʥؼСA�վ�ĵ�٭�
        if (movingTarget != null)
        {
            // �ݨ�ؼСA�W�[ĵ��
            float increaseRate = (currentAlertLevel < 100) ? lowAlertIncreaseRate : mediumAlertIncreaseRate;
            currentAlertLevel += increaseRate * aiUpdateInterval;
            timeSinceLastSighting = 0f; // ���m�W���ݨ�ɶ�
            lastSightingPosition = movingTarget.position; // ��s�̫���R��m
        }
        else
        {
            // �S�ݨ�ؼСA���Cĵ��
            if (currentAlertLevel < 100)
            {
                currentAlertLevel -= lowAlertDecreaseRate * aiUpdateInterval;
            }
            else
            {
                // ��ĵ�٫ץH�W�A�ݭn�@�q�ɶ��S���R�~�}�l���C
                timeSinceLastSighting += aiUpdateInterval;
                if (timeSinceLastSighting >= timeToStartDecreasing)
                {
                    currentAlertLevel -= mediumAlertDecreaseRate * aiUpdateInterval;
                }
            }
        }

        // �p�Gĵ�٭ȹF��̰��A������ Alerted ���A
        if (currentAlertLevel >= 200 && movingTarget != null) // �T�O���ؼФ~����
        {
            threatTarget = movingTarget; // �]�w�¯٥ؼ�
            currentState = NpcState.Alerted;
            Debug.Log($"���A����: Searching -> Alerted! ��w�ؼ�: {threatTarget.name}");
            // (�i��) �b�o�̥i�HĲ�o�@�Ƕi�Jĵ�٪��A�����ĩΰʵe
        }
    }

    // Alerted ���A�޿�
    private void AlertedState()
    {
        agent.speed = chaseSpeed; // ������l���t��

        // ĵ�٭��H�ɶ��۵M�U��
        currentAlertLevel -= highAlertDecreaseRate * aiUpdateInterval;

        // �ˬd�¯٥ؼЬO�_�s�b�B�i��
        bool threatIsStillVisible = (threatTarget != null && fov.visibleTargets.Contains(threatTarget));

        if (threatIsStillVisible)
        {
            // A: �ؼХi���A����l��
            agent.SetDestination(threatTarget.position);
            lastSightingPosition = threatTarget.position; // ��s�̫�ݨ��m
            timeSinceLastSighting = 0f; // ���m�p�ɾ�

            // �ˬd�O�_�i�J�����d��
            if (Vector3.Distance(transform.position, threatTarget.position) < captureDistance)
            {
                Debug.Log($"���ؼ�: {threatTarget.name}!");
                // (���n) ����e���q TeamManager �����A�קK��b�Ĭ�
                if (teamManager != null) teamManager.RemoveCharacterFromTeam(threatTarget.gameObject);
                TriggerPickup(threatTarget); // Ĳ�o�߬B (�`�N: TriggerPickup �| stop agent)

                // ���m���A
                threatTarget = null;
                currentState = NpcState.Searching;
                currentAlertLevel = 0;
                // �`�N: agent.isStopped �b PickupEnd �ƥ󤤷|�]�^ false
                return; // �������� Alerted �޿�
            }
        }
        else
        {
            // B: �ؼХᥢ
            // �e���̫�w����m
            if (agent.destination != lastSightingPosition) // �קK���Ƴ]�m
            {
                agent.SetDestination(lastSightingPosition);
            }


            // C: �b�e���~���ݨ�s�����ʥؼ�
            Transform newMovingTarget = CheckForMovingTargets(); // �A���ˬd����
            if (newMovingTarget != null && newMovingTarget != threatTarget) // �T�O�O�s�ؼ�
            {
                Debug.Log($"�D�n�ؼ� {threatTarget?.name ?? "NULL"} �ᥢ�I�o�{�s�ؼ�: {newMovingTarget.name}");
                threatTarget = newMovingTarget; // �����¯�
                currentAlertLevel = 200f; // ���mĵ�١A���O�l���s�ؼ�
                lastSightingPosition = threatTarget.position; // ��s�̫��m
                timeSinceLastSighting = 0f; // ���m�p��
                // ���ݭn return�A�U�@���|�����i�J���p A
            }
            // D: �w��F�̫��m�A�B���o�{�s�ؼ�
            else if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance) // �� stoppingDistance �P�_��ǽT
            {
                // �p�G�@�p�q�ɶ����S�A�ݨ�ؼСA�h���
                timeSinceLastSighting += aiUpdateInterval;
                if (timeSinceLastSighting > timeToStartDecreasing * 2f) // ���ɶ��i�H�]���@�I
                {
                    Debug.Log("�b�̫�w����m�j���L�G�A��^�j�����A�C");
                    threatTarget = null;
                    currentState = NpcState.Searching;
                    // agent.isStopped �b SearchingState �}�l�ɷ|�Q Patrol() �B�z
                }
            }
        }

        // �p�Gĵ�٭Ȧ۵M����@�w�{�סA�]��^�j�� (�Ҧp < 50?)
        if (currentAlertLevel < 100) // �H�ȥi�H�վ�
        {
            Debug.Log("ĵ�٭ȤU���A�Ѱ�ĵ�٪��A�C");
            threatTarget = null;
            currentState = NpcState.Searching;
        }
    }

    // �ˬd�������O�_������b����
    private Transform CheckForMovingTargets()
    {
        Transform detectedMovingTarget = null;
        if (fov == null || fov.visibleTargets == null) return null; // ����

        foreach (Transform target in fov.visibleTargets)
        {
            if (target == null) continue; // ����

            // �����ݨ�A�O����m
            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue;
            }

            // �p�Ⲿ�ʶZ��
            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);

            // �P�_�O�_�W�L�����H�� (�Ҽ{��s���j)
            if (aiUpdateInterval > 0 && (distanceMoved / aiUpdateInterval) > movementThreshold)
            {
                // �o�{���ʥؼСI
                detectedMovingTarget = target;
                // lastSightingPosition = target.position; // �b���A�޿�̧�s��X�A
                // Debug.Log($"Detected moving target: {target.name}, Speed: {distanceMoved / aiUpdateInterval}");
            }

            // ��s�ؼЪ��w����m
            lastKnownPositions[target] = target.position;
        }

        // --- �M�z�w���}�������ؼаO�� ---
        // �ϥ� List �קK�b���N�ɭק� Dictionary
        List<Transform> targetsToForget = new List<Transform>();
        foreach (var pair in lastKnownPositions)
        {
            // �p�G�O�������ؼФ��b��e�i���ؼЦC���
            if (!fov.visibleTargets.Contains(pair.Key))
            {
                targetsToForget.Add(pair.Key);
            }
        }
        // �q Dictionary �������o�ǥؼ�
        foreach (Transform target in targetsToForget)
        {
            lastKnownPositions.Remove(target);
        }

        return detectedMovingTarget;
    }

    // ���� NPC �b�����I��������
    private void Patrol()
    {
        // �ˬd�O�_�������I�H�� Agent �O�_�N��
        if (patrolPoints == null || patrolPoints.Count == 0 || agent == null || !agent.enabled || agent.isStopped) return;

        // �p�G�w��F�ت��a (�α���ت��a)
        // �ϥ� stoppingDistance �P�_��i�a
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // �e���U�@�Ө����I
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            // �ˬd�U�@�Ө����I�O�_�s�b
            if (patrolPoints[currentPatrolIndex] != null)
            {
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                // Debug.Log($"Patrolling to point {currentPatrolIndex}: {patrolPoints[currentPatrolIndex].name}");
            }
            else
            {
                Debug.LogWarning($"Patrol point at index {currentPatrolIndex} is null!");
                // �i�H��ܸ��L�o���I�ΰ����
                // currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count; // ���L
            }

        }
    }
}