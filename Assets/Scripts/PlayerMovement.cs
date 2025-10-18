using UnityEngine;
using UnityEngine.InputSystem;
// using System.Collections.Generic; // 不再需要
// using System.Linq; // 不再需要

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    // ... (大部分參數和引用保持不變) ...
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

    // ▼▼▼ 移除高亮材質模板和動態輪廓參數 ▼▼▼
    // [Header("Possessed Mode Interaction & Highlighting")]
    [Tooltip("執行射線檢測的最大距離")]
    [SerializeField] private float interactionDistance = 10f;
    // [SerializeField] private Material highlightMaterial;
    // [Header("Dynamic Outline")]
    // ...
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;

    // ▼▼▼ 只保留一個引用，指向當前瞄準的 HighlightableObject ▼▼▼
    private HighlightableObject currentlyTargetedPlayerObject;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.AddToTeam.performed -= OnAddToTeam;
        // 確保停用時取消目標高亮
        if (currentlyTargetedPlayerObject != null)
        {
            currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            currentlyTargetedPlayerObject = null;
        }
    }
    void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        HandlePossessedHighlight(); // 依然在 Update 中處理
    }

    // ▼▼▼ 全新的 HandlePossessedHighlight ▼▼▼
    private void HandlePossessedHighlight()
    {
        if (cameraTransform == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        HighlightableObject hitHighlightable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            // 嘗試獲取元件，並排除自己
            if (hit.collider.transform.root != transform.root) // Make sure we don't target ourselves
            {
                hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
            }
        }

        if (hitHighlightable != currentlyTargetedPlayerObject)
        {
            if (currentlyTargetedPlayerObject != null)
            {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            }
            // 只有 Player Tag 的物件可以被瞄準高亮
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
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // StoreAndApplyHighlightPlayer() 和 RestoreOriginalMaterialsPlayer() 整個移除

    // ▼▼▼ OnAddToTeam 現在使用 currentlyTargetedPlayerObject ▼▼▼
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;

        if (currentlyTargetedPlayerObject != null)
        {
            GameObject targetObject = currentlyTargetedPlayerObject.transform.root.gameObject;
            Debug.Log($"Requesting to add highlighted object {targetObject.name} to team.");
            bool success = teamManager.TryAddCharacterToTeam(targetObject);
            // 添加成功後 TeamManager 會處理狀態，這裡不需要特別清除高亮
            // 但我們可以取消瞄準狀態
            if (success && currentlyTargetedPlayerObject != null)
            {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);
                currentlyTargetedPlayerObject = null;
            }

        }
        else
        {
            Debug.Log("No target highlighted to add to team.");
        }
    }
    // Awake remains the same
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        playerActions = new InputSystem_Actions();
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("PlayerMovement cannot find TeamManager!");
    }
    // OnEnable remains the same
    private void OnEnable()
    {
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
        Debug.Log($"{gameObject.name} enabled, subscribing to AddToTeam.");
    }
    // FixedUpdate, GroundCheck, HandleMovement, HandleJump, ApplyExtraGravity, OnDrawGizmosSelected remain the same
    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
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
        if (cameraTransform == null) return; // Ensure camera reference exists
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
        // GroundCheck Gizmos
        if (capsuleCollider == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 castOrigin; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier; float castDistance;
        switch (capsuleCollider.direction)
        { /* ... */
            case 0: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 1: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 2: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            default: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
        }
        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);

        // Interaction Ray Gizmo (Optional visualization)
        // if (cameraTransform != null) {
        //     Gizmos.color = Color.cyan;
        //    Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * interactionDistance);
        // }
    }


}