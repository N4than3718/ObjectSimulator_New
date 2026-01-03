using UnityEngine;
using UnityEngine.InputSystem;
// using System.Collections.Generic; // Not needed currently
// using System.Linq; // Not needed currently

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float interactionDistance = 200f;

    [Header("References")]
    [SerializeField] private TeamManager teamManager;

    [Header("Highlighting")]
    // We still need the template reference here, even if HighlightableObject handles instantiation
    [Tooltip("高亮材質的模板")]
    [SerializeField] private Material highlightMaterial; // Keep template reference

    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;
    private float yaw;
    private float pitch;
    public bool IsInputPaused { get; set; } = false;

    private HighlightableObject currentlyTargetedObject;

    // --- Keep only ONE definition for each method ---

    void Awake()
    {
        if (GameDirector.Instance != null)
        {
            inputActions = GameDirector.Instance.playerActions;
        }
        else
        {
            inputActions = new InputSystem_Actions();
        }

        spectatorCamera = GetComponent<Camera>();
        Debug.Log("[Spectator] Awake called.");
    }

    private void OnEnable()
    {
        inputActions.Spectator.Enable();
        inputActions.Spectator.Select.performed += OnSelectPerformed;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[Spectator] Enabled, Action Map Active.");
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Spectator.Select.performed -= OnSelectPerformed;
            inputActions.Spectator.Disable();
        }

        // Ensure highlight is cleared when disabled
        if (currentlyTargetedObject != null)
        {
            currentlyTargetedObject.SetTargetedHighlight(false);
            currentlyTargetedObject = null;
        }
        // Cursor state might be handled elsewhere
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
        if (IsInputPaused)
        {
            return; // Don't process rotation, positioning, etc.
        }

        HandleLook();       // Call the single HandleLook method
        HandleMovement();   // Call the single HandleMovement method
        HandleHighlight();  // Call the single HandleHighlight method

        if (Time.timeScale <= 0f) Debug.LogError("[Spectator] Time.timeScale is 0! Game paused?");
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

        Vector3 horizontalMove = (transform.forward * moveInput.y + transform.right * moveInput.x);
        Vector3 verticalMove = Vector3.up * (ascendInput - descendInput);
        Vector3 finalMove = (horizontalMove + verticalMove).normalized; // Use normalized

        transform.position += finalMove * moveSpeed * Time.deltaTime;
    }

    private void HandleHighlight()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        HighlightableObject hitHighlightable = null;
        float hitDistance = interactionDistance;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.transform.root != transform.root) // Avoid highlighting self if collider is on parent
            {
                hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
                if (hitHighlightable != null) hitDistance = hit.distance;
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

        if (currentlyTargetedObject != null)
        {
            // Calculate and set outline width
            float t = Mathf.InverseLerp(0, maxDistanceForOutline, hitDistance);
            float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            currentlyTargetedObject.SetOutlineWidth(newWidth);
        }
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (this == null || teamManager == null) return;

        if (currentlyTargetedObject != null)
        {
            // 嘗試往上找，直到找到掛有 PlayerMovement 的那個物件
            var targetMovement = currentlyTargetedObject.GetComponentInParent<PlayerMovement>();

            if (targetMovement != null)
            {
                Debug.Log($"[Spectator] Select Fired! Target: {targetMovement.name}");
                // 傳入找到腳本的那個 GameObject
                TeamManager activeManager = TeamManager.Instance != null ? TeamManager.Instance : teamManager;
                activeManager.PossessCharacter(targetMovement.gameObject);
            }
            else
            {
                Debug.LogWarning($"[Spectator] {currentlyTargetedObject.name} 身上或父層找不到 PlayerMovement 腳本！");
            }
        }
        else
        {
            Debug.Log("[Spectator] Select Fired! No target selected.");
        }
    }
} // <-- Make sure this is the absolute final closing brace