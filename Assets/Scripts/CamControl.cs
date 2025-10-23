using UnityEngine;
using UnityEngine.InputSystem;

public class CamControl : MonoBehaviour
{
    [Header("跟隨目標")]
    public Transform FollowTarget;

    [Header("偏移與距離")]
    public float offsetZ = 4f;
    public float offsetY = 0.5f;

    [Header("旋轉設定")]
    public float rotateSpeed = 0.1f;
    public float rotateLerp = 15f; // 可以稍微調整這個值來改變平滑度

    [Header("視角限制 (俯仰角)")]
    public float pitchMin = -85f;
    public float pitchMax = 85f;

    // --- 私有變數 ---
    private InputSystem_Actions playerActions;
    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 lookInput;
    public bool IsInputPaused { get; set; } = false;
    public Vector2 RotationInput { get; private set; }

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ▼▼▼ Update 依然只負責讀取輸入 ▼▼▼
    void Update()
    {
        if (IsInputPaused || FollowTarget == null) // Also check FollowTarget just in case
        {
            Debug.Log($"{this.GetType().Name} Update: IsInputPaused = {IsInputPaused}");
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
            Debug.Log($"{this.GetType().Name} Update: IsInputPaused = {IsInputPaused}");
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

        // 4. 計算並更新位置 (在目標移動完成後，且攝影機旋轉更新後)
        Vector3 targetPos = FollowTarget.position;
        targetPos.y += offsetY;
        transform.position = targetPos - (transform.rotation * new Vector3(0, 0, offsetZ));
    }
}