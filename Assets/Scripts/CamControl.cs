using UnityEngine;
using UnityEngine.InputSystem;

public class CamControl : MonoBehaviour
{
    [Header("跟隨目標")]
    public Transform FollowTarget;

    [Header("基礎設定")]
    public float offsetZ = 2f;
    public float defaultOffsetY = 0.8f; // 原本的 offsetY 改名為 default，這是理想高度
    public float minOffsetY = 0.1f;     // 最低能降到多低 (避免貼地太近穿模)

    [Header("自動避障 (Auto-Crouch Cam)")]
    public LayerMask obstacleLayer;     // 設定成 Default, Ground, Furniture 等層級
    public float checkRadius = 0.05f;    // 探測球的大小 (比攝影機稍大一點)
    public float heightSmoothTime = 0.1f; // 高度變化的平滑時間

    [Header("旋轉設定")]
    public float rotateSpeed = 0.1f;
    public float rotateLerp = 15f; // 可以稍微調整這個值來改變平滑度
    public float positionSmoothTime = 0.05f;

    [Header("視角限制 (俯仰角)")]
    public float pitchMin = -10f;
    public float pitchMax = 85f;

    // --- 私有變數 ---
    private InputSystem_Actions playerActions;
    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 lookInput;
    private Vector3 _currentVelocity = Vector3.zero;
    public bool IsInputPaused { get; set; } = false;
    public Vector2 RotationInput { get; private set; }

    private float _currentHeight;
    private float _heightVelocity; // 高度變化的速度紀錄

    void Awake()
    {
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        playerActions.Player.UnlockCursor.performed += OnUnlockCursor;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.UnlockCursor.performed -= OnUnlockCursor;
    }

    void Start()
    {
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;
        _currentHeight = defaultOffsetY; // 初始高度

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ▼▼▼ Update 依然只負責讀取輸入 ▼▼▼
    void Update()
    {
        if (IsInputPaused || FollowTarget == null) // Also check FollowTarget just in case
        {
            return; // Don't process rotation, positioning, etc.
        }

        lookInput = playerActions.Player.Look.ReadValue<Vector2>();
        RotationInput = lookInput; // 如果 RotationInput 沒用到，可以註解掉或刪除
    }

    private void OnUnlockCursor(InputAction.CallbackContext context)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ▼▼▼ 新增：LateUpdate()，用於處理攝影機移動和旋轉 ▼▼▼
    void LateUpdate()
    {
        if (IsInputPaused || FollowTarget == null) // Also check FollowTarget just in case
        {
            return; // Don't process rotation, positioning, etc.
        }

        // 1. 計算目標旋轉角度 (使用 Update 中讀取的 lookInput)
        //    注意：這裡用 Time.deltaTime 是因為 LateUpdate 跟隨幀率
        yaw += lookInput.x * rotateSpeed;
        pitch -= lookInput.y * rotateSpeed;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 2. 如果沒有目標，就什麼都不做
        if (FollowTarget == null) return;

        // 3. 計算並平滑地更新旋轉
        //    使用 Time.deltaTime 配合 LateUpdate
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateLerp);

        // 4. 動態高度檢測 (Ceiling Check)

        float targetHeight = defaultOffsetY;

        // 從目標頭頂微微往上發射 SphereCast
        Vector3 castOrigin = FollowTarget.position + Vector3.up * 0.2f; // 起點稍微抬高，避免卡在地板
        float castDistance = defaultOffsetY - 0.1f; // 檢測距離

        RaycastHit hit;
        // 往上掃描，看看有沒有東西擋在 "理想高度" 之間
        if (Physics.SphereCast(castOrigin, checkRadius, Vector3.up, out hit, castDistance, obstacleLayer))
        {
            // 如果撞到了 (例如撞到沙發底)，就把目標高度設為 "碰撞點高度"
            // 減去 checkRadius 是為了留一點緩衝空間，不要讓攝影機中心貼死天花板
            targetHeight = hit.distance;
        }

        // 限制高度不低於最小值 (避免鑽太低)
        targetHeight = Mathf.Max(targetHeight, minOffsetY);

        // 使用 SmoothDamp 讓高度變化平滑 (避免因為掃到一根橫樑就劇烈抖動)
        _currentHeight = Mathf.SmoothDamp(_currentHeight, targetHeight, ref _heightVelocity, heightSmoothTime);

        // 5. 計算並更新位置 (在目標移動完成後，且攝影機旋轉更新後)
        Vector3 targetPos = FollowTarget.position + Vector3.up * _currentHeight;
        Vector3 desiredPosition = targetPos - (targetRotation * Vector3.forward * offsetZ);

        float snapSpeed = 60f;

        // 如果距離太遠 (例如傳送)，就直接瞬移，避免拖影
        if (Vector3.Distance(transform.position, desiredPosition) > 1f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * snapSpeed);
        }
    }

    void OnDrawGizmos()
    {
        if (FollowTarget == null) return;

        // 重現原本的邏輯參數
        Vector3 castOrigin = FollowTarget.position + Vector3.up * 0.05f;
        float castDistance = defaultOffsetY - 0.1f;

        // 1. 畫出原本預計要偵測的路徑 (黃色線)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(castOrigin, Vector3.up * castDistance);
        Gizmos.DrawWireSphere(castOrigin, checkRadius); // 起點球

        // 2. 實際做一次檢測，看看撞到什麼
        RaycastHit hit;
        bool isHit = Physics.SphereCast(castOrigin, checkRadius, Vector3.up, out hit, castDistance, obstacleLayer);

        if (isHit)
        {
            // 如果撞到了，畫紅色！
            Gizmos.color = Color.red;
            Gizmos.DrawLine(castOrigin, hit.point);
            Gizmos.DrawWireSphere(hit.point, checkRadius); // 撞擊點的球

            // 在 Scene 視窗顯示撞到的名字，讓你抓兇手
            // (需要 UnityEditor 命名空間，或者只看顏色就好)
#if UNITY_EDITOR
            UnityEditor.Handles.Label(hit.point, $"Hit: {hit.collider.name}");
#endif
        }
        else
        {
            // 沒撞到，畫綠色虛線表示通過
            Gizmos.color = Color.green;
            Gizmos.DrawLine(castOrigin, castOrigin + Vector3.up * castDistance);
        }
    }
}