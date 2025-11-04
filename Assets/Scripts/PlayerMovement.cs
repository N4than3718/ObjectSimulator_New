using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private Collider coll;
    private Collider[] groundCheckColliders = new Collider[5]; // <--- [新增] 緩衝區
    private TeamManager teamManager;
    private AudioSource audioSource;
    private Animator animator;
    [Tooltip("用於指定 Rigidbody 重心的輔助物件 (可選)")]
    [SerializeField] private Transform centerOfMassHelper;

    [Header("UI Display")] // <-- [新增]
    public Sprite radialMenuIcon;

    [Header("Component Links")]
    public CamControl myCharacterCamera;
    public Transform myFollowTarget;

    [Header("音效設定 (SFX)")]
    [SerializeField] private AudioClip jumpSound;
    [Tooltip("播放跳躍音效前，允許的最大垂直速度")]
    [SerializeField] private float jumpSoundVelocityThreshold = 0.5f;

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;
    [Tooltip("角色轉向的速度")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Animation Settings")]
    [Tooltip("動畫在 1x 速度播放時，對應的玩家移動速度 (m/s)")]
    [SerializeField] private float animationBaseSpeed = 5.0f;

    [Header("物理設定")]
    [Tooltip("未操控時的物理角阻力 (預設 0.05)")]
    [SerializeField] private float uncontrolledAngularDrag = 0.05f;

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float jumpCooldown = 0.2f; // <--- [新增] 兩次跳躍間的最小間隔

    [Header("地面檢測")]
    [SerializeField] private float groundCheckRadius = 0.4f;
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask platformLayer;

    [Header("Possessed Mode Interaction & Highlighting")]
    [SerializeField] private float interactionDistance = 10f;

    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private HighlightableObject currentlyTargetedPlayerObject;
    private bool jumpHeld = false;
    private float lastJumpTime = -Mathf.Infinity;
    private bool isPushing = false;
    private bool isOverEncumbered = false;
    private float currentWeight = 0f;
    private float currentHeavyPushForce = 50f; // (保留預設值)
    private float currentPushInterval = 0.8f;  // (保留預設值)

    public enum CapsuleOrientation { YAxis, XAxis, ZAxis }
    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions != null && playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

        playerActions = new InputSystem_Actions();
        playerActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("PlayerMovement cannot find TeamManager!");

        if (centerOfMassHelper != null && rb != null)
        {
            // 把輔助點的 "本地位置" (localPosition) 設為 Rigidbody 的重心
            rb.centerOfMass = centerOfMassHelper.localPosition;
            Debug.Log($"{name} 的重心已手動設定為 {rb.centerOfMass}");
        }
        else if (rb != null)
        {
            // 如果沒設定輔助點，Unity 會自動計算 (保留預設行為)
            Debug.LogWarning($"{name}: Center of Mass Helper 未設定，使用自動計算的重心 {rb.centerOfMass}。");
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false; // 確保遊戲一開始不會播
        }
    }

    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();
        playerActions.Player.AddToTeam.performed += OnAddToTeam;
        playerActions.Player.Jump.started += OnJumpStarted;
        playerActions.Player.Jump.canceled += OnJumpCanceled;
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.angularDamping = 0.0f;
        }
    }

    private void OnDisable()
    {
        if(playerActions != null)
        {
            playerActions.Player.Disable();
            playerActions.Player.AddToTeam.performed -= OnAddToTeam;
            playerActions.Player.Jump.started -= OnJumpStarted;
            playerActions.Player.Jump.canceled -= OnJumpCanceled;
        }

        if (currentlyTargetedPlayerObject != null)
        {
            currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            currentlyTargetedPlayerObject = null;
        }

        if (rb != null)
        {
            rb.freezeRotation = false;
            rb.angularDamping = uncontrolledAngularDrag;
        }
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        jumpHeld = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpHeld = false;
    }

    /// <summary>
    /// 負責播放跳躍音效 (包含 Debug 檢查)
    /// </summary>
    private void PlayJumpSound()
    {
        if (audioSource != null && jumpSound != null)
        {
            Debug.Log($"PlayJumpSound: Firing audio '{jumpSound.name}'.");
            audioSource.PlayOneShot(jumpSound);
        }
        else
        {
            Debug.LogError($"PlayJumpSound: FAILED safety check.");
            if (audioSource == null) Debug.LogError("Reason: AudioSource is null.");
            if (jumpSound == null) Debug.LogError("Reason: JumpSound is null.");
        }
    }

    void Update()
    {
        if (playerActions == null) return;
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
        HandlePossessedHighlight();

        animator.SetBool("isOverEncumbered", isOverEncumbered); // 通知 Animator

        // 2. 獲取移動方向 (相對於攝影機)
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camForward.y = 0;
        camRight.y = 0;
        Vector3 moveDirection = (camForward.normalized * moveInput.y + camRight.normalized * moveInput.x).normalized;
        bool isTryingToMove = moveDirection.magnitude > 0.1f;

        // 3. 根據狀態決定行為
        if (isOverEncumbered)
        {
            // --- 超重狀態：處理「一段一段」的推 ---
            if (isTryingToMove && !isPushing)
            {
                // 如果玩家按著方向鍵，並且目前沒有在推
                StartCoroutine(HeavyPushCoroutine(moveDirection));
            }
            // 在重推模式下，關閉一般移動的動畫參數
            animator.SetFloat("Speed", 0f);
        }
        else
        {
            // --- 正常狀態：處理「連續」的滑行 ---
            // (FixedUpdate 裡會處理物理移動)
            // 這裡的 Speed 應該反映連續移動的速度
            float currentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
            animator.SetFloat("Speed", currentHorizontalSpeed);
        }
    }

    void FixedUpdate()
    {
        GroundCheck();
        if (!isOverEncumbered && moveInput.magnitude > 0.1f)
        {
            HandleMovement();
        }
        HandleJump();
        ApplyExtraGravity();
        UpdateAnimationParameters();
    }

    /// <summary>
    /// 將 PlayerMovement 的狀態傳遞給 Animator
    /// </summary>
    private void UpdateAnimationParameters()
    {
        if (animator == null) return; // 如果沒掛 Animator 就跳過

        // 1. 取得當前水平速度 (我們不關心 Y 軸)
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        // 2. 傳遞參數
        animator.SetFloat("Speed", horizontalSpeed);
        animator.SetBool("IsGrounded", IsGrounded);

        if (animator == null || rb == null) return; // 防呆

        // 告訴 Animator 播放頻率 (要播多快)
        // (這是新的邏輯)
        
        // 檢查是否正在移動 (避免除以零或在 Idle 時設錯)
        if (horizontalSpeed > 0.1f && animationBaseSpeed > 0f)
        {
            // 計算播放速度 = 當前速度 / 動畫基準速度
            // 例: 當前 6 m/s, 基準 3 m/s -> 播放速度 = 2x
            float playbackSpeed = horizontalSpeed / animationBaseSpeed;

            // (可選) 限制播放速度，避免太鬼畜
            // anim.speed = Mathf.Clamp(playbackSpeed, 0.5f, 2.0f); 

            // 直接設定
            animator.speed = playbackSpeed;
        }
        else
        {
            // 關鍵：待機時，必須把速度重置回 1
            // 不然它會卡在 0，永遠播不了 Idle
            animator.speed = 1.0f; 
        }
    }

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
        // 我們只設定 XZ 軸速度，保留 Y 軸讓物理引擎處理 (重力/跳躍)
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        // --- 3. 處理旋轉 ---
        // 只有在實際移動時才進行旋轉
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            // 計算目標旋轉方向 (只看水平方向)
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            // 使用 Slerp 平滑地轉向目標方向
            // 注意：直接修改 Rigidbody 的 rotation 比修改 transform.rotation 更好
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
            rb.MoveRotation(newRotation); // 使用 MoveRotation 更符合物理更新
        }
    }

    private void GroundCheck()
    {
        if (coll == null || rb == null)
        {
            IsGrounded = false;
            Debug.LogError($"GroundCheck FAILED: {gameObject.name} 缺少 Collider 元件!");
            return;
        }

        // 取得碰撞體的世界最低點
        Vector3 objectCenter = coll.bounds.center;
        Vector3 pointFarBelow = objectCenter + (Vector3.down * 10f);
        Vector3 lowestPointOnCollider = coll.ClosestPoint(pointFarBelow);

        // 設定 CheckSphere 參數
        float checkRadius = groundCheckRadius; // 使用 Inspector 的半徑
        float checkDistanceOffset = groundCheckLeeway / 2f; // 把檢測球心設在「最低點」再往下「Leeway」一半的位置
        Vector3 checkSphereCenter = lowestPointOnCollider + (Vector3.down * checkDistanceOffset);
        LayerMask combinedMask = groundLayer | platformLayer;

        int numCollidersFound = Physics.OverlapSphereNonAlloc(
         checkSphereCenter,
         checkRadius,
         groundCheckColliders, // 存入緩衝區
         combinedMask,
         QueryTriggerInteraction.Ignore
     );

        bool foundGround = false;
        if (numCollidersFound > 0)
        {
            for (int i = 0; i < numCollidersFound; i++)
            {
                // 檢查碰到的 Collider 是不是 *不是* 我們自己
                if (groundCheckColliders[i].attachedRigidbody != rb)
                {
                    foundGround = true; // 只要碰到任何一個 *不是自己* 的東西，就當作在地上
                    break; // 找到就不用再查了
                }
            }
        }

        IsGrounded = foundGround;
    }

    private void HandlePossessedHighlight()
    {
        if (cameraTransform == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        HighlightableObject hitHighlightable = null;
        float hitDistance = interactionDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance)) {
            if(hit.collider.transform.root != transform.root) {
                 hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
                 if (hitHighlightable != null) hitDistance = hit.distance;
            }
        }
        if (hitHighlightable != currentlyTargetedPlayerObject) {
            if (currentlyTargetedPlayerObject != null) currentlyTargetedPlayerObject.SetTargetedHighlight(false);
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player")) {
                 currentlyTargetedPlayerObject = hitHighlightable;
                 currentlyTargetedPlayerObject.SetTargetedHighlight(true);
            } else { currentlyTargetedPlayerObject = null; }
        }
        if (currentlyTargetedPlayerObject != null) {
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, hitDistance);
            float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            currentlyTargetedPlayerObject.SetOutlineWidth(newWidth);
        }
    }
    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;
        if (currentlyTargetedPlayerObject != null) {
            GameObject targetObject = currentlyTargetedPlayerObject.transform.root.gameObject;
            bool success = teamManager.TryAddCharacterToTeam(targetObject);
             if (success && currentlyTargetedPlayerObject != null) {
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);
                currentlyTargetedPlayerObject = null;
            }
        }
    }
    private void HandleJump()
    {
        if (playerActions == null || rb == null) return;

        bool freshJumpPressed = playerActions.Player.Jump.WasPressedThisFrame();

        bool heldJumpActive = jumpHeld;

        bool canJump = Time.fixedTime > lastJumpTime + jumpCooldown; // 使用 fixedTime

        if (freshJumpPressed || heldJumpActive && IsGrounded && canJump)
        {
            float currentVerticalVelocity = rb.linearVelocity.y;
            bool canPlaySound = Mathf.Abs(currentVerticalVelocity) < jumpSoundVelocityThreshold;

            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);

            if (canPlaySound)
            {
                PlayJumpSound(); // 呼叫獨立的播放方法
            }

            lastJumpTime = Time.fixedTime;
        }
    }

    private void ApplyExtraGravity()
    {
        if (rb == null) return;
        if (!IsGrounded && rb.linearVelocity.y < 0) {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (coll == null) coll = GetComponent<Collider>();
        if (coll == null) return;

        // --- [修改] 繪製 GroundCheck Gizmos ---
        Gizmos.color = IsGrounded ? Color.green : Color.red;

        float checkRadius = groundCheckRadius;
        float checkDistanceOffset = groundCheckLeeway / 2f;

        // 計算 Gizmo 位置 (如果 coll.bounds 還沒準備好，可能會在 (0,0,0))
        Vector3 objectCenter = coll.bounds.center;
        if (objectCenter == Vector3.zero && transform.position != Vector3.zero)
        {
            objectCenter = transform.position; // 備案
        }

        Vector3 pointFarBelow = objectCenter + (Vector3.down * 10f);
        Vector3 lowestPointOnCollider = coll.ClosestPoint(pointFarBelow);
        Vector3 checkSphereCenter = lowestPointOnCollider + (Vector3.down * checkDistanceOffset);

        Gizmos.DrawWireSphere(checkSphereCenter, checkRadius);

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Gizmos.color = Color.cyan; // 用青色顯示
                                       // 將本地的 centerOfMass 點轉換為世界座標
            Vector3 worldCoM = transform.TransformPoint(rb.centerOfMass);
            Gizmos.DrawWireSphere(worldCoM, 0.1f);
            Gizmos.DrawLine(transform.position, worldCoM); // 從物件中心拉一條線過去
        }
    }

    /// <summary>
    /// (Public Setter) 允許 BoxContainer 更新此物件的所有重量相關狀態
    /// </summary>
    public void SetWeightAndPushStats(float newWeight, bool isNowOverEncumbered, float pushForce, float pushInterval)
    {
        currentWeight = newWeight;
        isOverEncumbered = isNowOverEncumbered;
        currentHeavyPushForce = pushForce;
        currentPushInterval = pushInterval;
    }

    /// <summary>
    /// (Public Getter) 允許其他腳本讀取當前的重量
    /// </summary>
    public float GetCurrentWeight()
    {
        return currentWeight;
    }

    private IEnumerator HeavyPushCoroutine(Vector3 pushDirection)
    {
        isPushing = true; // 鎖定

        // 1. 觸發「發力」動畫
        animator.SetTrigger("DoPush"); // (你需要一個叫 "DoPush" 的 Trigger)

        // 2. 施加物理力 (等待物理幀)
        yield return new WaitForFixedUpdate();
        rb.AddForce(pushDirection * currentHeavyPushForce, ForceMode.Impulse); // <-- 使用 current

        // 3. 等待動畫/間隔結束
        yield return new WaitForSeconds(currentPushInterval); // <-- 使用 current

        isPushing = false; // 解鎖
    }
}