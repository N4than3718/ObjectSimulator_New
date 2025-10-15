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
    private bool jumpRequested;

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
        playerActions.Player.Jump.performed += OnJumpRequest;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.Jump.performed -= OnJumpRequest;
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

    private void OnJumpRequest(InputAction.CallbackContext context)
    {
        // 收到跳躍指令後，只做一件事：舉起「請求跳躍」的旗子
        jumpRequested = true;
    }

    private void GroundCheck()
    {
        // 射線的起點稍微往上提一點，終點往下延伸，徹底避免動畫干擾
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

        // 直接設定 XZ 軸速度，保留 Y 軸物理速度
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
    }

    private void HandleJump()
    {
        // 在物理幀中，檢查旗子是不是舉起來了，並且是否在地上
        if (jumpRequested && IsGrounded)
        {
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
        // 無論是否成功跳躍，處理完後都把旗子放下
        jumpRequested = false;
    }

    private void ApplyExtraGravity()
    {
        if (!IsGrounded && rb.linearVelocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    // 在 Scene 視窗中畫出地面檢測的範圍，方便除錯
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 startPoint = transform.position + Vector3.up * (groundCheckRadius);
        Gizmos.DrawWireSphere(startPoint, groundCheckRadius);
        Gizmos.DrawWireSphere(startPoint + Vector3.down * groundCheckDistance, groundCheckRadius);
    }
}