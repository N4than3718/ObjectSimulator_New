using System.Collections;
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

    [Header("Animation Settings")]
    [Tooltip("動畫在 1x 速度播放時，對應的玩家移動速度 (m/s)")]
    [SerializeField] private float animationBaseSpeed = 5.0f;

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
    [Tooltip("檢測距離 (越不規則的物件可能需要長一點的緩衝)")]
    [SerializeField] private float groundCheckLeeway = 0.1f;
    [Tooltip("手動修正起點高度")]
    [SerializeField] private float sinkAmount = 0.15f; // [新增] 手動修正起點高度
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask platformLayer;

    [Header("Interaction & Highlighting")]
    [Tooltip("這是所有互動與準星高亮的標準距離")]
    public float interactionDistance = 1.0f;

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
        Current = this;

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
        if (Current == this)
        {
            Current = null;
        }

        if (playerActions != null)
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
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
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
        GroundCheck();

        if (currentCardboard != null)
        {
            // 把自己的 Rigidbody 和狀態傳過去
            currentCardboard.UpdateAnimationState(rb, isOverEncumbered, isPushing);
        }

        if (!isOverEncumbered && moveInput.magnitude > 0.1f)
        {
            rb.linearDamping = moveDrag;
            HandleMovement();
        }
        else if (!isOverEncumbered && IsGrounded) // [新增] 如果沒超重，也沒按鍵，就停下
        {
            sleepTimer += Time.fixedDeltaTime;

            // 檢查水平速度是否已經很慢了
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

            if (horizontalVel.sqrMagnitude < 0.05f && sleepTimer > 0.5f) // 閾值可以微調，例如 0.05f
            {
                // 【關鍵】如果夠慢，直接開啟 Kinematic，物理引擎完全停止運算此物件
                if (!rb.isKinematic)
                {
                    rb.isKinematic = true;
                    // 關閉插值，避免鎖死瞬間的視覺拉扯 (可選，視情況)
                    // rb.interpolation = RigidbodyInterpolation.None; 

                    // 強制歸零速度，以防切換回物理時亂噴
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                // 還沒夠慢，先用高阻尼減速 (你原本的邏輯)
                rb.linearDamping = stopDrag;
                // 或者保留你剛剛加的主動煞車代碼
            }
        }
        else if (!isOverEncumbered)
        {
            // 如果 GroundCheck 稍微閃了一下 (判定成空中)，
            // 我們不能讓阻力維持在 0，否則會無限滑行。
            // 給它一個介於中間的阻力 (例如 2.0f)，讓它在"微跳"時也能減速。
            sleepTimer = 0f; // 空中不鎖死
            rb.isKinematic = false;
            rb.linearDamping = 0.5f;
        }

        CurrentHorizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;

        HandleJump();
        ApplyExtraGravity();
        HandleMovementNoise();
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

        // 2. 根據是否在地面，決定移動邏輯
        if (IsGrounded)
        {
            // --- 地面：保持原本的「直接速度控制」，反應靈敏 ---
            Vector3 targetVelocity = moveDirection * CurrentSpeed;

            Vector3 currentVelocity = rb.linearVelocity;
            Vector3 velocityChange = targetVelocity - currentVelocity;

            velocityChange.y = 0;

            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
        else
        {
            // --- 空中：改為「施加力」或「限制性速度控制」，保留慣性 ---
            // 方案 A (簡單版)：只允許玩家在空中「微調」方向，但不能急停
            if (moveInput.magnitude > 0.1f)
            {
                // 1. 計算目標速度
                Vector3 targetVelocity = moveDirection * CurrentSpeed;

                // 2. 取得當前水平速度
                Vector3 currentVelocity = rb.linearVelocity;
                Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0, currentVelocity.z);

                // 3. 【關鍵還原】計算 Lerp 之後的「預期速度」
                // 這裡保留你原本的參數 (Time.fixedDeltaTime * airControl * 5f)
                Vector3 intendedVelocity = Vector3.Lerp(currentHorizontal, targetVelocity, Time.fixedDeltaTime * airControl * 5f);

                // 4. 計算「速度差 (Delta)」： 預期速度 - 當前速度
                Vector3 velocityChange = intendedVelocity - currentHorizontal;

                // 5. 將這個差值轉化為力，施加給剛體
                // ForceMode.VelocityChange 會無視質量，直接改變速度，效果等同於你原本的寫法，但更安全
                rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }
            // 注意：這裡沒有 else { velocity = 0 }，所以鬆開按鍵後，角色會繼續依照慣性飛行！
        }

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

            // B. 【關鍵修改】計算碰撞點與角度
            // 找出這個牆壁/地板上，離我最近的那個點
            Vector3 closestPoint = hitColl.ClosestPoint(objectCenter);

            // 計算方向向量：從「碰撞點」指向「我的中心」
            // 想像一根箭頭從地板射向你的肚子
            Vector3 rayOrigin = closestPoint + Vector3.up * 0.5f;

            // C. 判斷角度 (Normal Check)
            // directionToCenter.y > 0.7f 代表這個面大致朝上 (約 45 度以內的坡度)
            // 如果是牆壁，這個值會接近 0；如果是天花板，這個值會是負的
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitInfo, 1.0f, combinedMask))
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
        HighlightableObject hitHighlightable = null;
        float hitDistance = interactionDistance;

        // ▼▼▼ [核心修改] 改用 RaycastAll ▼▼▼
        // 取得射線路徑上所有的碰撞 (不只是一個)
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance);

        // 我們需要找到 "最近的" 且 "不是自己" 的那個
        float closestDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            // 1. 排除自己 (檢查根物件是否相同)
            if (hit.collider.transform.IsChildOf(transform)) continue;

            // 2. 排除 Trigger (視需求，通常 Highlight 不選 Trigger)
            if (hit.collider.isTrigger) continue;

            // 3. 檢查是否有 HighlightableObject 元件
            // 注意：這裡優化一下，先看距離，如果比當前最近的還遠，就不用 GetComponent 了，省效能
            if (hit.distance < closestDistance)
            {
                // 🔥 關鍵修改：只要有 HighlightableObject 就視為目標，不管 Tag 是不是 Player
                var highlightable = hit.collider.GetComponentInParent<HighlightableObject>();
                if (highlightable != null)
                {
                    hitHighlightable = highlightable;
                    closestDistance = hit.distance;
                }
            }
        }

        // 處理高亮狀態切換
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
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, closestDistance);
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

            // 找出最近點
            Vector3 closestPoint = hitColl.ClosestPoint(objectCenter);

            // --- 視覺化 Micro-Raycast ---
            Vector3 rayOrigin = closestPoint + Vector3.up * 0.5f;
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