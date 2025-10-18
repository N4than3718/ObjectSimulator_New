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
    [Tooltip("指定一個子物件，其 Z 軸 (藍色軸) 將定義物件的『前方』")]
    public Transform orientationTarget;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private TeamManager teamManager; // 引用 TeamManager

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

    [Tooltip("指定哪些圖層被視為『可站立的平台或物件』（例如其他玩家）")]
    [SerializeField] private LayerMask platformLayer;

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

        if (orientationTarget == null)
        {
            orientationTarget = transform.Find("OrientationTarget"); // 嘗試找名為 "OrientationTarget" 的子物件
            if (orientationTarget == null)
            {
                Debug.LogWarning($"PlayerMovement on {gameObject.name} does not have OrientationTarget assigned or found. Rotation might not work as intended.", this);
                // 可以選擇指向自己作為備用
                orientationTarget = transform;
            }
        }
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
        if (capsuleCollider == null) return;

        Vector3 castOriginOffset = capsuleCollider.center;
        float halfExtent; // 代表從中心到碰撞體底部的距離
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;

        // 根據 CapsuleCollider 的方向計算半高/半長
        switch (capsuleCollider.direction)
        {
            case 0: // X-Axis (水平)
            case 2: // Z-Axis (水平)
                // 水平時，從中心到底部的距離是半徑
                halfExtent = capsuleCollider.radius;
                // 可以稍微調整起點 Y，讓它更貼近理論底部中心，但通常從中心發射更穩定
                // castOriginOffset.y += (capsuleCollider.height / 2f) - castRadius; // 這是舊的錯誤邏輯
                break;
            case 1: // Y-Axis (垂直)
            default:
                // 垂直時，從中心到底部的距離是半高
                halfExtent = capsuleCollider.height / 2f;
                break;
        }

        // 射線起點 = 物件位置 + 碰撞體中心偏移
        Vector3 castOrigin = transform.TransformPoint(castOriginOffset); // 使用 TransformPoint 確保處理旋轉

        // 射線長度 = 從中心到底部的距離 - 檢測球體半徑 + 容錯距離
        // (SphereCast 的 distance 是從球體表面開始算的，所以要減去半徑)
        float castDistance = halfExtent - castRadius + groundCheckLeeway;
        // 確保 castDistance 不為負數
        if (castDistance < 0.01f) castDistance = 0.01f;


        // 執行 SphereCast
        LayerMask combinedMask = groundLayer | platformLayer;
        IsGrounded = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out _, castDistance, combinedMask);

        // (除錯用) 如果持續失敗，印出詳細參數
        // if (!IsGrounded && Time.frameCount % 60 == 0) // 每秒印一次
        // {
        //     Debug.Log($"GroundCheck Failed: Origin={castOrigin}, Radius={castRadius}, Dist={castDistance}, Dir={capsuleCollider.direction}, HalfExt={halfExtent}");
        // }
    }

    // --- 只保留一個 HandleMovement ---
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

        // --- 3. ▼▼▼ 核心修改：使用 Orientation Target 決定旋轉 ▼▼▼ ---
        // 只有在提供了朝向目標時才進行旋轉
        if (orientationTarget != null)
        {
            // 獲取朝向目標的**世界**前方向量，並壓平到水平面
            Vector3 forwardDir = orientationTarget.forward;
            forwardDir.y = 0;
            forwardDir.Normalize();

            // 如果成功計算出有效的水平朝向
            if (forwardDir.sqrMagnitude > 0.01f)
            {
                // 計算目標旋轉 (讓物件的 Y 軸旋轉與 forwardDir 一致)
                Quaternion targetRotation = Quaternion.LookRotation(forwardDir, Vector3.up);
                // 平滑轉向
                Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
                rb.MoveRotation(newRotation);
            }
        }
        // 如果沒有提供 orientationTarget，物件就不會自動旋轉
        // 或者，你可以加一個 else 條件，讓它在沒有 target 時恢復成跟隨移動方向
        else if (moveDirection.sqrMagnitude > 0.01f) // Fallback to move direction if no orientation target
        {
             Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
             Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
             rb.MoveRotation(newRotation);
        }
        // --- ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲ ---
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
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>(); // Try to get it if null
        if (capsuleCollider == null) return; // If still null, exit

        Gizmos.color = IsGrounded ? Color.green : Color.red;

        Vector3 castOriginOffset = capsuleCollider.center;
        float halfExtent;
        float castRadius = capsuleCollider.radius * groundCheckRadiusModifier;

        switch (capsuleCollider.direction)
        {
            case 0:
            case 2: // Horizontal
                halfExtent = capsuleCollider.radius;
                break;
            case 1:
            default: // Vertical
                halfExtent = capsuleCollider.height / 2f;
                break;
        }

        Vector3 castOrigin = transform.TransformPoint(castOriginOffset);
        float castDistance = halfExtent - castRadius + groundCheckLeeway;
        if (castDistance < 0.01f) castDistance = 0.01f;

        // Draw the sphere at the end of the cast
        Gizmos.DrawWireSphere(castOrigin + Vector3.down * castDistance, castRadius);
    }
} // <-- 確保這是 Class 最後的大括號