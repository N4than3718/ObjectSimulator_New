using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ControllableUnit ���O�O������
[System.Serializable]
public class ControllableUnit
{
    public PlayerMovement character;
    public CamControl characterCamera;
    public Transform cameraFollowTarget;
}

public class TeamManager : MonoBehaviour
{
    public enum GameState { Spectator, Possessing }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Spectator;

    [Header("Team Setup")]
    [Tooltip("����̤j�e�q")]
    private const int MaxTeamSize = 8; // �]�w�T�w�e�q�� 8
    // ������ �֤߭ק�G�q List �אּ�T�w�j�p���}�C ������
    public ControllableUnit[] team = new ControllableUnit[MaxTeamSize];
    // ������������������������������������������������

    [Header("Scene References")]
    public GameObject spectatorCameraObject;

    private int activeCharacterIndex = -1; // -1 �N��S������Q����
    private InputSystem_Actions playerActions;

    void Awake()
    {
        playerActions = new InputSystem_Actions();
        // ��l�ư}�C�A�T�O�Ҧ���m���O null
        for (int i = 0; i < team.Length; i++)
        {
            team[i] = null;
        }
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        playerActions.Player.Next.performed += ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed += ctx => SwitchPreviousCharacter();
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.Next.performed -= ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed -= ctx => SwitchPreviousCharacter();
    }

    void Start()
    {
        if (spectatorCameraObject == null)
        {
            Debug.LogError("Spectator Camera Object not assigned in TeamManager!");
            return;
        }

        // �T�O�C���@�}�l�Ҧ��i�઺�w�]�������B��D���ʪ��A
        foreach (var unit in team)
        {
            SetUnitControl(unit, false); // �o�Ө禡�|�B�z null �����p
        }

        EnterSpectatorMode();
    }

    // Update �O�Ū�

    // ������ �֤߭ק�GPossessCharacter �{�b�]�t�u�[�J����v���޿� ������
    public void PossessCharacter(GameObject characterObject)
    {
        // 1. �ˬd�O�_�w�b���
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                EnterPossessingMode(i); // �p�G�w�b����A��������
                return;
            }
        }

        // 2. �p�G���b����A�M��Ů�
        int emptySlotIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] == null || team[i].character == null) // ���Ĥ@�ӪŮ�
            {
                emptySlotIndex = i;
                break;
            }
        }

        // 3. �p�G���Ů�A�h�[�J����ê���
        if (emptySlotIndex != -1)
        {
            // ����������n������
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            CamControl cam = characterObject.GetComponentInChildren<CamControl>(true); // �]�t�D���ʪ��l����
            Transform followTarget = FindInChildren(characterObject.transform, "Cam Follow Target"); // ���]���H�I�s�o�ӦW�r

            if (pm == null) { Debug.LogError($"Selected object {characterObject.name} missing PlayerMovement script!"); return; }
            if (cam == null) { Debug.LogError($"Selected object {characterObject.name} missing child Camera with CamControl script!"); return; }
            if (followTarget == null)
            {
                Debug.LogWarning($"Selected object {characterObject.name} missing child 'Cam Follow Target'. Falling back to root.");
                followTarget = characterObject.transform;
            }

            // �Ыطs�� ControllableUnit
            ControllableUnit newUnit = new ControllableUnit
            {
                character = pm,
                characterCamera = cam,
                cameraFollowTarget = followTarget
            };

            // ��J�Ů�
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");

            // �ߨ�����s�[�J������
            EnterPossessingMode(emptySlotIndex);
        }
        else
        {
            Debug.Log("Team is full! Cannot add more characters.");
            // �i�H�b�o�̥[�ӭ��Ĵ��ܪ��a����w��
        }
    }
    // ������������������������������������������������

    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        // Debug.Log("Entered Spectator Mode."); // �i�H���ѱ��קK Console ���
    }

    private void EnterPossessingMode(int newIndex)
    {
        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name} (Slot {newIndex}).");
    }

    // ������ �֤߭ק�G�����޿�{�b�|���L�Ů� ������
    private void SwitchNextCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int nextIndex = (activeCharacterIndex + 1) % team.Length;

        // �`���d��U�@�ӫD�Ū���l
        while (nextIndex != initialIndex)
        {
            if (team[nextIndex] != null && team[nextIndex].character != null)
            {
                SwitchToCharacter(nextIndex);
                return; // ���ä������\
            }
            nextIndex = (nextIndex + 1) % team.Length;
        }
        // �p�G¶�F�@�鳣�S����L�i�Ϊ�����
        Debug.Log("No other controllable character found in the team.");
    }

    private void SwitchPreviousCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int prevIndex = (activeCharacterIndex - 1 + team.Length) % team.Length; // �T�O�����`�O����

        // �`���d��W�@�ӫD�Ū���l
        while (prevIndex != initialIndex)
        {
            if (team[prevIndex] != null && team[prevIndex].character != null)
            {
                SwitchToCharacter(prevIndex);
                return; // ���ä������\
            }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
        Debug.Log("No other controllable character found in the team.");
    }
    // ������������������������������������������������

    // SwitchToCharacter �O�����ܡA���b�I�s�e�w�T�O newIndex �O���Ī�
    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    // SetUnitControl �[�J�F�� unit ������ null �ˬd
    private void SetUnitControl(ControllableUnit unit, bool isActive)
    {
        if (unit == null) return; // �p�G�o�Ӯ�l�O�Ū��A������^

        if (unit.character != null)
        {
            unit.character.enabled = isActive;
            var animator = unit.character.GetComponent<MovementAnimator>();
            if (animator != null) animator.enabled = isActive;
        }
        if (unit.characterCamera != null)
        {
            unit.characterCamera.gameObject.SetActive(isActive);
            if (isActive && unit.character != null && unit.cameraFollowTarget != null)
            {
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
        }
    }

    // ���U�禡�G���j�d��l����
    private Transform FindInChildren(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }
}