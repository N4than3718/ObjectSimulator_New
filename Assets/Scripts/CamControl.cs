using UnityEngine;
using UnityEngine.InputSystem;

public class CamControl : MonoBehaviour
{
    [Header("���H�ؼ�")]
    public Transform FollowTarget;

    [Header("�����P�Z��")]
    public float offsetZ = 4f;
    public float offsetY = 0.5f;

    [Header("����]�w")]
    public float rotateSpeed = 0.1f;
    public float rotateLerp = 15f;

    [Header("�������� (������)")]
    public float pitchMin = -85f;
    public float pitchMax = 85f;

    // --- �p���ܼ� ---
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
        // ������ �q�\�s�� UnlockCursor �ƥ� ������
        playerActions.Player.UnlockCursor.performed += OnUnlockCursor;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // ������ �����q�\�ƥ�A�o�O�n�ߺD ������
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

        // �ª� GetKeyDown �w�g�Q����
    }

    // ������ �o�O�s���ƥ�B�z�禡 ������
    private void OnUnlockCursor(InputAction.CallbackContext context)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    // ����������������������������

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