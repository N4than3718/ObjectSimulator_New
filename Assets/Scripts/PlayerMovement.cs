using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 恢復 List
using System.Linq; // 恢復 Linq

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour // 確保 Class 名稱是你改過的 PlayerMovement
{
    [Header("元件參考")]
    [Tooltip("TeamManager 會在啟用時自動設定這個")]
    public Transform cameraTransform; // 現在代表角色自己的攝影機 Transform
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private TeamManager teamManager; // 引用 TeamManager

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

    // --- 操控狀態下的互動與高亮 ---
    [Header("Possessed Mode Interaction & Highlighting")]
    [Tooltip("執行射線檢測的最大距離")]
    [SerializeField] private float interactionDistance = 10f;
    // 高亮材質模板現在由 HighlightableObject 管理，這裡不需要了
    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;
    // ------------------------------------

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;

    // --- 高亮相關私有變數 ---
    private HighlightableObject currentlyTargetedPlayerObject;
    // -------------------------

    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions != null && playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed; // 加入 null 檢查

    // --- 只保留一個 Awake ---
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        playerActions = new InputSystem_Actions();
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("PlayerMovement cannot find TeamManager!");
    }

    // --- 只保留一個 OnEnable ---
    private void OnEnable()
    {
        // 確保 playerActions 已經初始化
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
        Debug.Log($"{gameObject.name} enabled, subscribing to AddToTeam.");
    }

    // --- 只保留一個 OnDisable ---
    private void OnDisable()
    {
        // 可能在物件銷毀時呼叫，加入 null 檢查
        if (playerActions != null)
        {
            playerActions.Player.Disable();
            playerActions.Player.AddToTeam.performed -= OnAddToTeam;
        }
        // 確保停用時清除高亮
        if (currentlyTargetedPlayerObject != null)
        {
            currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            currentlyTargetedPlayerObject = null;
        }
        Debug.Log($"{gameObject.name} disabled, unsubscribing and restoring materials.");
    }

    // --- 只保留一個 Update ---
    void Update()
    {
        if (playerActions == null) return; // 保護
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        HandlePossessedHighlight();
    }

    // --- 只保留一個 FixedUpdate ---
    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
    }

    // --- 高亮邏輯 ---
    private void HandlePossessedHighlight()
    {
        if (cameraTransform == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        HighlightableObject hitHighlightable = null;
        float hitDistance = interactionDistance;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.transform.root != transform.root)
            {
                hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
                if (hitHighlightable != null) hitDistance = hit.distance;
            }
        }

        if (hitHighlightable != currentlyTargetedPlayerObject)
        {
            if (currentlyTargetedPlayerObject != null)
            {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            }
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player"))
            {
                currentlyTargetedPlayerObject = hitHighlightable;
                currentlyTargetedPlayerObject.SetTargetedHighlight(true);
            }
            else
            {
                currentlyTargetedPlayerObject = null;
            }
        }

        // 更新輪廓寬度
        if (currentlyTargetedPlayerObject != null)
        {
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, hitDistance);
            float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            currentlyTargetedPlayerObject.SetOutlineWidth(newWidth);
        }
    }

    // --- 添加隊友邏輯 ---
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;
        if (currentlyTargetedPlayerObject != null)
        {
            GameObject targetObject = currentlyTargetedPlayerObject.transform.root.gameObject;
            Debug.Log($"Requesting to add highlighted object {targetObject.name} to team.");
            bool success = teamManager.TryAddCharacterToTeam(targetObject);
            if (success && currentlyTargetedPlayerObject != null)
            {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);
                currentlyTargetedPlayerObject = null;
            }
        }
        else { Debug.Log("No target highlighted to add to team."); }
    }

    // --- 只保留一個 GroundCheck ---
    private void GroundCheck()
    {
        if (capsuleCollider == null) return; // 保護
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

    // --- 只保留一個 HandleMovement ---
    private void HandleMovement()
    {
        if (cameraTransform == null || rb == null || playerActions == null) return; // 保護
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        Vector3 targetVelocity = moveDirection * CurrentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
    }

    // --- 只保留一個 HandleJump ---
    private void HandleJump()
    {
        if (playerActions == null || rb == null) return; // 保護
        if (playerActions.Player.Jump.IsPressed() && IsGrounded)
        {
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    // --- 只保留一個 ApplyExtraGravity ---
    private void ApplyExtraGravity()
    {
        if (rb == null) return; // 保護
        if (!IsGrounded && rb.linearVelocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration); // 不需要乘以 mass，Acceleration 模式會處理
        }
    }

    // --- 只保留一個 OnDrawGizmosSelected ---
    private void OnDrawGizmosSelected()
    {
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>(); // 嘗試重新獲取
        if (capsuleCollider == null) return;

        // GroundCheck Gizmos
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

        // Interaction Ray Gizmo (Optional)
        // if (cameraTransform != null) { Gizmos.color = Color.cyan; Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * interactionDistance); }
    }
} // <-- 確保這是 Class 最後的大括號