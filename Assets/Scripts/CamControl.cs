using UnityEngine;
using UnityEngine.InputSystem;

public class CamControl : MonoBehaviour
{
    public static CamControl Current { get; private set; }

    [Header("跟隨目標")]
    public Transform FollowTarget;

    [Header("基礎設定")]
    public float offsetZ = 2f;
    public float defaultOffsetY = 0.8f; // 原本的 offsetY 改名為 default，這是理想高度
    public float minOffsetY = 0.1f;     // 最低能降到多低 (避免貼地太近穿模)

    [Header("防穿牆與避障")]
    public LayerMask obstacleLayer;      // 務必設定為 Default, Ground, Wall 等層級
    public float checkRadius = 0.2f;     // 探測半徑 (建議設大一點，例如 0.2，避免攝影機穿幫)
    public float wallClipPadding = 0.1f; // 撞到牆時，稍微把攝影機再往前推一點點，避免看到牆壁內部

    [Header("平滑設定")]
    public float heightSmoothTime = 0.1f;
    public float rotateSpeed = 0.1f;
    public float rotateLerp = 15f;
    public float moveSmoothSpeed = 60f; // 用於位置跟隨的平滑度

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
        Current = this;

        playerActions.Player.Enable();
        playerActions.Player.UnlockCursor.performed += OnUnlockCursor;
    }

    private void OnDisable()
    {
        if(Current = this)
        {
            Current = null;
        }

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
        if (IsInputPaused || FollowTarget == null) return;
        if (GameDirector.Instance != null && GameDirector.Instance.CurrentState != GameDirector.GameState.Playing) return;

        // 1. 計算旋轉
        yaw += lookInput.x * rotateSpeed;
        pitch -= lookInput.y * rotateSpeed;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 2. 應用旋轉 (Slerp 平滑)
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateLerp);

        // 3. 高度檢測 (原本的 Ceiling Check，防撞天花板)
        float targetHeight = defaultOffsetY;
        Vector3 castOrigin = FollowTarget.position + Vector3.up * 0.2f;

        // 向上偵測
        if (Physics.SphereCast(castOrigin, checkRadius, Vector3.up, out RaycastHit ceilingHit, defaultOffsetY - 0.1f, obstacleLayer))
        {
            targetHeight = ceilingHit.distance;
        }
        targetHeight = Mathf.Max(targetHeight, minOffsetY);
        _currentHeight = Mathf.SmoothDamp(_currentHeight, targetHeight, ref _heightVelocity, heightSmoothTime);

        // 4. 計算「理想」的攝影機中心點 (Pivot)
        Vector3 pivotPos = FollowTarget.position + Vector3.up * _currentHeight;

        // 5. 計算攝影機的「後退方向」
        Vector3 cameraDir = targetRotation * -Vector3.forward;

        // 6. 從 Pivot 往後發射射線，看看會不會撞到牆
        float finalDistance = offsetZ; // 預設距離

        // 使用 SphereCast 更有體積感，避免看穿牆縫
        if (Physics.SphereCast(pivotPos, checkRadius, cameraDir, out RaycastHit wallHit, offsetZ, obstacleLayer))
        {
            // 如果撞到了牆，距離 = 撞擊點距離 - 緩衝區
            // Mathf.Max 確保攝影機不會跑到玩家身體裡面 (保持最小 0.2 距離)
            finalDistance = Mathf.Max(wallHit.distance - wallClipPadding, 0.2f);
        }

        // 7. 計算最終位置
        Vector3 desiredPosition = pivotPos + (cameraDir * finalDistance);


        // 8. 移動攝影機
        // 如果偵測到牆壁導致需要劇烈拉近，直接瞬移比較好，不然會穿幫
        // 如果是正常跟隨，則用 Lerp 平滑
        float distToDesired = Vector3.Distance(transform.position, desiredPosition);

        // 如果距離變化太大 (例如傳送) 或 正在撞牆 (需要快速反應)，加快跟隨速度
        if (distToDesired > 1f || finalDistance < offsetZ - 0.1f)
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 100f); // 幾乎瞬移
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * moveSmoothSpeed);
        }
    }

    void OnDrawGizmos()
    {
        if (FollowTarget == null) return;

        // 畫出高度偵測 (綠色)
        Gizmos.color = Color.green;
        Vector3 pivotPos = FollowTarget.position + Vector3.up * (_currentHeight > 0 ? _currentHeight : defaultOffsetY);
        Gizmos.DrawWireSphere(pivotPos, 0.1f);

        // 畫出防穿牆偵測 (紅色/黃色)
        Vector3 cameraDir = transform.rotation * -Vector3.forward;

        // 模擬當前的偵測
        if (Physics.SphereCast(pivotPos, checkRadius, cameraDir, out RaycastHit hit, offsetZ, obstacleLayer))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pivotPos, hit.point);
            Gizmos.DrawWireSphere(hit.point, checkRadius);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pivotPos, pivotPos + cameraDir * offsetZ);
            Gizmos.DrawWireSphere(pivotPos + cameraDir * offsetZ, checkRadius);
        }
    }
}