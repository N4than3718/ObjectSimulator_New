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
    public float rotateLerp = 15f; // �i�H�y�L�վ�o�ӭȨӧ��ܥ��ƫ�

    [Header("�������� (������)")]
    public float pitchMin = -85f;
    public float pitchMax = 85f;

    // --- �p���ܼ� ---
    private InputSystem_Actions playerActions;
    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 lookInput;

    // // �o�Ӥ��}�ݩʦp�G�T�w MovementAnimator �S�Ψ�A�i�H�Ҽ{����
    // public Vector2 RotationInput { get; private set; }

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

    // ������ Update �̵M�u�t�dŪ����J ������
    void Update()
    {
        lookInput = playerActions.Player.Look.ReadValue<Vector2>();
        // RotationInput = lookInput; // �p�G RotationInput �S�Ψ�A�i�H���ѱ��ΧR��
    }

    private void OnUnlockCursor(InputAction.CallbackContext context)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ������ ���� FixedUpdate() ������
    // void FixedUpdate()
    // {
    //     // ... �Ҧ��޿貾�� LateUpdate ...
    // }

    // ������ �s�W�GLateUpdate()�A�Ω�B�z��v�����ʩM���� ������
    void LateUpdate()
    {
        // 1. �p��ؼб��ਤ�� (�ϥ� Update ��Ū���� lookInput)
        //    �`�N�G�o�̥� Time.deltaTime �O�]�� LateUpdate ���H�V�v
        yaw += lookInput.x * rotateSpeed;
        pitch -= lookInput.y * rotateSpeed;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 2. �p�G�S���ؼСA�N���򳣤���
        if (FollowTarget == null) return;

        // 3. �p��å��Ʀa��s����
        //    �ϥ� Time.deltaTime �t�X LateUpdate
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateLerp);

        // 4. �p��ç�s��m (�b�ؼв��ʧ�����A�B��v�������s��)
        Vector3 targetPos = FollowTarget.position;
        targetPos.y += offsetY;
        transform.position = targetPos - (transform.rotation * new Vector3(0, 0, offsetZ));
    }
}