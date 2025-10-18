using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // ��_ List
using System.Linq; // ��_ Linq

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float interactionDistance = 200f; // ��_�A���G�ݭn

    [Header("References")]
    [SerializeField] private TeamManager teamManager;

    // --- ��_���G�����Ѽ� ---
    [Header("Highlighting")]
    [Tooltip("���G���誺�ҪO")]
    [SerializeField] private Material highlightMaterial; // �ݭn�ҪO
    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;
    // --- ��_���� ---


    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    // --- ��_���G���A�ܼ� ---
    private HighlightableObject currentlyTargetedObject; // ��W�A��M��
    // --- ��_���� ---

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        spectatorCamera = GetComponent<Camera>();
        Debug.Log("[Spectator] Awake called.");
    }

    private void OnEnable()
    {
        inputActions.Spectator.Enable();
        // ������ �ץ��G�T�{�q�\�F���T����k ������
        inputActions.Spectator.Select.performed += OnSelectPerformed;
        // ����������������������������������������
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[Spectator] Enabled, Action Map Active.");
    }

    private void OnDisable()
    {
        inputActions.Spectator.Disable();
        // ������ �ץ��G�T�{�����q�\�F���T����k ������
        inputActions.Spectator.Select.performed -= OnSelectPerformed;
        // ����������������������������������������
        if (currentlyTargetedObject != null)
        {
            currentlyTargetedObject.SetTargetedHighlight(false);
            currentlyTargetedObject = null;
        }
        // Cursor state might be handled elsewhere when switching
        Debug.Log("[Spectator] Disabled.");
    }

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("SpectatorController needs a reference to the TeamManager!");
        Debug.Log("[Spectator] Start called.");
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        // ������ �ץ��G���T�I�s HandleHighlight ��k ������
        HandleHighlight();
        // ������������������������������������
        if (Time.timeScale <= 0f) Debug.LogError("[Spectator] Time.timeScale is 0! Game paused?");
    }

    private void HandleLook()
    {
        Vector2 lookInput = inputActions.Spectator.Look.ReadValue<Vector2>();
        //if (lookInput.sqrMagnitude > 0.01f) Debug.Log($"[Spectator] Look Input: {lookInput}");

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

        //if (moveInput.sqrMagnitude > 0.01f || ascendInput > 0.1f || descendInput > 0.1f)
        //     Debug.Log($"[Spectator] Move Input: H:{moveInput.x:F2} V:{moveInput.y:F2}, Asc:{ascendInput:F1}, Desc:{descendInput:F1}");

        Vector3 horizontalMove = (transform.forward * moveInput.y + transform.right * moveInput.x);
        Vector3 verticalMove = Vector3.up * (ascendInput - descendInput);
        Vector3 finalMove = (horizontalMove + verticalMove).normalized; // ��_ normalized �T�O�t�פ@�P

        //if (finalMove.sqrMagnitude > 0.01f) Debug.Log($"[Spectator] Final Move Vector: {finalMove:F2}, Speed: {moveSpeed}");
        //if (moveSpeed <= 0f) Debug.LogWarning("[Spectator] Move Speed is 0!");

        transform.position += finalMove * moveSpeed * Time.deltaTime;
    }

    // ������ ��_���㪺 HandleHighlight �޿� (���e�i��~�R) ������
    private void HandleHighlight()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        HighlightableObject hitHighlightable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.transform.root != transform.root) // Make sure we don't target ourselves
            {
                hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
            }
        }

        if (hitHighlightable != currentlyTargetedObject)
        {
            if (currentlyTargetedObject != null)
            {
                currentlyTargetedObject.SetTargetedHighlight(false);
            }
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player"))
            {
                currentlyTargetedObject = hitHighlightable;
                currentlyTargetedObject.SetTargetedHighlight(true);
            }
            else
            {
                currentlyTargetedObject = null;
            }
        }

        // ��s�����e�� (�ݭn currentlyTargetedObject �M highlightMaterial �ҪO)
        if (currentlyTargetedObject != null && highlightMaterial != null) // �ˬd�ҪO�O�_�s�b
        {
            // �o�̻ݭn�z�L HighlightableObject �ӧ�s�A�T�O�����ۤv��������
            // �ڭ̰��] HighlightableObject �����B�z�ʺA�e�סA�p�G�S���A�ݭn�[�^�h
            // float distance = Vector3.Distance(transform.position, currentlyTargetedObject.transform.position);
            // float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
            // float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            // currentlyTargetedObject.UpdateOutlineWidth(newWidth); // ���] HighlightableObject ���o�Ӥ�k
        }

    }
    // ������������������������������������������������������

    // ������ �T�O OnSelectPerformed ��k�s�b�B�y�k���T ������
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // �O�����߮g�u�޿�
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                Debug.Log($"[Spectator] Select Fired! Target: {hit.transform.root.name}");
                teamManager.PossessCharacter(hit.transform.root.gameObject);
            }
            else
            {
                Debug.Log($"[Spectator] Select Fired! Hit {hit.collider.name} but not Player tag.");
            }
        }
        else
        {
            Debug.Log("[Spectator] Select Fired! Ray missed.");
        }
    } // <-- �T�O�o�̦��j�A�� }
    // ������������������������������������������������������
} // <-- �T�O Class ���j�A�� } �]�s�b