using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 恢復 List
using System.Linq; // 恢復 Linq

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float interactionDistance = 200f; // 恢復，高亮需要

    [Header("References")]
    [SerializeField] private TeamManager teamManager;

    // --- 恢復高亮相關參數 ---
    [Header("Highlighting")]
    [Tooltip("高亮材質的模板")]
    [SerializeField] private Material highlightMaterial; // 需要模板
    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;
    // --- 恢復結束 ---


    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    // --- 恢復高亮狀態變數 ---
    private HighlightableObject currentlyTargetedObject; // 改名，更清晰
    // --- 恢復結束 ---

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        spectatorCamera = GetComponent<Camera>();
        Debug.Log("[Spectator] Awake called.");
    }

    private void OnEnable()
    {
        inputActions.Spectator.Enable();
        // ▼▼▼ 修正：確認訂閱了正確的方法 ▼▼▼
        inputActions.Spectator.Select.performed += OnSelectPerformed;
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[Spectator] Enabled, Action Map Active.");
    }

    private void OnDisable()
    {
        inputActions.Spectator.Disable();
        // ▼▼▼ 修正：確認取消訂閱了正確的方法 ▼▼▼
        inputActions.Spectator.Select.performed -= OnSelectPerformed;
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
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
        // ▼▼▼ 修正：正確呼叫 HandleHighlight 方法 ▼▼▼
        HandleHighlight();
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
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
        Vector3 finalMove = (horizontalMove + verticalMove).normalized; // 恢復 normalized 確保速度一致

        //if (finalMove.sqrMagnitude > 0.01f) Debug.Log($"[Spectator] Final Move Vector: {finalMove:F2}, Speed: {moveSpeed}");
        //if (moveSpeed <= 0f) Debug.LogWarning("[Spectator] Move Speed is 0!");

        transform.position += finalMove * moveSpeed * Time.deltaTime;
    }

    // ▼▼▼ 恢復完整的 HandleHighlight 邏輯 (之前可能誤刪) ▼▼▼
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

        // 更新輪廓寬度 (需要 currentlyTargetedObject 和 highlightMaterial 模板)
        if (currentlyTargetedObject != null && highlightMaterial != null) // 檢查模板是否存在
        {
            // 這裡需要透過 HighlightableObject 來更新，確保它有自己的材質實例
            // 我們假設 HighlightableObject 內部處理動態寬度，如果沒有，需要加回去
            // float distance = Vector3.Distance(transform.position, currentlyTargetedObject.transform.position);
            // float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
            // float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            // currentlyTargetedObject.UpdateOutlineWidth(newWidth); // 假設 HighlightableObject 有這個方法
        }

    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // ▼▼▼ 確保 OnSelectPerformed 方法存在且語法正確 ▼▼▼
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // 保持中心射線邏輯
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
    } // <-- 確保這裡有大括號 }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
} // <-- 確保 Class 的大括號 } 也存在