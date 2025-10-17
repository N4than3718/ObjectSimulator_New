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
    [Tooltip("高亮材質的模板")]
    [SerializeField] private Material highlightMaterial;

    // ▼▼▼ 新增的動態輪廓參數 ▼▼▼
    [Header("Dynamic Outline")]
    [Tooltip("輪廓的最小寬度")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [Tooltip("輪廓的最大寬度")]
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [Tooltip("達到最大寬度所需的距離")]
    [SerializeField] private float maxDistanceForOutline = 50f;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    // --- 管理高亮狀態的變數 ---
    private Renderer currentlyHighlighted;
    private Material[] originalMaterials;
    private Material highlightInstance; // 我們動態創建的材質實例

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

        // 偵測滑鼠指向的物件
        if (Physics.Raycast(ray, out RaycastHit hit, 200f) && hit.collider.CompareTag("Controllable"))
        {
            var renderer = hit.transform.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // 如果指向了一個新的物件，就切換高亮
                if (currentlyHighlighted != renderer)
                {
                    RestoreOriginalMaterials();
                    currentlyHighlighted = renderer;
                    StoreAndApplyHighlight();
                }

                // --- 核心修改：每一幀都更新輪廓寬度 ---
                if (highlightInstance != null)
                {
                    float distance = Vector3.Distance(transform.position, currentlyHighlighted.transform.position);
                    // InverseLerp 會將距離映射到 0-1 的範圍
                    float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                    // Lerp 根據 0-1 的範圍，計算出在 min 和 max 之間對應的寬度
                    float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);

                    // 使用 SetFloat 更新 Shader 中的 _OutlineWidth 屬性
                    highlightInstance.SetFloat("_OutlineWidth", newWidth);
                }
                return;
            }
        }

        // 如果沒打到任何可操控物件，就清除高亮
        RestoreOriginalMaterials();
    }

    private void StoreAndApplyHighlight()
    {
        if (currentlyHighlighted == null || highlightMaterial == null) return;

        // 儲存原始材質
        originalMaterials = currentlyHighlighted.materials;

        // 創建高亮材質的實例
        highlightInstance = new Material(highlightMaterial);

        // 套用新材質列表（原始材質 + 高亮實例）
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

        // 清理狀態
        currentlyHighlighted = null;
        originalMaterials = null;

        // 如果存在材質實例，就銷毀它，避免記憶體洩漏
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