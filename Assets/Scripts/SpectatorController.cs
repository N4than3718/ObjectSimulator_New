using UnityEngine;
using UnityEngine.InputSystem;
// using System.Collections.Generic; //���A�ݭn
// using System.Linq; //���A�ݭn

[RequireComponent(typeof(Camera))]
public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float interactionDistance = 200f; // �g�u�Z��

    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    // ������ �����Ҧ�����M Outline ������ public ��� ������
    // [Header("Highlighting")]
    // [SerializeField] private Material highlightMaterial;
    // [Header("Dynamic Outline")]
    // [SerializeField] private float minOutlineWidth = 0.003f;
    // ... etc ...
    // ����������������������������������������

    private InputSystem_Actions inputActions;
    private Camera spectatorCamera;
    private float yaw;
    private float pitch;

    // ������ �u�O�d�@�ӤޥΡA���V��e�Q�˷Ǫ� HighlightableObject ������
    private HighlightableObject currentlyTargetedObject;
    // ����������������������������������������

    private void OnDisable()
    {
        inputActions.Spectator.Disable();
        inputActions.Spectator.Select.performed -= OnSelectPerformed;
        // �T�O���}�ɨ����ؼа��G
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

    // ������ ���s�� HandleHighlight ������
    private void HandleHighlight()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        HighlightableObject hitHighlightable = null; // �x�s�o���g�u���쪺����

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            // ������� HighlightableObject ����
            hitHighlightable = hit.collider.GetComponentInParent<HighlightableObject>();
        }

        // �p�G�o�����쪺����M�W�������@��
        if (hitHighlightable != currentlyTargetedObject)
        {
            // �����W�@�Ӫ��󪺥ؼа��G
            if (currentlyTargetedObject != null)
            {
                currentlyTargetedObject.SetTargetedHighlight(false);
            }
            // �]�w�s�����󬰥ؼа��G
            if (hitHighlightable != null && hitHighlightable.CompareTag("Player")) // �T�O�O Player Tag
            {
                currentlyTargetedObject = hitHighlightable;
                currentlyTargetedObject.SetTargetedHighlight(true);
            }
            else
            {
                currentlyTargetedObject = null; // �p�G����F������O Player�A�]�M���ؼ�
            }
        }
        // �p�G���쪺����M�W���@�ˡA�h���򳣤����A�O�����G
    }
    // ����������������������������������

    // StoreAndApplyHighlight() �M RestoreOriginalMaterials() ��Ӳ���

    // ������ OnSelectPerformed �{�b�ϥ� currentlyTargetedObject ������
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