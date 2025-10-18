using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour // 確保 Class 名稱是你改過的 PlayerMovement
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private TeamManager teamManager; // 新增 TeamManager 的引用

    // ... (移動、跳躍、重力、地面檢測等參數保持不變) ...
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

    // ▼▼▼ 新增互動參數 ▼▼▼
    [Header("隊伍互動")]
    [Tooltip("可以將物件加入隊伍的最大距離")]
    [SerializeField] private float addToTeamRadius = 2.0f;
    // ▲▲▲▲▲▲▲▲▲▲▲▲

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

        // 獲取 TeamManager 的引用
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("PlayerMovement cannot find TeamManager in the scene!");
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // ▼▼▼ 訂閱新的 AddToTeam 事件 ▼▼▼
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // ▼▼▼ 取消訂閱 ▼▼▼
        playerActions.Player.AddToTeam.performed -= OnAddToTeam;
    }

    void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        // Update 中不再需要處理跳躍
    }

    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
    }

    // ▼▼▼ 新增的事件處理函式 ▼▼▼
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;

        // 1. 在周圍進行球體檢測，尋找帶有 "Controllable" Tag 的物件
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, addToTeamRadius);
        GameObject closestControllable = null;
        float closestDistanceSqr = addToTeamRadius * addToTeamRadius + 1f; // 初始設為比最大距離稍大

        foreach (Collider hitCollider in nearbyColliders)
        {
            // 檢查 Tag 並且排除自己
            if (hitCollider.CompareTag("Controllable") && hitCollider.transform.root.gameObject != gameObject)
            {
                GameObject potentialTarget = hitCollider.transform.root.gameObject;
                float distSqr = (transform.position - potentialTarget.transform.position).sqrMagnitude;

                // 找到最近的那個
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestControllable = potentialTarget;
                }
            }
        }

        // 2. 如果找到了最近的可操控物件，就請求 TeamManager 將其加入
        if (closestControllable != null)
        {
            Debug.Log($"Requesting to add {closestControllable.name} to team.");
            teamManager.TryAddCharacterToTeam(closestControllable);
        }
        else
        {
            Debug.Log("No controllable object found nearby to add.");
            // 可以加個音效提示找不到
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void GroundCheck()
    {
        Vector3 castOrigin;
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;
        float castDistance;
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
        // 畫出地面檢測範圍
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

        // ▼▼▼ 畫出添加隊友的偵測範圍 ▼▼▼
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, addToTeamRadius);
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }
}