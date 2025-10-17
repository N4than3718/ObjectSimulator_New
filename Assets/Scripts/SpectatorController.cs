using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;

    [Header("References")]
    [SerializeField] private TeamManager teamManager;

    [Header("Highlighting")]
    [Tooltip("���G���誺�ҪO")]
    [SerializeField] private Material highlightMaterial;

    // ������ �s�W���ʺA�����Ѽ� ������
    [Header("Dynamic Outline")]
    [Tooltip("�������̤p�e��")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [Tooltip("�������̤j�e��")]
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [Tooltip("�F��̤j�e�שһݪ��Z��")]
    [SerializeField] private float maxDistanceForOutline = 50f;
    // ����������������������������������

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    // --- �޲z���G���A���ܼ� ---
    private Renderer currentlyHighlighted;
    private Material[] originalMaterials;
    private Material highlightInstance; // �ڭ̰ʺA�Ыت�������

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
        HandleHighlight();
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

    private void HandleHighlight()
    {
        Ray ray = spectatorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // �����ƹ����V������
        if (Physics.Raycast(ray, out RaycastHit hit, 200f) && hit.collider.CompareTag("Controllable"))
        {
            var renderer = hit.transform.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // �p�G���V�F�@�ӷs������A�N�������G
                if (currentlyHighlighted != renderer)
                {
                    RestoreOriginalMaterials();
                    currentlyHighlighted = renderer;
                    StoreAndApplyHighlight();
                }

                // --- �֤߭ק�G�C�@�V����s�����e�� ---
                if (highlightInstance != null)
                {
                    float distance = Vector3.Distance(transform.position, currentlyHighlighted.transform.position);
                    // InverseLerp �|�N�Z���M�g�� 0-1 ���d��
                    float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                    // Lerp �ھ� 0-1 ���d��A�p��X�b min �M max �����������e��
                    float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);

                    // �ϥ� SetFloat ��s Shader ���� _OutlineWidth �ݩ�
                    highlightInstance.SetFloat("_OutlineWidth", newWidth);
                }
                return;
            }
        }

        // �p�G�S�������i�ޱ�����A�N�M�����G
        RestoreOriginalMaterials();
    }

    private void StoreAndApplyHighlight()
    {
        if (currentlyHighlighted == null || highlightMaterial == null) return;

        // �x�s��l����
        originalMaterials = currentlyHighlighted.materials;

        // �Ыذ��G���誺���
        highlightInstance = new Material(highlightMaterial);

        // �M�ηs����C��]��l���� + ���G��ҡ^
        var newMaterials = originalMaterials.ToList();
        newMaterials.Add(highlightInstance);
        currentlyHighlighted.materials = newMaterials.ToArray();
    }

    private void RestoreOriginalMaterials()
    {
        if (currentlyHighlighted != null && originalMaterials != null)
        {
            currentlyHighlighted.materials = originalMaterials;
        }

        // �M�z���A
        currentlyHighlighted = null;
        originalMaterials = null;

        // �p�G�s�b�����ҡA�N�P�����A�קK�O���鬪�|
        if (highlightInstance != null)
        {
            Destroy(highlightInstance);
            highlightInstance = null;
        }
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (currentlyHighlighted != null)
        {
            teamManager.PossessCharacter(currentlyHighlighted.transform.root.gameObject);
        }
    }
}