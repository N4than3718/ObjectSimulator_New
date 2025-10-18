using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct TeamUnit
{
    public PlayerMovement character; // ���⪫�� (PlayerMovement �}��)
    public CamControl characterCamera; // <--- �令�����ޥ� CamControl �}��
    public Transform cameraFollowTarget;
    public bool isAvailable;
}

public class TeamManager : MonoBehaviour
{
    public enum GameState { Spectator, Possessing }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Spectator;

    [Header("Team Setup")]
    private const int MaxTeamSize = 8;
    // �}�C�|�b Start �ɳQ���� "��" (�]�� .character ���O null)
    public TeamUnit[] team = new TeamUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;
    private SpectatorController spectatorController; // !! <-- [�״_] �����ŧi

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager;

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    // --- ���}�ݩ� ---
    public Transform CurrentCameraTransform
    {
        get
        {
            if (currentState == GameState.Spectator && spectatorCameraObject != null)
            {
                return spectatorCameraObject.transform;
            }
            // !! [�״_] struct ����� '?'
            else if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].characterCamera != null)
            {
                return team[activeCharacterIndex].characterCamera.transform;
            }
            else
            {
                Debug.LogWarning("TeamManager couldn't determine the current active camera transform.");
                return spectatorCameraObject != null ? spectatorCameraObject.transform : null;
            }
        }
    }

    public GameObject ActiveCharacterGameObject
    {
        get
        {
            // !! [�״_] struct ����� '?'
            if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
            {
                return team[activeCharacterIndex].character.gameObject;
            }
            return null;
        }
    }

    // --- Awake ---
    void Awake()
    {
        playerActions = new InputSystem_Actions();

        // !! [�״_] �R�� `team[i] = null;`�Cstruct �}�C�|�۰ʪ�l�Ƭ� "��" (�Ҧ���쬰�w�]��)

        // ������ �۰ʬd�� ������
        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();

        // !! [�״_] ���o SpectatorController ����
        if (spectatorCameraObject != null)
        {
            spectatorController = spectatorCameraObject.GetComponent<SpectatorController>();
        }

        if (highlightManager == null) Debug.LogError("TeamManager cannot find HighlightManager!");
        if (spectatorController == null) Debug.LogError("TeamManager cannot find SpectatorController on SpectatorCameraObject!");
    }

    // --- OnEnable ---
    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();

        InputAction switchNextAction = playerActions.Player.Next;
        if (switchNextAction != null)
            switchNextAction.performed += ctx => SwitchNextCharacter();
        else
            Debug.LogError("Input Action 'SwitchNext' not found!");

        InputAction switchPrevAction = playerActions.Player.Previous;
        if (switchPrevAction != null)
            switchPrevAction.performed += ctx => SwitchPreviousCharacter();
        else
            Debug.LogError("Input Action 'SwitchPrevious' not found!");
    }

    // --- OnDisable ---
    private void OnDisable()
    {
        if (playerActions != null)
        {
            playerActions.Player.Disable();
            InputAction switchNextAction = playerActions.Player.Next;
            if (switchNextAction != null)
                switchNextAction.performed -= ctx => SwitchNextCharacter();

            InputAction switchPrevAction = playerActions.Player.Previous;
            if (switchPrevAction != null)
                switchPrevAction.performed -= ctx => SwitchPreviousCharacter();
        }
    }

    // --- Start ---
    void Start()
    {
        // �ˬd Spectator �M HighlightManager �O�_�s�b
        if (spectatorController == null || highlightManager == null)
        {
            Debug.LogError("TeamManager has missing references! (SpectatorController or HighlightManager)");
            return;
        }

        // --- �ѨM��סG��u�j��T�Ρv�޿�[�^�� ---

        // �@�~ 1: [����] �������� *�Ҧ�* �� PlayerMovement �}���ñj��T�Υ���
        // �o�i�H����ǡu�٨S�[�J����v������ť��J�C
        Debug.Log("Start: Finding and disabling all PlayerMovement scripts in scene...");
        var allCharacterScripts = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (var characterScript in allCharacterScripts)
        {
            // 1. �����T�θ}�� (Ĳ�o OnDisable ������J)
            characterScript.enabled = false;

            // 2. �T�Υ��b PlayerMovement �W�s������v�� (�p�G������)
            // (�o�O�ϥΧڭ̦b�W�@�ʷs�W�� myCharacterCamera ���)
            if (characterScript.myCharacterCamera != null)
            {
                characterScript.myCharacterCamera.gameObject.SetActive(false);
            }

            // 3. �T�ΰʵe (�n�ߺD)
            var animator = characterScript.GetComponent<MovementAnimator>();
            if (animator != null) animator.enabled = false;
        }
        Debug.Log($"Start: Force disabled {allCharacterScripts.Length} characters.");

        // �@�~ 2: [�O�d] ��l�ƥ��� *�w�g* �b Inspector �̳]�w�n������
        // (��A�ثe���u�šv�}�C�ӻ��A�o�@�B�|�������L�A�O���`��)
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i].character != null) // �u�������u�����F��~����
            {
                SetUnitControl(team[i], false, true);
            }
        }
        // ------------------------------------

        // �@�~ 3: [����] �i�J�[��̼Ҧ�
        EnterSpectatorMode();
    }

    // --- Update (�Ū�) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] �ˬd .character
            if (team[i].character?.gameObject == characterObject) { EnterPossessingMode(i); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] �ˬd .character
            if (team[i].character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }

        int emptySlotIndex = -1;
        // !! [�״_] �ˬd .character �O�_�� null �ӧ�Ŧ�
        for (int i = 0; i < team.Length; i++) { if (team[i].character == null) { emptySlotIndex = i; break; } }

        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            if (pm == null) { Debug.LogError($"Object {characterObject.name} has no PlayerMovement script!"); return false; }

            // =================================================================
            // !! <-- [�֤߸ѨM���] <-- !!
            // �ڭ̤��A "�q"�A�ӬO�����h "��" PlayerMovement ������v���b��
            // �o�ݭn�A�w���� [�B�J 1] �M [�B�J 2]
            CamControl cam = pm.myCharacterCamera;
            Transform followTarget = pm.myFollowTarget;
            // =================================================================

            if (cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, its 'myCharacterCamera' field is not set in PlayerMovement!"); return false; }
            if (followTarget == null)
            {
                Debug.LogWarning($"{characterObject.name} has no follow target, using its own transform.");
                followTarget = characterObject.transform;
            }

            TeamUnit newUnit = new TeamUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget, isAvailable = true };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");

            if (possessAfterAdding) { EnterPossessingMode(emptySlotIndex); }
            else { SetUnitControl(newUnit, false, true); }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
            return true;
        }
        else { Debug.Log("Team is full!"); return false; }
    }

    // --- RemoveCharacterFromTeam ---
    public void RemoveCharacterFromTeam(GameObject characterObject)
    {
        int foundIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] �ˬd .character
            if (team[i].character?.gameObject == characterObject)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex != -1)
        {
            Debug.Log($"Removing {characterObject.name} from team slot {foundIndex}.");
            TeamUnit unitToRemove = team[foundIndex];

            SetUnitControl(unitToRemove, false, true);
            // !! [�״_] �� new TeamUnit() �M�� struct
            team[foundIndex] = new TeamUnit();

            if (currentState == GameState.Possessing && activeCharacterIndex == foundIndex)
            {
                Debug.Log("Caught character was the active one. Attempting to switch...");
                SwitchToPreviousOrSpectator(foundIndex);
            }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        }
        else { Debug.LogWarning($"Attempted to remove {characterObject.name}, but it wasn't found."); }
    }

    // --- SwitchToPreviousOrSpectator ---
    private void SwitchToPreviousOrSpectator(int removedIndex)
    {
        int nextAvailableIndex = -1;
        for (int i = 1; i < team.Length; i++)
        {
            int checkIndex = (removedIndex - i + team.Length) % team.Length;
            // !! [�״_] �ˬd .character
            if (team[checkIndex].character != null)
            {
                nextAvailableIndex = checkIndex;
                break;
            }
        }

        if (nextAvailableIndex != -1)
        {
            Debug.Log($"Switching control to team member at index {nextAvailableIndex}.");
            EnterPossessingMode(nextAvailableIndex);
        }
        else
        {
            Debug.Log("No other team members available. Switching to Spectator mode.");
            EnterSpectatorMode();
        }
    }

    // --- EnterSpectatorMode ---
    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        // !! [�״_] �ˬd .character
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} before entering Spectator.");
            SetUnitControl(team[activeCharacterIndex], false, true);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        if (spectatorController != null) spectatorController.enabled = true; // �T�O
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        Debug.Log("Entered Spectator Mode.");
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex)
    {
        // !! [�״_] �ˬd .character
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex].character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode();
            return;
        }

        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
        if (spectatorController != null) spectatorController.enabled = false; // �T�O
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name} (Slot {newIndex}).");
    }

    // --- SwitchNextCharacter ---
    private void SwitchNextCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int nextIndex = (activeCharacterIndex + 1) % team.Length;
        while (nextIndex != initialIndex)
        {
            // !! [�״_] �ˬd .character
            if (team[nextIndex].character != null) { SwitchToCharacter(nextIndex); return; }
            nextIndex = (nextIndex + 1) % team.Length;
        }
    }

    // --- SwitchPreviousCharacter ---
    private void SwitchPreviousCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int prevIndex = (activeCharacterIndex - 1 + team.Length) % team.Length;
        while (prevIndex != initialIndex)
        {
            // !! [�״_] �ˬd .character
            if (team[prevIndex].character != null) { SwitchToCharacter(prevIndex); return; }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex)
    {
        // !! [�״_] �ˬd .character
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);

        if (highlightManager != null)
        {
            highlightManager.ForceHighlightUpdate();
        }
        else
        {
            Debug.LogError("HighlightManager reference is missing in TeamManager!");
        }
    }

    // --- SetUnitControl ---
    private void SetUnitControl(TeamUnit unit, bool isActive, bool forceDisable = false)
    {
        // �ˬd���⥻���O�_�s�b
        if (unit.character == null)
        {
            return;
        }

        // �ˬd��v���ޥάO�_�s�b
        if (unit.characterCamera == null)
        {
            Debug.LogWarning($"SetUnitControl: {unit.character.name} is missing its CharacterCamera reference!");
        }

        unit.character.enabled = isActive;
        var animator = unit.character.GetComponent<MovementAnimator>();
        if (animator != null) animator.enabled = isActive;

        if (isActive)
        {
            if (unit.characterCamera != null)
            {
                unit.characterCamera.gameObject.SetActive(true);
                // ��o�x��v���� Transform �ǵ� PlayerMovement
                unit.character.cameraTransform = unit.characterCamera.transform;
                // !! [���n] �T�O CamControl ���H���T���ؼ�
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
            else
            {
                Debug.LogError($"{unit.character.name} has no camera assigned! Movement will be based on Spectator.");
                if (spectatorController != null) // !! [�״_] �ˬd
                {
                    unit.character.cameraTransform = spectatorController.transform;
                }
            }

            // (ForceHighlightUpdate ���� SwitchToCharacter ����)
        }
        else
        {
            if (unit.characterCamera != null)
            {
                unit.characterCamera.gameObject.SetActive(false);
            }
        }
    }

    // --- FindInChildren ---
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

    // --- FindUnitByCharacter ---
    // !! [�״_] ���^�������� TeamUnit? (nullable struct)
    private TeamUnit? FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i)
        {
            if (team[i].character?.gameObject == charObject) { return team[i]; }
        }

        // !! [�״_] �R�� "GetComponent" �޿�.
        // �p�G�����b `team` �}�C��, ���N "not found".

        return null;
    }

    // --- IsInTeam ---
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i].character?.gameObject == characterObject) { return true; }
        }
        return false;
    }
}