using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Current { get; private set; }

    [Header("元件參考")]
    public Transform cameraTransform;
    private Rigidbody rb;
    private Collider coll;
    private Collider[] _groundOverlapResults = new Collider[10];
    private TeamManager teamManager;
    private AudioSource audioSource;
    private Animator animator;
    [Tooltip("用於指定 Rigidbody 重心的輔助物件 (可選)")]
    [SerializeField] private Transform centerOfMassHelper;
    [SerializeField] private Collider movementCollider;

    [Header("UI Display")] // <-- [新增]
    public Sprite radialMenuIcon;

    [Header("Component Links")]
    public CamControl myCharacterCamera;
    public Transform myFollowTarget;
    public CardboardSkill currentCardboard;

    [Header("音效設定 (SFX)")]
    [SerializeField] private AudioClip jumpSound;
    [Tooltip("播放跳躍音效前，允許的最大垂直速度")]
    [SerializeField] private float jumpSoundVelocityThreshold = 0.5f;
    // ▼▼▼ [新增] 腳步聲陣列 ▼▼▼
    [Tooltip("放入多個相似的音效以增加變化 (例如：Step1, Step2, Step3)")]
    [SerializeField] private AudioClip[] footstepSounds;
    // ▼▼▼ [新增] 音調變化範圍 (讓聲音聽起來更自然) ▼▼▼
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    [Header("移動設定")]
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float fastSpeed = 10.0f;
    [Tooltip("角色轉向的速度")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("潛行與噪音設定")] // <--- [新增]
    [SerializeField] private float walkNoiseRange = 5f;  // 走路聲音範圍
    [SerializeField] private float sprintNoiseRange = 10f; // 衝刺聲音範圍
    [SerializeField] private float jumpNoiseRange = 8f;   // 跳躍著地聲音範圍
    [SerializeField] private float noiseFrequency = 0.3f; // 發出聲音的頻率 (秒)

    [Header("Debug 可視化")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1, 1, 0, 0.5f); // 黃色半透明
    [SerializeField] private float gizmoDuration = 1.0f;

    [Header("物理設定")]
    [Tooltip("未操控時的物理角阻力 (預設 0.05)")]
    [SerializeField] private float uncontrolledAngularDrag = 0.05f;
    public float moveDrag = 0f; // 移動時的阻力 (設為 0，讓它滑順)
    public float stopDrag = 10f; // 停止時的阻力 (設高一點，讓它急停)

    [Header("跳躍與重力")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private float jumpCooldown = 0.5f; // <--- [新增] 兩次跳躍間的最小間隔

    [Header("跳躍手感優化")]
    [SerializeField] private float jumpCutMultiplier = 0.5f; // 鬆開按鍵時，垂直速度剩多少 (0.5 = 砍一半)
    [SerializeField] private float airControl = 0.5f; // 0 = 空中完全無法移動, 1 = 跟地面一樣靈活

    [Header("地面檢測")]
    [Tooltip("檢測球的半徑 (越小的物件應該設越小)")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("手動修正起點高度")]
    [SerializeField] private float sinkAmount = 0.15f; // [新增] 手動修正起點高度
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask platformLayer;

    [Header("Interaction & Highlighting")]
    [Tooltip("這是所有互動與準星高亮的標準距離")]
    public float interactionDistance = 3.5f;

    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.008f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    [Header("Optimization")]
    // 🔥 新增：預先配置好的射線碰撞陣列 (大小設為 10 通常夠用了)
    private RaycastHit[] _highlightHits = new RaycastHit[10];

    [Tooltip("射線檢測的層級 (建議排除 Player 層)")]
    public LayerMask interactionLayer = -1; // -1 代表 Everything (預設)

    private InputSystem_Actions playerActions => GameDirector.Instance.playerActions;
    private Vector2 moveInput;
    private HighlightableObject currentlyTargetedPlayerObject;
    private bool jumpHeld = false;
    private float lastJumpTime = -Mathf.Infinity;
    private bool isPushing = false;
    private bool isOverEncumbered = false;
    private float currentWeight = 0f;
    private float currentHeavyPushForce = 50f; // (保留預設值)
    private float currentPushInterval = 0.8f;  // (保留預設值)
    private float sleepTimer = 0f;
    private float noiseTimer = 0f; // 計時器
    private float _lastNoiseTime = -10f;
    private float _lastNoiseRadius;
    private Vector3 _lastNoisePos;
    public enum CapsuleOrientation { YAxis, XAxis, ZAxis }
    public bool IsGrounded { get; private set; }
    public float CurrentHorizontalSpeed { get; private set; }
    private float CurrentSpeed => (playerActions != null && playerActions.Player.Sprint.IsPressed()) ? fastSpeed : playerSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        coll = movementCollider;
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

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
        Current = this;

        if (GameDirector.Instance != null && playerActions != null)
        {
            // 綁定移動偵測 (這是 Update 能動的關鍵)
            playerActions.Player.Move.performed += OnMovePerformed;
            playerActions.Player.Move.canceled += OnMoveCanceled;

            // 原有的綁定
            playerActions.Player.AddToTeam.performed += OnAddToTeam;
            playerActions.Player.Jump.started += OnJumpStarted;
            playerActions.Player.Jump.canceled += OnJumpCanceled;
            playerActions.Player.Attack.performed += OnSelectPerformed;
        }

        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.angularDamping = 0.0f;
        }
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }

        if (GameDirector.Instance != null && GameDirector.Instance.playerActions != null)
        {
            // 💀 [修正] 同步解綁
            playerActions.Player.Move.performed -= OnMovePerformed;
            playerActions.Player.Move.canceled -= OnMoveCanceled;

            playerActions.Player.AddToTeam.performed -= OnAddToTeam;
            playerActions.Player.Jump.started -= OnJumpStarted;
            playerActions.Player.Jump.canceled -= OnJumpCanceled;
            playerActions.Player.Attack.performed -= OnSelectPerformed;
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

    private void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        jumpHeld = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpHeld = false;
    }

    // 當有東西撞到我們時觸發
    private void OnCollisionEnter(Collision collision)
    {
        // 1. 如果我們目前是「鎖死/裝死」狀態 (Kinematic)
        if (rb.isKinematic)
        {
            // 2. 過濾條件：只有被「動態物體」撞到才醒來
            // collision.rigidbody != null 代表撞我的人有物理剛體 (例如 NPC, 其他掉落物)
            // collision.impulse.magnitude > 0.5f 代表撞擊力道夠大 (過濾掉微小的誤觸)
            if (collision.rigidbody != null || collision.impulse.magnitude > 0.5f)
            {
                // 3. 解除封印！變回物理物件
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate; // 記得把畫面平滑開回來

            }
        }
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
        if (cameraTransform == null) return;
        if (!this.enabled) return;
        if (GameDirector.Instance == null || GameDirector.Instance.IsPaused) return;
        if (GameDirector.Instance.CurrentState != GameDirector.GameState.Playing) return;

        if (GameDirector.Instance != null && GameDirector.Instance.playerActions != null)
        {
            moveInput = GameDirector.Instance.playerActions.Player.Move.ReadValue<Vector2>();
        }

        HandlePossessedHighlight();

        // 只要偵測到輸入，立刻解除鎖定，並重置貪睡鐘
        if (moveInput.sqrMagnitude > 0.01f)
        {
            sleepTimer = 0f; // 重置計時器，代表我很活躍

            if (rb.isKinematic)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        // 2. 獲取移動方向 (相對於攝影機)
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
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
        }
    }

    void FixedUpdate()
    {
        GroundCheck(); // 地面檢測

        // 如果附身的是紙箱，更新紙箱動畫
        if (currentCardboard != null)
        {
            currentCardboard.UpdateAnimationState(rb, isOverEncumbered, isPushing);
        }

        // --- 核心移動判斷 ---
        if (!isOverEncumbered && moveInput.magnitude > 0.1f)
        {
            // 🔥 移動時：設定移動阻力 (通常較低，甚至可以是 0，因為上面的 HandleMovement 已經自己處理慣性了)
            rb.linearDamping = moveDrag;
            HandleMovement(); // <--- 呼叫剛剛修好的函式
        }
        else if (!isOverEncumbered && IsGrounded)
        {
            // --- 停止時：處理自動煞車與休眠 ---
            sleepTimer += Time.fixedDeltaTime;

            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

            // 如果速度夠慢且已經停了一陣子，直接鎖定物理 (Kinematic) 省效能
            if (horizontalVel.sqrMagnitude < 0.05f && sleepTimer > 0.5f)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                // 還沒完全停下來，給它高阻力 (stopDrag) 幫忙煞車
                rb.linearDamping = stopDrag;
            }
        }
        else if (!isOverEncumbered)
        {
            // 空中狀態
            sleepTimer = 0f;
            rb.isKinematic = false;
            rb.linearDamping = 0.5f; // 空中給一點點阻力防止無限飄移
        }

        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        HandleJump();
        ApplyExtraGravity();
        HandleMovementNoise();
    }

    private void HandleMovement()
    {
        if (cameraTransform == null || rb == null || playerActions == null) return;

        // --- 1. 計算移動方向 ---
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // 2. 根據是否在地面，決定移動邏輯
        if (IsGrounded)
        {
            // 🔥🔥🔥 [修復重點] 地面移動改用「速度差」計算 🔥🔥🔥
            // 這會讓角色反應變得非常靈敏，想停就停，想轉就轉

            // A. 計算目標速度 (我們希望角色達到的速度)
            Vector3 targetVelocity = moveDirection * CurrentSpeed;

            // B. 取得當前速度
            Vector3 currentVelocity = rb.linearVelocity;

            // C. 計算「需要補償的力」 (目標 - 當前)
            Vector3 velocityChange = targetVelocity - currentVelocity;

            // D. 忽略垂直方向 (不要影響跳躍或重力)
            velocityChange.y = 0;

            // E. 施加力 (使用 VelocityChange 模式，無視質量，瞬間生效)
            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
        else
        {
            // --- 空中邏輯 (保留慣性) ---
            if (moveInput.magnitude > 0.1f)
            {
                Vector3 targetVelocity = moveDirection * CurrentSpeed;
                Vector3 currentVelocity = rb.linearVelocity;
                Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0, currentVelocity.z);

                // 空中給一點點延遲 (Lerp)，不要像地面那麼黏
                Vector3 intendedVelocity = Vector3.Lerp(currentHorizontal, targetVelocity, Time.fixedDeltaTime * airControl * 5f);

                Vector3 velocityChange = intendedVelocity - currentHorizontal;

                rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }
        }

        // --- 3. 處理旋轉 ---
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            float step = rotationSpeed * 72f * Time.fixedDeltaTime;

            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, step));
        }
    }

    private void HandleMovementNoise()
    {
        // 只有在地面上且有移動時才發出聲音
        if (IsGrounded && moveInput.sqrMagnitude > 0.01f)
        {
            noiseTimer += Time.fixedDeltaTime;
            if (noiseTimer >= noiseFrequency)
            {
                bool isSprinting = playerActions != null && playerActions.Player.Sprint.IsPressed();
                float range = isSprinting ? sprintNoiseRange : walkNoiseRange;
                float intensity = isSprinting ? 15f : 5f; // 衝刺加比較多警戒值

                // 發出聲音！
                StealthManager.MakeNoise(transform.position, range, intensity);
                PlayRandomFootstep();

                if (showDebugGizmos)
                {
                    _lastNoiseTime = Time.time;
                    _lastNoiseRadius = range;
                    _lastNoisePos = transform.position;
                }

                noiseTimer = 0f; // 重置計時
            }
        }
        else
        {
            noiseTimer = noiseFrequency; // 停下來時重置，確保下次移動立刻發聲
        }
    }

    // ▼▼▼ [新增] 隨機播放方法 ▼▼▼
    private void PlayRandomFootstep()
    {
        if (audioSource == null || footstepSounds == null || footstepSounds.Length == 0) return;

        // 1. 隨機選一個片段
        int index = Random.Range(0, footstepSounds.Length);
        AudioClip clip = footstepSounds[index];

        // 2. 隨機改變音高 (這是讓聲音不機械化的關鍵！)
        audioSource.pitch = Random.Range(minPitch, maxPitch);

        // 3. 稍微隨機化音量 (可選)
        // audioSource.volume = Random.Range(0.8f, 1.0f);

        // 4. 播放
        audioSource.PlayOneShot(clip);
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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

        float bottomY = coll.bounds.min.y;

        // 建構 SphereCast 的起點
        float radius = groundCheckRadius;
        Vector3 castOrigin = objectCenter;

        if (coll is BoxCollider box)
        {
            castOrigin = transform.TransformPoint(box.center + Vector3.down * (box.size.y * 0.5f - groundCheckRadius + sinkAmount));
        }
        else
        {
            // 其他形狀：確保起點在底部附近
            castOrigin = new Vector3(objectCenter.x, bottomY + groundCheckRadius - sinkAmount, objectCenter.z);
        }

        LayerMask combinedMask = groundLayer | platformLayer;

        int hitCount = Physics.OverlapSphereNonAlloc(
                    castOrigin,
                    groundCheckRadius,
                    _groundOverlapResults,
                    combinedMask,
                    QueryTriggerInteraction.Ignore
                );

        bool validGroundFound = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitColl = _groundOverlapResults[i];

            // A. 排除自己
            if (hitColl.transform.IsChildOf(transform)) continue;
            if (hitColl.attachedRigidbody == rb) continue;

            bool isComplexCollider = (hitColl is MeshCollider mesh && !mesh.convex) || (hitColl is TerrainCollider);

            // 計算方向向量：從「碰撞點」指向「我的中心」
            // 想像一根箭頭從地板射向你的肚子
            Vector3 rayOrigin;

            if (isComplexCollider)
            {
                // 🔥 備案 B：針對地形/複雜網格
                // 因為算不出 ClosestPoint，我們直接假設接觸點就在「腳底正下方」
                // 從物件中心往下發射
                rayOrigin = objectCenter;
            }
            else
            {
                // ✅ 方案 A：針對一般地板 (最精準)
                // 找出最靠近的點，並從那裡稍微往上抬一點當作射線起點
                Vector3 closestPoint = hitColl.ClosestPoint(objectCenter);
                rayOrigin = closestPoint + Vector3.up * 0.5f;
            }

            // C. 判斷角度 (Normal Check)
            // directionToCenter.y > 0.7f 代表這個面大致朝上 (約 45 度以內的坡度)
            // 如果是牆壁，這個值會接近 0；如果是天花板，這個值會是負的
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitInfo, 2.0f, combinedMask))
            {
                // 如果射線打到的 collider 就是我們 overlap 到的這個 (確認沒打錯人)
                if (hitInfo.collider == hitColl)
                {
                    // 使用法線 (Normal) 來判斷坡度
                    // 只要法線跟向上的夾角小於 50 度，就算地板 (包含了平地、斜坡)
                    float angle = Vector3.Angle(hitInfo.normal, Vector3.up);

                    if (angle < 50f)
                    {
                        validGroundFound = true;

                        // Debug: 綠色線代表確認為地板
                        if (showDebugGizmos)
                            Debug.DrawRay(hitInfo.point, hitInfo.normal, Color.green);

                        break; // 找到一個地板就夠了
                    }
                }
            }

            if (!validGroundFound)
            {
                if (isComplexCollider) continue;

                Vector3 closestPoint = hitColl.ClosestPoint(objectCenter);

                bool isBelowCenter = closestPoint.y < objectCenter.y;

                float heightDiff = Mathf.Abs(closestPoint.y - bottomY);
                bool isAtFeetLevel = (closestPoint.y < bottomY + 0.15f) && (closestPoint.y > bottomY - 0.25f);

                // 容許 0.05f 的高度誤差
                if (isBelowCenter && isAtFeetLevel)
                {
                    validGroundFound = true;
                    if (showDebugGizmos) Debug.DrawRay(closestPoint, Vector3.up * 0.2f, Color.yellow); // 黃色表示備案生效
                    break;
                }
            }
        }

        IsGrounded = validGroundFound;
    }

    private void HandlePossessedHighlight()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.red);

        // 取得射線路徑上的碰撞
        int hitCount = Physics.RaycastNonAlloc(
                    ray,
                    _highlightHits,
                    interactionDistance,
                    interactionLayer,
                    QueryTriggerInteraction.Ignore
                );

        // 我們需要找到 "最近的" 且 "不是自己" 的那個
        HighlightableObject closestHighlightable = null;
        float closestDist = float.MaxValue;

        bool doDebugLog = false;
        if (doDebugLog) Debug.Log($"--- Raycast Hit Count: {hitCount} ---");

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _highlightHits[i]; // 取出當前的碰撞資訊
            if (doDebugLog) Debug.Log($"[{i}] Hit: {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");

            // 1. 排除自己
            if (hit.collider.transform.IsChildOf(transform))
            {
                if (doDebugLog) Debug.Log(" -> Ignored (Self)");
                continue;
            }

            // 2. 排除 Trigger
            if (hit.collider.isTrigger)
            {
                if (doDebugLog) Debug.Log(" -> Ignored (Trigger)");
                continue;
            }

            // 3. 取得 Highlight 元件
            // 優化小技巧：先判斷距離是否比目前最近的還近，如果已經比較遠就不用 GetComponent 了 (省效能)
            // 但因為我們有 "Player Tag 優先權" 的邏輯，所以這裡還是得先抓出來看 Tag
            var highlightable = hit.collider.GetComponentInParent<HighlightableObject>();

            if (highlightable != null)
            {
                // --- 距離權重邏輯 (保留上一版的優化) ---
                float modifiedDistance = hit.distance;

                // 如果是物品 (Player Tag)，讓它在判定上「近一點」，解決鑰匙在抽屜裡拿不到的問題
                if (highlightable.CompareTag("Player"))
                {
                    modifiedDistance -= 0.05f;
                }

                if (modifiedDistance < closestDist)
                {
                    closestHighlightable = highlightable;
                    closestDist = modifiedDistance;
                    if (doDebugLog) Debug.Log(" -> 🔥 Candidate Found!");
                }
            }
            else
            {
                if (doDebugLog) Debug.Log(" -> No HighlightableObject Script found on parent.");
            }
        }

        // 處理高亮狀態切換
        HighlightableObject hitHighlightable = closestHighlightable;

        if (hitHighlightable != currentlyTargetedPlayerObject)
        {
            // 關掉舊的
            if (currentlyTargetedPlayerObject != null)
                currentlyTargetedPlayerObject.SetTargetedHighlight(false);

            // 開啟新的
            currentlyTargetedPlayerObject = hitHighlightable;
            if (currentlyTargetedPlayerObject != null)
                currentlyTargetedPlayerObject.SetTargetedHighlight(true);
        }

        // 動態調整線條寬度
        if (currentlyTargetedPlayerObject != null)
        {
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, closestDist);
            float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            currentlyTargetedPlayerObject.SetOutlineWidth(newWidth);
        }
    }

    /// <summary>
    /// 取得當前準星鎖定並高亮的物件 (唯讀)
    /// </summary>
    public GameObject CurrentTargetedObject
    {
        get
        {
            if (currentlyTargetedPlayerObject != null)
            {
                return currentlyTargetedPlayerObject.gameObject;
            }
            return null;
        }
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (!this.enabled || currentlyTargetedPlayerObject == null) return;

        // 💀 [核心邏輯]：如果自己目前有高亮選中某個物件
        CardboardSkill targetCardboard = currentlyTargetedPlayerObject.GetComponentInParent<CardboardSkill>();

        if (targetCardboard != null)
        {
            Debug.Log($"[Interaction] {gameObject.name} 偵測到目標為紙箱，發送收納請求...");
            targetCardboard.RequestStorage(this.gameObject);
            return; // 💀 執行了互動就結束，不要讓 SkillManager 報錯
        }
    }

    private void OnAddToTeam(InputAction.CallbackContext context)
    {
        if (teamManager == null) return;
        if (currentlyTargetedPlayerObject != null) {
            PlayerMovement targetMovement = currentlyTargetedPlayerObject.GetComponentInParent<PlayerMovement>();

            if (targetMovement != null && targetMovement.CompareTag("Player"))
            {
                // 傳入找到腳本的那個 GameObject
                bool success = teamManager.TryAddCharacterToTeam(targetMovement.gameObject);

                if (success)
                {
                    // 如果成功加入，取消高亮
                    currentlyTargetedPlayerObject.SetTargetedHighlight(false);
                    currentlyTargetedPlayerObject = null;
                }
            }
            else
            {
                Debug.LogWarning($"[PlayerMovement] 嘗試招募 {currentlyTargetedPlayerObject.name}，但找不到 PlayerMovement 腳本！");
            }
        }
    }

    private void HandleJump()
    {
        if (playerActions == null || rb == null) return;

        bool freshJumpPressed = playerActions.Player.Jump.WasPressedThisFrame();

        bool heldJumpActive = jumpHeld;

        bool canJump = Time.fixedTime > lastJumpTime + jumpCooldown; // 使用 fixedTime

        if ((freshJumpPressed || heldJumpActive) && IsGrounded && canJump)
        {
            rb.isKinematic = false;
            float currentVerticalVelocity = rb.linearVelocity.y;
            bool canPlaySound = Mathf.Abs(currentVerticalVelocity) < jumpSoundVelocityThreshold;

            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
            transform.position += Vector3.up * 0.1f;
            rb.linearDamping = 0f;
            float jumpForce = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);

            if (canPlaySound)
            {
                PlayJumpSound(); // 呼叫獨立的播放方法

                // ▼▼▼ [新增] 跳躍發出聲音 ▼▼▼
                StealthManager.MakeNoise(transform.position, jumpNoiseRange, 10f);
                // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

                if (showDebugGizmos)
                {
                    _lastNoiseTime = Time.time;
                    _lastNoiseRadius = jumpNoiseRange;
                    _lastNoisePos = transform.position;
                }
            }

            lastJumpTime = Time.fixedTime;
        }

        if (playerActions.Player.Jump.WasReleasedThisFrame() && rb.linearVelocity.y > 0)
        {
            // 直接把垂直速度砍半，造成「急墜」感，縮短滯空時間 = 縮短距離
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier, rb.linearVelocity.z);
        }
    }

    private void ApplyExtraGravity()
    {
        if (rb == null) return;
        if (!IsGrounded && rb.linearVelocity.y < 0) {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void OnDrawGizmos()
    {
        // --- 1. 噪音視覺化 (保留原本邏輯) ---
        if (showDebugGizmos && Application.isPlaying)
        {
            float timeSinceLastNoise = Time.time - _lastNoiseTime;
            if (timeSinceLastNoise < gizmoDuration)
            {
                float alpha = 1.0f - (timeSinceLastNoise / gizmoDuration);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * alpha);
                Gizmos.DrawWireSphere(_lastNoisePos, _lastNoiseRadius);
                Gizmos.DrawSphere(_lastNoisePos, 0.1f);
            }
        }

        // --- 2. GroundCheck 視覺化 (更新為 Hybrid 邏輯) ---
        // 即使沒選中物件，只要有 Collider 就畫出來，方便調試
        if (coll == null) coll = movementCollider != null ? movementCollider : GetComponent<Collider>();
        if (coll == null) return;

        // A. 重現 GroundCheck 的起點計算 (必須跟 GroundCheck 邏輯一致)
        Vector3 objectCenter = coll.bounds.center;
        float bottomY = coll.bounds.min.y;
        Vector3 castOrigin = objectCenter;

        if (coll is BoxCollider box)
        {
            castOrigin = transform.TransformPoint(box.center + Vector3.down * (box.size.y * 0.5f - groundCheckRadius + sinkAmount));
        }
        else
        {
            castOrigin = new Vector3(objectCenter.x, bottomY + groundCheckRadius - sinkAmount, objectCenter.z);
        }

        // B. 畫出檢測範圍 (黃色透明球) - 這是 Physics.OverlapSphere 的範圍
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(castOrigin, groundCheckRadius);

        // C. 模擬 GroundCheck 的內部邏輯來畫線
        // 注意：這裡為了視覺化，我們在 Editor 裡再跑一次檢測，可能會稍微吃一點點編輯器效能，但在 Game 視窗不影響
        LayerMask combinedMask = groundLayer | platformLayer;
        Collider[] hits = Physics.OverlapSphere(castOrigin, groundCheckRadius, combinedMask);

        foreach (var hitColl in hits)
        {
            if (hitColl.transform.root == transform.root) continue; // 忽略自己

            bool isComplexCollider = (hitColl is MeshCollider mesh && !mesh.convex) || (hitColl is TerrainCollider);

            Vector3 rayOrigin;
            Vector3 closestPoint;

            if (isComplexCollider)
            {
                // 備案：直接從中心往下
                rayOrigin = objectCenter;
            }
            else
            {
                // 正規：從最近點
                closestPoint = hitColl.ClosestPoint(objectCenter);
                rayOrigin = closestPoint + Vector3.up * 0.5f;
            }

            // --- 視覺化 Micro-Raycast ---
            Ray ray = new Ray(rayOrigin, Vector3.down);

            // 模擬射線檢測
            if (hitColl.Raycast(ray, out RaycastHit hitInfo, 1.0f))
            {
                // 檢查角度
                float angle = Vector3.Angle(hitInfo.normal, Vector3.up);

                if (angle < 50f)
                {
                    // [綠色] 通過：這是合法的地面
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(rayOrigin, hitInfo.point); // 畫出射線
                    Gizmos.DrawRay(hitInfo.point, hitInfo.normal * 0.3f); // 畫出法線 (刺刺的那個)
                }
                else
                {
                    // [紅色] 失敗：坡度太陡 (牆壁)
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(rayOrigin, hitInfo.point);
                    Gizmos.DrawRay(hitInfo.point, hitInfo.normal * 0.3f);
                }
            }
            else
            {
                if (isComplexCollider) continue;

                closestPoint = hitColl.ClosestPoint(objectCenter);

                // --- 視覺化 Fallback (備案) ---
                // 如果射線失敗，檢查備案條件
                bool isBelowCenter = closestPoint.y < objectCenter.y;
                float heightDiff = Mathf.Abs(closestPoint.y - bottomY);
                bool isAtFeetLevel = heightDiff < 0.15f;

                if (isBelowCenter && isAtFeetLevel)
                {
                    // [青色] 備案通過：雖然射線沒打到(可能穿模)，但高度正確
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(closestPoint, 0.05f);
                    Gizmos.DrawLine(closestPoint, closestPoint + Vector3.up * 0.1f);
                }
                else
                {
                    // [洋紅色] 完全失敗：射線沒打到，高度也不對
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(closestPoint, 0.02f);
                }
            }
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

    private IEnumerator HeavyPushCoroutine(Vector3 pushDirection)
    {
        isPushing = true; // 鎖定

        // 1. 觸發「發力」動畫
        animator.SetTrigger("Do Push"); // (你需要一個叫 "Do Push" 的 Trigger)

        // 2. 施加物理力 (等待物理幀)
        yield return new WaitForFixedUpdate();
        rb.AddForce(pushDirection * currentHeavyPushForce, ForceMode.Impulse); // <-- 使用 current

        // 3. 等待動畫/間隔結束
        yield return new WaitForSeconds(currentPushInterval); // <-- 使用 current

        isPushing = false; // 解鎖
    }
}