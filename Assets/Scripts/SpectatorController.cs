using UnityEngine;
using UnityEngine.InputSystem;
// using System.Collections.Generic; //不再需要
// using System.Linq; //不再需要

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float interactionDistance = 200f; // 射線距離

    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    // ▼▼▼ 移除所有材質和 Outline 相關的 public 欄位 ▼▼▼
    // [Header("Highlighting")]
    // [SerializeField] private Material highlightMaterial;
    // [Header("Dynamic Outline")]
    // [SerializeField] private float minOutlineWidth = 0.003f;
    // ... etc ...
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;
    private float yaw;
    private float pitch;

    // ▼▼▼ 只保留一個引用，指向當前被瞄準的 HighlightableObject ▼▼▼
    private HighlightableObject currentlyTargetedObject;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void OnDisable()
    {
        inputActions.Spectator.Disable();
        inputActions.Spectator.Select.performed -= OnSelectPerformed;
        // 確保離開時取消目標高亮
        if (currentlyTargetedObject != null)
        {
            currentlyTargetedObject.SetTargetedHighlight(false);
            currentlyTargetedObject = null;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleHighlight();
    }
    private void HandleLook() {/*...*/}
    private void HandleMovement() {/*...*/}

    // ▼▼▼ 全新的 HandleHighlight ▼▼▼
    private void HandleHighlight()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        HighlightableObject hitHighlightable = null; // 儲存這次射線打到的物件

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            // 嘗試獲取 HighlightableObject 元件
            hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
        }

        // 如果這次打到的物件和上次的不一樣
        if (hitHighlightable != currentlyTargetedObject)
        {
            // 取消上一個物件的目標高亮
            if (currentlyTargetedObject != null)
            {
                currentlyTargetedObject.SetTargetedHighlight(false);
            }
            // 設定新的物件為目標高亮
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player")) // 確保是 Player Tag
            {
                currentlyTargetedObject = hitHighlightable;
                currentlyTargetedObject.SetTargetedHighlight(true);
            }
            else
            {
                currentlyTargetedObject = null; // 如果打到東西但不是 Player，也清除目標
            }
        }
        // 如果打到的物件和上次一樣，則什麼都不做，保持高亮
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // StoreAndApplyHighlight() 和 RestoreOriginalMaterials() 整個移除

    // ▼▼▼ OnSelectPerformed 現在使用 currentlyTargetedObject ▼▼▼
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (currentlyTargetedObject != null)
        {
            teamManager.PossessCharacter(currentlyTargetedObject.transform.root.gameObject);
        }
    }
    // Awake, OnEnable (no select), Start, HandleLook, HandleMovement remain similar
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
        // No need to clear highlight here, OnDisable handles it
    }

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null) Debug.LogError("SpectatorController needs a reference to the TeamManager!");
    }
}