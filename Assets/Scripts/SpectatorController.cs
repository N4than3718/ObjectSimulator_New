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
    [SerializeField] private Material highlightMaterial;

    [Header("Dynamic Outline")]
    [SerializeField] private float minOutlineWidth = 0.003f;
    [SerializeField] private float maxOutlineWidth = 0.04f;
    [SerializeField] private float maxDistanceForOutline = 50f;

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;

    private float yaw;
    private float pitch;

    private Renderer currentlyHighlighted;
    private Material[] originalMaterials;
    private Material highlightInstance;

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // ▼▼▼ 核心修改 ▼▼▼
            if (hit.collider.CompareTag("Player")) // 從 "Controllable" 改成 "Player"
            {
                // ▲▲▲▲▲▲▲▲▲▲
                var renderer = hit.transform.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    if (currentlyHighlighted != renderer)
                    {
                        RestoreOriginalMaterials();
                        currentlyHighlighted = renderer;
                        StoreAndApplyHighlight();
                    }

                    if (highlightInstance != null)
                    {
                        float distance = Vector3.Distance(transform.position, currentlyHighlighted.transform.position);
                        float t = Mathf.InverseLerp(0, maxDistanceForOutline, distance);
                        float newWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
                        highlightInstance.SetFloat("_OutlineWidth", newWidth);
                    }
                    return;
                }
            }
        }

        RestoreOriginalMaterials();
        currentlyHighlighted = null;
    }

    private void StoreAndApplyHighlight()
    {
        if (currentlyHighlighted == null || highlightMaterial == null) return;
        originalMaterials = currentlyHighlighted.materials;
        highlightInstance = new Material(highlightMaterial);
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
        currentlyHighlighted = null;
        originalMaterials = null;
        if (highlightInstance != null)
        {
            Destroy(highlightInstance);
            highlightInstance = null;
        }
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // ▼▼▼ 核心修改 ▼▼▼
            if (hit.collider.CompareTag("Player")) // 從 "Controllable" 改成 "Player"
            {
                // ▲▲▲▲▲▲▲▲▲▲
                teamManager.PossessCharacter(hit.transform.root.gameObject);
            }
        }
    }
}