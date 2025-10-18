using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    // ... (所有參數保持不變) ...
    [Header("元件參考")]
    public Transform cameraTransform;
    [Tooltip("指定一個子物件，物件將始終朝向該子物件的位置")]
    public Transform orientationTarget; // 現在代表看向的目標點
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private TeamManager teamManager;
    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;
    [SerializeField] private float rotationSpeed = 10f;
    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [Header("地面檢測")]
    [SerializeField][Range(0.1f, 1f)] private float groundCheckRadiusModifier = 0.9f;
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

        // 自動查找 Orientation Target
        if (orientationTarget == null)
        {
            orientationTarget = transform.Find("OrientationTarget");
            if (orientationTarget == null)
            {
                Debug.LogWarning($"PlayerMovement on {gameObject.name} does not have OrientationTarget assigned or found. Rotation will follow movement direction.", this);
            }
        }
    }

    // ... (OnEnable, OnDisable, Update, FixedUpdate 保持不變) ...
    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
    }
    private void OnDisable()
    {
        if (playerActions != null)
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
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        // --- 3. ▼▼▼ 核心修改：計算看向目標點的方向 ▼▼▼ ---
        Vector3 lookDirection = Vector3.zero;

        if (orientationTarget != null)
        {
            // 計算從 Rigidbody 的位置指向 OrientationTarget 位置的向量
            lookDirection = orientationTarget.position - rb.position; // 使用 rb.position 更精確
        }
        else if (moveDirection.sqrMagnitude > 0.01f) // Fallback to move direction
        {
            lookDirection = moveDirection;
        }

        // 壓平到水平面
        lookDirection.y = 0;

        // 只有在計算出有效的、非零的水平朝向時才進行旋轉
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            lookDirection.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
            rb.MoveRotation(newRotation);
        }
        // --- ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲ ---
    }

    // --- GroundCheck, HandlePossessedHighlight, OnAddToTeam, HandleJump, ApplyExtraGravity, OnDrawGizmosSelected 保持不變 ---
    private void GroundCheck()
    {
        // ... (保持不變) ...
        if (capsuleCollider == null) return;
        Vector3 castOriginOffset = capsuleCollider.center;
        float halfExtent; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        switch (capsuleCollider.direction)
        {
            case 0: case 2: halfExtent = capsuleCollider.radius; break; // Horizontal
            case 1: default: halfExtent = capsuleCollider.height / 2f; break; // Vertical
        }
        Vector3 castOrigin = transform.TransformPoint(castOriginOffset); // Use TransformPoint for rotation
        float castDistance = halfExtent - castRadius + groundCheckLeeway;
        if (castDistance < 0.01f) castDistance = 0.01f;
        LayerMask combinedMask = groundLayer | platformLayer;
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, combinedMask);
    }
    private void HandlePossessedHighlight()
    {
        // ... (保持不變) ...
        if (cameraTransform == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        HighlightableObject hitHighlightable = null;
        float hitDistance = interactionDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.transform.root != transform.root)
            { // Check root to avoid self-highlight
                hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
                if (hitHighlightable != null) hitDistance = hit.distance;
            }
        }
        if (hitHighlightable != currentlyTargetedPlayerObject)
        {
            if (currentlyTargetedPlayerObject != null) currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player"))
            {
                currentlyTargetedPlayerObject = hitHighlightable;
                currentlyTargetedPlayerObject.SetTargetedHighlight(true);
            }
            else { currentlyTargetedPlayerObject = null; }
        }
        if (currentlyTargetedPlayerObject != null)
        {
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, hitDistance);
            float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            currentlyTargetedPlayerObject.SetOutlineWidth(newWidth);
        }
    }
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        // ... (保持不變) ...
        if (teamManager == null) return;
        if (currentlyTargetedPlayerObject != null)
        {
            GameObject targetObject = currentlyTargetedPlayerObject.transform.root.gameObject;
            bool success = teamManager.TryAddCharacterToTeam(targetObject);
            if (success && currentlyTargetedPlayerObject != null)
            {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false); // Remove highlight after adding
                currentlyTargetedPlayerObject = null;
            }
        }
    }
    private void HandleJump()
    {
        // ... (保持不變) ...
        if (playerActions == null || rb == null) return;
        if (playerActions.Player.Jump.IsPressed() && IsGrounded)
        {
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }
    private void ApplyExtraGravity()
    {
        // ... (保持不變) ...
        if (rb == null) return;
        if (!IsGrounded && rb.linearVelocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }
    private void OnDrawGizmosSelected()
    {
        // ... (保持不變) ...
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 castOriginOffset = capsuleCollider.center;
        float halfExtent; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        switch (capsuleCollider.direction)
        {
            case 0: case 2: halfExtent = capsuleCollider.radius; break;
            case 1: default: halfExtent = capsuleCollider.height / 2f; break;
        }
        Vector3 castOrigin = transform.TransformPoint(castOriginOffset); // Use TransformPoint
        float castDistance = halfExtent - castRadius + groundCheckLeeway;
        if (castDistance < 0.01f) castDistance = 0.01f;
        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);
    }
}