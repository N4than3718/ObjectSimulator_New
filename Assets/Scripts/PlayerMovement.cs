using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private TeamManager teamManager;

    [Header("Component Links")]
    public CamControl myCharacterCamera;
    public Transform myFollowTarget;

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;
    [Tooltip("角色轉向的速度")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [Header("地面檢測")]
    [SerializeField] [Range(0.1f, 1f)] private float groundCheckRadiusModifier = 0.9f;
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask platformLayer;
    [Header("Possessed Mode Interaction & Highlighting")]
    [SerializeField] private float interactionDistance = 10f;
    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private HighlightableObject currentlyTargetedPlayerObject;

    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions != null && playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        playerActions = new InputSystem_Actions();
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("PlayerMovement cannot find TeamManager!");
    }

    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
    }

    private void OnDisable()
    {
        if(playerActions != null)
        {
            playerActions.Player.Disable();
            playerActions.Player.AddToTeam.performed -= OnAddToTeam;
        }
        if (currentlyTargetedPlayerObject != null)
        {
            currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            currentlyTargetedPlayerObject = null;
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
        Vector3 castOriginOffset = capsuleCollider.center;
        float halfExtent; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        switch (capsuleCollider.direction) {
            case 0: case 2: halfExtent = capsuleCollider.radius; break; // Horizontal
            case 1: default: halfExtent = capsuleCollider.height / 2f; break; // Vertical
        }
        Vector3 castOrigin = transform.TransformPoint(castOriginOffset);
        float castDistance = halfExtent - castRadius + groundCheckLeeway;
        if (castDistance < 0.01f) castDistance = 0.01f;
        LayerMask combinedMask = groundLayer | platformLayer;
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, combinedMask);
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
        if (playerActions.Player.Jump.IsPressed() && IsGrounded) {
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
        Vector3 castOriginOffset = capsuleCollider.center;
        float halfExtent; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        switch (capsuleCollider.direction) {
            case 0: case 2: halfExtent = capsuleCollider.radius; break;
            case 1: default: halfExtent = capsuleCollider.height / 2f; break;
        }
        Vector3 castOrigin = transform.TransformPoint(castOriginOffset);
        float castDistance = halfExtent - castRadius + groundCheckLeeway;
        if (castDistance < 0.01f) castDistance = 0.01f;
        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);
    }
}