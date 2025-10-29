using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private TeamManager teamManager;
    private AudioSource audioSource;
    [Tooltip("用於指定 Rigidbody 重心的輔助物件 (可選)")]
    [SerializeField] private Transform centerOfMassHelper;

    [Header("UI Display")] // <-- [新增]
    public Sprite radialMenuIcon;

    [Header("Component Links")]
    public CamControl myCharacterCamera;
    public Transform myFollowTarget;

    [Header("音效設定 (SFX)")]
    [SerializeField] private AudioClip jumpSound;

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;
    [Tooltip("角色轉向的速度")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("物理設定")]
    [Tooltip("未操控時的物理角阻力 (預設 0.05)")]
    [SerializeField] private float uncontrolledAngularDrag = 0.05f;

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("地面檢測")]
    [SerializeField] [Range(0.1f, 1f)] private float groundCheckRadiusModifier = 0.9f;
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask platformLayer;

    [Header("碰撞體設定")]
    [Tooltip("手動指定膠囊碰撞體的朝向，影響地面檢測")]
    [SerializeField] private CapsuleOrientation colliderOrientation = CapsuleOrientation.YAxis;

    [Header("Possessed Mode Interaction & Highlighting")]
    [SerializeField] private float interactionDistance = 10f;

    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private HighlightableObject currentlyTargetedPlayerObject;

    public enum CapsuleOrientation { YAxis, XAxis, ZAxis }
    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions != null && playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        audioSource = GetComponent<AudioSource>();

        playerActions = new InputSystem_Actions();
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("PlayerMovement cannot find TeamManager!");

        if (centerOfMassHelper != null && rb != null)
        {
            // 把輔助點的 "本地位置" (localPosition) 設為 Rigidbody 的重心
            rb.centerOfMass = centerOfMassHelper.localPosition;
            Debug.Log($"{name} 的重心已手動設定為 {rb.centerOfMass}");
        }
        else if (rb != null)
        {
            // 如果沒設定輔助點，Unity 會自動計算 (保留預設行為)
            Debug.LogWarning($"{name}: Center of Mass Helper 未設定，使用自動計算的重心 {rb.centerOfMass}。");
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false; // 確保遊戲一開始不會播
        }
    }

    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
        playerActions.Player.Jump.performed += OnJumpPerformed;

        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.angularDamping = 0.0f;
        }
    }

    private void OnDisable()
    {
        if(playerActions != null)
        {
            playerActions.Player.Disable();
            playerActions.Player.AddToTeam.performed -= OnAddToTeam;
            playerActions.Player.Jump.performed -= OnJumpPerformed;
        }

        if (currentlyTargetedPlayerObject != null)
        {
            currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            currentlyTargetedPlayerObject = null;
        }

        if (rb != null)
        {
            rb.freezeRotation = false;
            rb.angularDamping = uncontrolledAngularDrag;
        }
    }

    /// <summary>
    /// 當跳躍 Action 被執行 (按下) 時觸發 (由 Input System 呼叫)
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        // 只有在剛按下的時候，並且我們正好在地上，才播一次音效
        if (IsGrounded)
        {
            PlayJumpSound(); // 我們把音效播放邏輯抽出來
        }
    }

    /// <summary>
    /// 負責播放跳躍音效 (包含 Debug 檢查)
    /// </summary>
    private void PlayJumpSound()
    {
        if (audioSource != null && jumpSound != null)
        {
            Debug.Log($"PlayJumpSound: Firing audio '{jumpSound.name}'.");
            audioSource.PlayOneShot(jumpSound);
        }
        else
        {
            Debug.LogError($"PlayJumpSound: FAILED safety check.");
            if (audioSource == null) Debug.LogError("Reason: AudioSource is null.");
            if (jumpSound == null) Debug.LogError("Reason: JumpSound is null.");
        }
    }

    void Update()
    {
        if (playerActions == null) return;
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        HandlePossessedHighlight();
    }

    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
    }

    private void HandleMovement()
    {
        if (cameraTransform == null || rb == null || playerActions == null) return;

        // --- 1. 計算移動方向 (保持不變) ---
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // --- 2. 應用移動速度 (保持不變) ---
        Vector3 targetVelocity = moveDirection * CurrentSpeed;
        // 我們只設定 XZ 軸速度，保留 Y 軸讓物理引擎處理 (重力/跳躍)
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        // --- 3. 處理旋轉 ---
        // 只有在實際移動時才進行旋轉
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            // 計算目標旋轉方向 (只看水平方向)
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            // 使用 Slerp 平滑地轉向目標方向
            // 注意：直接修改 Rigidbody 的 rotation 比修改 transform.rotation 更好
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
            rb.MoveRotation(newRotation); // 使用 MoveRotation 更符合物理更新
        }
    }

    private void GroundCheck()
    {
        if (capsuleCollider == null) return;

        Debug.LogError($"Checking {gameObject.name}: Collider Radius={capsuleCollider.radius}, Modifier={groundCheckRadiusModifier}");

        float checkRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        float capsuleHeight = capsuleCollider.height;
        Vector3 capsuleCenterLocal = capsuleCollider.center;

        Vector3 point1, point2;
        float halfHeight = Mathf.Max(capsuleCollider.radius, capsuleHeight / 2f);
        float lineSegmentHalfLength = halfHeight - capsuleCollider.radius;

        switch (colliderOrientation)
        {
            case CapsuleOrientation.XAxis:
                point1 = capsuleCenterLocal + Vector3.left * lineSegmentHalfLength;
                point2 = capsuleCenterLocal + Vector3.right * lineSegmentHalfLength;
                break;
            case CapsuleOrientation.ZAxis:
                point1 = capsuleCenterLocal + Vector3.back * lineSegmentHalfLength;
                point2 = capsuleCenterLocal + Vector3.forward * lineSegmentHalfLength;
                break;
            case CapsuleOrientation.YAxis:
            default:
                point1 = capsuleCenterLocal + Vector3.down * lineSegmentHalfLength;
                point2 = capsuleCenterLocal + Vector3.up * lineSegmentHalfLength;
                break;
        }

        Vector3 point1World = transform.TransformPoint(point1);
        Vector3 point2World = transform.TransformPoint(point2);
        Vector3 lowerSphereCenterWorld = (point1World.y < point2World.y) ? point1World : point2World;
        Vector3 castOriginBase = lowerSphereCenterWorld;
        float liftAmount = 0.01f; // 抬高一點點
        Vector3 castOrigin = castOriginBase + Vector3.up * liftAmount;
        float castDistance = checkRadius + groundCheckLeeway + liftAmount;
        Vector3 castDirection = Vector3.down;

        LayerMask combinedMask = groundLayer | platformLayer;
        bool hitGround = Physics.SphereCast(castOrigin, checkRadius, castDirection, out RaycastHit hitInfo, castDistance, combinedMask);
        IsGrounded = hitGround;

#if UNITY_EDITOR
        string debugInfo = $"Obj: {gameObject.name}, Orient: {colliderOrientation}, LowSphereW: {lowerSphereCenterWorld:F3}, CastOrigin: {castOrigin:F3}, CheckRadius: {checkRadius:F3}, CastDist: {castDistance:F3}";
        Color rayColor = IsGrounded ? Color.green : Color.red;
        Debug.DrawRay(castOrigin, castDirection * (IsGrounded ? hitInfo.distance : castDistance), rayColor, 0.0f, false);

        if (!IsGrounded) // 只在失敗時打印 Log
        {
            Debug.LogWarning($"GroundCheck FAILED! {debugInfo}");

            // 1. 檢查 Layer Mask 是否為空
            if (combinedMask.value == 0)
            {
                Debug.LogError(" -> LayerMasks (groundLayer | platformLayer) ARE EMPTY! Assign layers in Inspector.", this);
            }

            // 2. 進行更長的 Raycast 檢查 (忽略 Layer Mask，看看是否能打到任何東西)
            float longDist = 5f;
            if (Physics.Raycast(castOrigin, castDirection, out RaycastHit rayHit, longDist))
            {
                Debug.LogWarning($" -> Simple Raycast hit '{rayHit.collider.name}' (Layer: {LayerMask.LayerToName(rayHit.collider.gameObject.layer)}) at dist {rayHit.distance:F3}. Was it the wrong layer?");
                // 檢查擊中點是否在預期距離內
                if (rayHit.distance <= castDistance)
                {
                    Debug.LogError($" ---> Raycast hit within expected distance but SphereCast missed! Check LayerMasks or Physics Settings (Queries Hit Triggers?). Collider: {rayHit.collider}", rayHit.collider);
                }
            }
            else
            {
                Debug.LogWarning($" -> Simple Raycast found NOTHING within {longDist}m downwards.");
            }

            // 3. 檢查 Physics.queriesHitTriggers
            bool queriesHitTriggers = Physics.queriesHitTriggers;
            if (queriesHitTriggers)
            {
                // 如果 Ground 是 Trigger Collider，但你不希望檢測到它，需要關閉這個設定
                // Edit -> Project Settings -> Physics -> Queries Hit Triggers (取消勾選)
                Debug.LogWarning(" -> Physics.queriesHitTriggers is TRUE. Is your ground collider accidentally set to 'Is Trigger'?");
            }
            // 反之，如果 Ground 是 Trigger Collider 而你 *希望* 檢測到它，這個必須是 true

            // 4. [驗證] 再次確認 Collider 參數
            // Debug.Log($" -> Collider Params: Center={capsuleCollider.center}, Radius={capsuleCollider.radius}, Height={capsuleCollider.height}, Direction={capsuleCollider.direction}");
            // Debug.Log($" -> PlayerMovement Params: RadiusMod={groundCheckRadiusModifier}, Leeway={groundCheckLeeway}");

        }
#endif
    }

    private void HandlePossessedHighlight()
    {
        if (cameraTransform == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        HighlightableObject hitHighlightable = null;
        float hitDistance = interactionDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance)) {
            if(hit.collider.transform.root != transform.root) {
                 hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
                 if (hitHighlightable != null) hitDistance = hit.distance;
            }
        }
        if (hitHighlightable != currentlyTargetedPlayerObject) {
            if (currentlyTargetedPlayerObject != null) currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player")) {
                 currentlyTargetedPlayerObject = hitHighlightable;
                 currentlyTargetedPlayerObject.SetTargetedHighlight(true);
            } else { currentlyTargetedPlayerObject = null; }
        }
        if (currentlyTargetedPlayerObject != null) {
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, hitDistance);
            float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            currentlyTargetedPlayerObject.SetOutlineWidth(newWidth);
        }
    }
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;
        if (currentlyTargetedPlayerObject != null) {
            GameObject targetObject = currentlyTargetedPlayerObject.transform.root.gameObject;
            bool success = teamManager.TryAddCharacterToTeam(targetObject);
             if (success && currentlyTargetedPlayerObject != null) {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);
                currentlyTargetedPlayerObject = null;
            }
        }
    }
    private void HandleJump()
    {
        if (playerActions == null || rb == null) return;
        if (playerActions.Player.Jump.IsPressed() && IsGrounded)
        {
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    private void ApplyExtraGravity()
    {
        if (rb == null) return;
        if (!IsGrounded && rb.linearVelocity.y < 0) {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;

        float checkRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        float capsuleHeight = capsuleCollider.height;
        Vector3 capsuleCenterLocal = capsuleCollider.center;

        Vector3 point1, point2;
        float halfHeight = Mathf.Max(capsuleCollider.radius, capsuleHeight / 2f);
        float lineSegmentHalfLength = halfHeight - capsuleCollider.radius;

        switch (colliderOrientation)
        {
            case CapsuleOrientation.XAxis:
                point1 = capsuleCenterLocal + Vector3.left * lineSegmentHalfLength;
                point2 = capsuleCenterLocal + Vector3.right * lineSegmentHalfLength;
                break;
            case CapsuleOrientation.ZAxis:
                point1 = capsuleCenterLocal + Vector3.back * lineSegmentHalfLength;
                point2 = capsuleCenterLocal + Vector3.forward * lineSegmentHalfLength;
                break;
            case CapsuleOrientation.YAxis:
            default:
                point1 = capsuleCenterLocal + Vector3.down * lineSegmentHalfLength;
                point2 = capsuleCenterLocal + Vector3.up * lineSegmentHalfLength;
                break;
        }

        Vector3 point1World = transform.TransformPoint(point1);
        Vector3 point2World = transform.TransformPoint(point2);
        Vector3 lowerSphereCenterWorld = (point1World.y < point2World.y) ? point1World : point2World;
        Vector3 castOriginBase = lowerSphereCenterWorld;
        float liftAmount = 0.01f; // 抬高一點點
        Vector3 castOrigin = castOriginBase + Vector3.up * liftAmount;
        float castDistance = checkRadius + groundCheckLeeway + liftAmount;
        Vector3 castDirection = Vector3.down;

        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, checkRadius);

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Gizmos.color = Color.cyan; // 用青色顯示
                                       // 將本地的 centerOfMass 點轉換為世界座標
            Vector3 worldCoM = transform.TransformPoint(rb.centerOfMass);
            Gizmos.DrawWireSphere(worldCoM, 0.1f);
            Gizmos.DrawLine(transform.position, worldCoM); // 從物件中心拉一條線過去
        }
    }
}