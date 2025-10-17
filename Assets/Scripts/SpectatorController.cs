using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 需要這個來使用 List
using System.Linq; // 需要這個來使用 Linq

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;

    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Header("Highlighting")]
    [Tooltip("將你做好的黃色高亮 Material 拖到這裡")]
    [SerializeField] private Material highlightMaterial;

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    // --- 新增的變數，用來管理高亮狀態 ---
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
        // 確保離開模式時，清除所有高亮
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
        HandleHighlight(); // 在每一幀都處理高亮邏輯
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

    // ▼▼▼ 全新的高亮處理邏輯 ▼▼▼
    private void HandleHighlight()
    {
        Ray ray = spectatorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // 檢查是否射中了帶有 "Controllable" Tag 的物件
            if (hit.collider.CompareTag("Controllable"))
            {
                // 獲取物件上的第一個 Renderer 元件
                var renderer = hit.transform.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    // 如果這是一個新的物件，就切換高亮
                    if (currentlyHighlighted != renderer)
                    {
                        RestoreOriginalMaterials(); // 先移除舊的高亮
                        currentlyHighlighted = renderer;
                        StoreAndApplyHighlight(); // 再套用新的高亮
                    }
                    return; // 處理完畢，直接返回
                }
            }
        }

        // 如果射線沒打到任何東西，或打到的不是可操控物件，就清除高亮
        RestoreOriginalMaterials();
        currentlyHighlighted = null;
    }

    private void StoreAndApplyHighlight()
    {
        if (currentlyHighlighted == null || highlightMaterial == null) return;

        // 儲存原始的材質列表
        originalMaterials = currentlyHighlighted.materials;

        // 創建一個新的材質列表，包含所有原始材質，再加上我們的高亮材質
        var newMaterials = originalMaterials.ToList();
        newMaterials.Add(highlightMaterial);
        currentlyHighlighted.materials = newMaterials.ToArray();
    }

    private void RestoreOriginalMaterials()
    {
        if (currentlyHighlighted != null && originalMaterials != null)
        {
            // 還原原始的材質列表
            currentlyHighlighted.materials = originalMaterials;
        }
        // 清空狀態
        currentlyHighlighted = null;
        originalMaterials = null;
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // 只有當高亮了一個物件時，點擊才有效
        if (currentlyHighlighted != null)
        {
            teamManager.PossessCharacter(currentlyHighlighted.transform.root.gameObject);
        }
    }
}