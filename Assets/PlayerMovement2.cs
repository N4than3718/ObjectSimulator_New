using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))] // 改回 CapsuleCollider
public class PlayerMovement2 : MonoBehaviour
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider; // 我們需要明確引用它

    // ... (移動、跳躍、重力等參數保持不變) ...
    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;
    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("地面檢測 (自適應)")]
    [Tooltip("檢測球體的半徑，會自動匹配碰撞體。這個值是額外的縮放。")]
    [SerializeField][Range(0.1f, 1f)] private float groundCheckRadiusModifier = 0.9f;
    [Tooltip("額外的射線長度，提供一點容錯空間")]
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    // --- 輸入系統 ---
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;

    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>(); // 取得 CapsuleCollider 的參考
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

    // ▼▼▼▼▼ 核心修改：全新的 GroundCheck ▼▼▼▼▼
    private void GroundCheck()
    {
        // 1. 計算射線的起點：物件的世界座標 + 碰撞體的中心偏移
        Vector3 castOrigin = transform.position + capsuleCollider.center;

        // 2. 計算射線需要行進的距離：從碰撞體中心到其底部邊緣的距離 + 一點額外空間
        float castDistance = (capsuleCollider.height / 2f) - capsuleCollider.radius + groundCheckLeeway;

        // 3. 計算 SphereCast 的半徑：匹配碰撞體的半徑，並稍微縮小一點避免卡牆
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;

        // 4. 執行 SphereCast
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, groundLayer);
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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

    // ▼▼▼ 同步更新 Gizmos，讓你看見真實的檢測範圍 ▼▼▼
    private void OnDrawGizmosSelected()
    {
        if (capsuleCollider == null) return; // 確保在編輯模式下不會報錯

        Gizmos.color = IsGrounded ? Color.green : Color.red;

        Vector3 castOrigin = transform.position + capsuleCollider.center;
        float castDistance = (capsuleCollider.height / 2f) - capsuleCollider.radius + groundCheckLeeway;
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;

        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);
    }
}