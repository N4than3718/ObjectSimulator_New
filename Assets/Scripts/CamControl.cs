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

    public Vector2 RotationInput { get; private set; }

    void Awake()
    {
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // ▼▼▼ 訂閱新的 UnlockCursor 事件 ▼▼▼
        playerActions.Player.UnlockCursor.performed += OnUnlockCursor;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // ▼▼▼ 取消訂閱事件，這是好習慣 ▼▼▼
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

    void Update()
    {
        RotationInput = playerActions.Player.Look.ReadValue<Vector2>();

        yaw += RotationInput.x * rotateSpeed;
        pitch -= RotationInput.y * rotateSpeed;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 舊的 GetKeyDown 已經被移除
    }

    // ▼▼▼ 這是新的事件處理函式 ▼▼▼
    private void OnUnlockCursor(InputAction.CallbackContext context)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void FixedUpdate()
    {
        if (FollowTarget == null) return;

        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotateLerp);

        Vector3 targetPos = FollowTarget.position;
        targetPos.y += offsetY;
        transform.position = targetPos - (transform.rotation * new Vector3(0, 0, offsetZ));
    }
}