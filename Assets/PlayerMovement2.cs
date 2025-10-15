using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement2 : MonoBehaviour
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("地面檢測 (SphereCast)")]
    [SerializeField] private float groundCheckRadius = 0.4f;
    [SerializeField] private float groundCheckDistance = 0.6f;
    [SerializeField] private LayerMask groundLayer;

    // --- 輸入系統 ---
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    // 我們不再需要 jumpRequested 旗號了

    // --- 公開屬性 ---
    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // 我們不再訂閱 Jump 事件
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // 也不需要取消訂閱
    }

    void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        // Update 裡不再處理跳躍輸入
    }

    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
    }

    // OnJumpRequest 函式可以整個刪掉了

    private void GroundCheck()
    {
        Vector3 spherePosition = transform.position + Vector3.up * (groundCheckRadius);
        IsGrounded = Physics.SphereCast(spherePosition, groundCheckRadius, Vector3.down, out _, groundCheckDistance, groundLayer);
    }

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

    // ▼▼▼ 核心修改在這裡 ▼▼▼
    private void HandleJump()
    {
        // 直接在物理幀檢查「跳躍鍵是否被按住」以及「是否在地上」
        if (playerActions.Player.Jump.IsPressed() && IsGrounded)
        {
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void ApplyExtraGravity()
    {
        if (!IsGrounded && rb.linearVelocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 startPoint = transform.position + Vector3.up * (groundCheckRadius);
        Gizmos.DrawWireSphere(startPoint, groundCheckRadius);
        Gizmos.DrawWireSphere(startPoint + Vector3.down * groundCheckDistance, groundCheckRadius);
    }
}