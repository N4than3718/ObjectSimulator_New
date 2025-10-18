using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 需要 List
using System.Linq; // 需要 Linq

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("元件參考")]
    [Tooltip("TeamManager 會在啟用時自動設定這個")]
    public Transform cameraTransform; // 現在代表角色自己的攝影機 Transform
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

    // --- 新增：操控狀態下的互動與高亮 ---
    [Header("Possessed Mode Interaction & Highlighting")]
    [Tooltip("執行射線檢測的最大距離")]
    [SerializeField] private float interactionDistance = 10f;
    [Tooltip("高亮材質的模板 (需從 Project 中拖入)")]
    [SerializeField] private Material highlightMaterial;
    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;
    // ------------------------------------

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;

    // --- 高亮相關私有變數 ---
    private Renderer currentlyHighlightedPlayer;
    private Material[] originalMaterialsPlayer;
    private Material highlightInstancePlayer;
    // -------------------------

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
        Debug.Log($"{gameObject.name} enabled, subscribing to AddToTeam.");
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.AddToTeam.performed -= OnAddToTeam;
        // 確保停用時清除高亮
        RestoreOriginalMaterialsPlayer();
        Debug.Log($"{gameObject.name} disabled, unsubscribing and restoring materials.");
    }

    void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        // 在 Update 中處理高亮，反應更即時
        HandlePossessedHighlight();
    }

    void FixedUpdate()
    {
        GroundCheck();
        HandleMovement();
        HandleJump();
        ApplyExtraGravity();
    }

    private void HandlePossessedHighlight()
    {
        // 確保我們有攝影機的參考
        if (cameraTransform == null) return;

        // 從當前角色攝影機的中心發射射線
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            // 檢查是否打到 Player Tag 且不是自己
            if (hit.collider.CompareTag("Player") && hit.transform.root.gameObject != gameObject)
            {
                var renderer = hit.transform.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    // 如果瞄準了新物件
                    if (currentlyHighlightedPlayer != renderer)
                    {
                        RestoreOriginalMaterialsPlayer();
                        currentlyHighlightedPlayer = renderer;
                        StoreAndApplyHighlightPlayer();
                    }

                    // 更新輪廓寬度
                    if (highlightInstancePlayer != null)
                    {
                        float distance = hit.distance; // 直接用 RaycastHit 的距離
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightInstancePlayer.SetFloat("_OutlineWidth", newWidth);
                    }
                    return; // 處理完畢
                }
            }
        }

        // 如果射線沒打到任何東西，或打到的不是有效的目標，就清除高亮
        RestoreOriginalMaterialsPlayer();
    }


    private void StoreAndApplyHighlightPlayer()
    {
        if (currentlyHighlightedPlayer == null || highlightMaterial == null) return;
        originalMaterialsPlayer = currentlyHighlightedPlayer.materials;
        highlightInstancePlayer = new Material(highlightMaterial);
        var newMaterials = originalMaterialsPlayer.ToList();
        newMaterials.Add(highlightInstancePlayer);
        currentlyHighlightedPlayer.materials = newMaterials.ToArray();
    }

    private void RestoreOriginalMaterialsPlayer()
    {
        if (currentlyHighlightedPlayer != null && originalMaterialsPlayer != null)
        {
            currentlyHighlightedPlayer.materials = originalMaterialsPlayer;
        }
        currentlyHighlightedPlayer = null;
        originalMaterialsPlayer = null;
        if (highlightInstancePlayer != null)
        {
            Destroy(highlightInstancePlayer);
            highlightInstancePlayer = null;
        }
    }


    // ▼▼▼ 核心修改：OnAddToTeam 現在使用高亮的目標 ▼▼▼
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;

        // 檢查是否有物件正被高亮
        if (currentlyHighlightedPlayer != null)
        {
            // 獲取高亮物件的根 GameObject (父物件)
            GameObject targetObject = currentlyHighlightedPlayer.transform.root.gameObject;
            Debug.Log($"Requesting to add highlighted object {targetObject.name} to team.");
            bool success = teamManager.TryAddCharacterToTeam(targetObject);
            if (success)
            {
                // 添加成功後，可以選擇清除高亮，避免視覺殘留
                RestoreOriginalMaterialsPlayer();
            }
        }
        else
        {
            Debug.Log("No target highlighted to add to team.");
            // 可以加個音效提示
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲


    // --- 其他函式保持不變 ---
    private void GroundCheck()
    {
        Vector3 castOrigin; float castRadius = capsuleCollider.radius * groundCheckRadiusModifier; float castDistance;
        switch (capsuleCollider.direction)
        { /* ... */
            case 0: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 1: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            case 2: castOrigin = transform.position + new Vector3(capsuleCollider.center.x, capsuleCollider.center.y + (capsuleCollider.height / 2f) - castRadius, capsuleCollider.center.z); castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
            default: castOrigin = transform.position + capsuleCollider.center; castDistance = (capsuleCollider.height / 2f) - castRadius + groundCheckLeeway; break;
        }
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, groundLayer);
    }
    private void HandleMovement()
    {
        if (cameraTransform == null) return; // 確保有攝影機參考
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
        // 地面檢測 Gizmos
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
        // 移除 addToTeamRadius 的 Gizmo
    }
}