using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement2 : MonoBehaviour
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("地面檢測 (全適應性)")]
    [Tooltip("檢測球體半徑相對於碰撞體的縮放")]
    [SerializeField][Range(0.1f, 1f)] private float groundCheckRadiusModifier = 0.9f;
    [Tooltip("提供一點額外的射線長度以應對斜坡")]
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;

    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
    }

    void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
    }

    // ▼▼▼▼▼ 【最終真理】 GroundCheck ▼▼▼▼▼
    private void GroundCheck()
    {
        Vector3 castOrigin;
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        float castDistance;

        // 根據 CapsuleCollider 的方向，使用不同的計算邏輯
        switch (capsuleCollider.direction)
        {
            // 水平: X-Axis
            case 0:
                castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z);
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;

            // 垂直: Y-Axis (預設)
            case 1:
                castOrigin = transform.position + capsuleCollider.center;
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;

            // 水平: Z-Axis
            case 2:
                castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z);
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;

            default: // 備用
                castOrigin = transform.position + capsuleCollider.center;
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;
        }

        // 執行 SphereCast
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, groundLayer);
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void HandleMovement()
    {
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
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
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>();

        Gizmos.color = IsGrounded ? Color.green : Color.red;

        Vector3 castOrigin;
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        float castDistance;

        switch (capsuleCollider.direction)
        {
            case 0:
                castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z);
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;
            case 1:
                castOrigin = transform.position + capsuleCollider.center;
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;
            case 2:
                castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z);
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;
            default:
                castOrigin = transform.position + capsuleCollider.center;
                castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway;
                break;
        }

        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);
    }
}