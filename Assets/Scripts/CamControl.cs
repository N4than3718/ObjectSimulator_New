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
    private Vector2 lookInput; // �u�Ψ��x�s��J��

    // �o�Ӥ��}�ݩʨ̵M�O�d�A�� MovementAnimator �ϥ�
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

    // ������ Update �{�b�u�t�dŪ����J�A���i�����p�� ������
    void Update()
    {
        lookInput = playerActions.Player.Look.ReadValue<Vector2>();
        RotationInput = lookInput; // ��s�� MovementAnimator �Ϊ���
    }

    private void OnUnlockCursor(InputAction.CallbackContext context)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ������ �Ҧ����p��M���ʳ��o�ͦb FixedUpdate ������
    void FixedUpdate()
    {
        // 1. �b���z�V���A�p��ؼб��ਤ��
        yaw += lookInput.x * rotateSpeed;
        pitch -= lookInput.y * rotateSpeed;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 2. �p�G�S���ؼСA�N���򳣤���
        if (FollowTarget == null) return;

        // 3. �p��å��Ʀa��s����
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotateLerp);

        // 4. �p��ç�s��m
        Vector3 targetPos = FollowTarget.position;
        targetPos.y += offsetY;
        transform.position = targetPos - (transform.rotation * new Vector3(0, 0, offsetZ));
    }
}