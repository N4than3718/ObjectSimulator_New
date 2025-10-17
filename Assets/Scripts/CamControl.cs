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
    public float rotateLerp = 15f;

    [Header("視角限制 (俯仰角)")]
    public float pitchMin = -85f;
    public float pitchMax = 85f;

    // --- 私有變數 ---
    private InputSystem_Actions playerActions;
    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 lookInput; // 只用來儲存輸入值

    // 這個公開屬性依然保留，給 MovementAnimator 使用
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

    // ▼▼▼ Update 現在只負責讀取輸入，不進行任何計算 ▼▼▼
    void Update()
    {
        lookInput = playerActions.Player.Look.ReadValue<Vector2>();
        RotationInput = lookInput; // 更新給 MovementAnimator 用的值
    }

    private void OnUnlockCursor(InputAction.CallbackContext context)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ▼▼▼ 所有的計算和移動都發生在 FixedUpdate ▼▼▼
    void FixedUpdate()
    {
        // 1. 在物理幀中，計算目標旋轉角度
        yaw += lookInput.x * rotateSpeed;
        pitch -= lookInput.y * rotateSpeed;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 2. 如果沒有目標，就什麼都不做
        if (FollowTarget == null) return;

        // 3. 計算並平滑地更新旋轉
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotateLerp);

        // 4. 計算並更新位置
        Vector3 targetPos = FollowTarget.position;
        targetPos.y += offsetY;
        transform.position = targetPos - (transform.rotation * new Vector3(0, 0, offsetZ));
    }
}