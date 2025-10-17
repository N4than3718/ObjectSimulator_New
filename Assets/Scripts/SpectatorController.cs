using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // �ݭn�o�ӨӨϥ� List
using System.Linq; // �ݭn�o�ӨӨϥ� Linq

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;

    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Header("Highlighting")]
    [Tooltip("�N�A���n�����Ⱚ�G Material ���o��")]
    [SerializeField] private Material highlightMaterial;

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    // --- �s�W���ܼơA�ΨӺ޲z���G���A ---
    private Renderer currentlyHighlighted;
    private Material[] originalMaterials;

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
    }

    private void OnDisable()
    {
        inputActions.Spectator.Disable();
        inputActions.Spectator.Select.performed -= OnSelectPerformed;
        // �T�O���}�Ҧ��ɡA�M���Ҧ����G
        RestoreOriginalMaterials();
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
        HandleHighlight(); // �b�C�@�V���B�z���G�޿�
    }

    private void HandleLook()
    {
        Vector2 lookInput = inputActions.Spectator.Look.ReadValue<Vector2>();
        yaw += lookInput.x * lookSensitivity;
        pitch -= lookInput.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
    }

    private void HandleMovement()
    {
        Vector2 moveInput = inputActions.Spectator.Move.ReadValue<Vector2>();
        float ascendInput = inputActions.Spectator.Ascend.ReadValue<float>();
        float descendInput = inputActions.Spectator.Descend.ReadValue<float>();

        Vector3 moveDirection = (transform.forward * moveInput.y + transform.right * moveInput.x + Vector3.up * (ascendInput - descendInput)).normalized;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    // ������ ���s�����G�B�z�޿� ������
    private void HandleHighlight()
    {
        Ray ray = spectatorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // �ˬd�O�_�g���F�a�� "Controllable" Tag ������
            if (hit.collider.CompareTag("Controllable"))
            {
                // �������W���Ĥ@�� Renderer ����
                var renderer = hit.transform.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    // �p�G�o�O�@�ӷs������A�N�������G
                    if (currentlyHighlighted != renderer)
                    {
                        RestoreOriginalMaterials(); // �������ª����G
                        currentlyHighlighted = renderer;
                        StoreAndApplyHighlight(); // �A�M�ηs�����G
                    }
                    return; // �B�z�����A������^
                }
            }
        }

        // �p�G�g�u�S�������F��A�Υ��쪺���O�i�ޱ�����A�N�M�����G
        RestoreOriginalMaterials();
        currentlyHighlighted = null;
    }

    private void StoreAndApplyHighlight()
    {
        if (currentlyHighlighted == null || highlightMaterial == null) return;

        // �x�s��l������C��
        originalMaterials = currentlyHighlighted.materials;

        // �Ыؤ@�ӷs������C��A�]�t�Ҧ���l����A�A�[�W�ڭ̪����G����
        var newMaterials = originalMaterials.ToList();
        newMaterials.Add(highlightMaterial);
        currentlyHighlighted.materials = newMaterials.ToArray();
    }

    private void RestoreOriginalMaterials()
    {
        if (currentlyHighlighted != null && originalMaterials != null)
        {
            // �٭��l������C��
            currentlyHighlighted.materials = originalMaterials;
        }
        // �M�Ū��A
        currentlyHighlighted = null;
        originalMaterials = null;
    }
    // ��������������������������������

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // �u�����G�F�@�Ӫ���ɡA�I���~����
        if (currentlyHighlighted != null)
        {
            teamManager.PossessCharacter(currentlyHighlighted.transform.root.gameObject);
        }
    }
}