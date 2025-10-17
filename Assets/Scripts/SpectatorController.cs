using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;

    [Header("References")]
    [SerializeField] private TeamManager teamManager;

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        spectatorCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        inputActions.Spectator.Enable();
        inputActions.Spectator.Select.performed += OnSelectPerformed;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Spectator Mode Enabled.");
    }

    private void OnDisable()
    {
        inputActions.Spectator.Disable();
        inputActions.Spectator.Select.performed -= OnSelectPerformed;
    }

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("SpectatorController needs a reference to the TeamManager!");
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 lookInput = inputActions.Spectator.Look.ReadValue<Vector2>();
        yaw += lookInput.x * lookSensitivity;
        pitch -= lookInput.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
    }

    // ▼▼▼ 核心修改在這裡 ▼▼▼
    private void HandleMovement()
    {
        // 1. 讀取水平移動輸入
        Vector2 moveInput = inputActions.Spectator.Move.ReadValue<Vector2>();
        Vector3 horizontalMove = (transform.forward * moveInput.y + transform.right * moveInput.x);

        // 2. 讀取垂直移動輸入
        float ascendInput = inputActions.Spectator.Ascend.ReadValue<float>();
        float descendInput = inputActions.Spectator.Descend.ReadValue<float>();
        Vector3 verticalMove = Vector3.up * (ascendInput - descendInput);

        // 3. 合併移動向量並應用
        Vector3 finalMove = (horizontalMove + verticalMove).normalized;
        transform.position += finalMove * moveSpeed * Time.deltaTime;
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        Ray ray = spectatorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            if (hit.collider.CompareTag("Controllable"))
            {
                teamManager.PossessCharacter(hit.transform.root.gameObject);
            }
        }
    }
}