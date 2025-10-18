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

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("地面檢測")]
    [SerializeField][Range(0.1f, 1f)] private float groundCheckRadiusModifier = 0.9f;
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("隊伍互動")]
    [SerializeField] private float addToTeamRadius = 2.0f;

    private InputSystem_Actions playerActions;

    // ▼▼▼ 核心修改：將 input 移到這裡宣告 ▼▼▼
    private Vector2 moveInput;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

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
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.AddToTeam.performed -= OnAddToTeam;
    }

    void Update()
    {
        // Update 只負責讀取輸入，並寫入到成員變數 moveInput
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement(); // HandleMovement 會讀取成員變數 moveInput
        HandleJump();
        ApplyExtraGravity();
    }

    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, addToTeamRadius);
        GameObject closestControllable = null;
        float closestDistanceSqr = addToTeamRadius * addToTeamRadius + 1f;
        foreach (Collider hitCollider in nearbyColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.transform.root.gameObject != gameObject)
            {
                GameObject potentialTarget = hitCollider.transform.root.gameObject;
                float distSqr = (transform.position - potentialTarget.transform.position).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestControllable = potentialTarget;
                }
            }
        }
        if (closestControllable != null)
        {
            teamManager.TryAddCharacterToTeam(closestControllable);
        }
    }

    private void GroundCheck()
    {
        Vector3 castOrigin; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier; float castDistance;
        switch (capsuleCollider.direction)
        {
            case 0: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 1: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 2: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            default: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
        }
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, groundLayer);
    }

    private void HandleMovement()
    {
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        // ▼▼▼ 現在這裡使用的 moveInput 是成員變數，FixedUpdate 可以讀取到 ▼▼▼
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        Vector3 targetVelocity = moveDirection * CurrentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
    }

    private void HandleJump()
    {
        if (playerActions.Player.Jump.IsPressed() && IsGrounded)
        {
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    private void ApplyExtraGravity()
    {
        if (!IsGrounded && rb.linearVelocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (capsuleCollider == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 castOrigin; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier; float castDistance;
        switch (capsuleCollider.direction)
        {
            case 0: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 1: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 2: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            default: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
        }
        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, addToTeamRadius);
    }
}