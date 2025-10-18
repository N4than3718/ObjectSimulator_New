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
    // �{�b�o�̥i�H���T��� ControllableUnit �F
    public TeamUnit[] team = new TeamUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;
    private SpectatorController spectatorController; // !! <-- [�״_] �ŧi�ܼ�

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager; // �� HighlightManager ���o��

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    // --- �s�W���}�ݩʡA�� HighlightManager �ϥ� ---
    public Transform CurrentCameraTransform
    {
        get
        {
            if (currentState == GameState.Spectator && spectatorCameraObject != null)
            {
                return spectatorCameraObject.transform;
            }
            // !! [�״_] ���� team[activeCharacterIndex] �᭱�� '?'
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
            // !! [�״_] ���� team[activeCharacterIndex] �᭱�� '?'
            if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
            {
                return team[activeCharacterIndex].character.gameObject;
            }
            return null; // �p�G���b�ޱ����A�ί��޵L�ġA��^ null
        }
    }

    // --- Awake ---
    void Awake()
    {
        playerActions = new InputSystem_Actions();

        // !! [�״_] �R�� `team[i] = null;` �o��A`struct` �}�C����s `null`
        // for (int i = 0; i < team.Length; i++) team[i] = null; // <-- �o�O���~��

        // ������ �۰ʬd�� HighlightManager & SpectatorController ������
        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();

        // !! [�״_] ���o SpectatorController ����
        if (spectatorCameraObject != null)
        {
            spectatorController = spectatorCameraObject.GetComponent<SpectatorController>();
        }

        // !! [�״_] ��s���~�ˬd
        if (highlightManager == null) Debug.LogError("TeamManager cannot find HighlightManager!");
        if (spectatorController == null) Debug.LogError("TeamManager cannot find SpectatorController on SpectatorCameraObject!");
        // ����������������������������������������
    }

    // --- OnEnable ---
    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();

        // ������ �[�J Action �s�b���ˬd ������
        InputAction switchNextAction = playerActions.Player.Next;
        if (switchNextAction != null)
            switchNextAction.performed += ctx => SwitchNextCharacter();
        else
            Debug.LogError("Input Action 'SwitchNext' not found in Player map! Please check InputSystem_Actions asset.");

        InputAction switchPrevAction = playerActions.Player.Previous;
        if (switchPrevAction != null)
            switchPrevAction.performed += ctx => SwitchPreviousCharacter();
        else
            Debug.LogError("Input Action 'SwitchPrevious' not found in Player map! Please check InputSystem_Actions asset.");
        // ����������������������������������������������
    }

    // --- OnDisable ---
    private void OnDisable()
    {
        if (playerActions != null)
        {
            playerActions.Player.Disable();
            // �P�˥[�W�ˬd�A�����q�\
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
        // !! [�״_] �ˬd spectatorController �Ӥ��O spectatorCameraObject
        if (spectatorController == null || highlightManager == null)
        {
            Debug.LogError("TeamManager has missing references! (SpectatorController or HighlightManager)");
            return;
        }

        // --- �ѨM��סG�����M�� Inspector �}�C ---
        Debug.Log($"Initializing {team.Length} units from Inspector.");
        for (int i = 0; i < team.Length; i++)
        {
            // �ϥ� team[i] �o�� struct
            SetUnitControl(team[i], false, true);
        }
        // ------------------------------------

        EnterSpectatorMode();
    }

    // --- Update (�Ū�) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] ���� team[i] �᭱�� '?'
            if (team[i].character?.gameObject == characterObject) { EnterPossessingMode(i); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] ���� team[i] �᭱�� '?'
            if (team[i].character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }
        int emptySlotIndex = -1;
        // !! [�״_] �ˬd team[i].character �O�_�� null�A�Ӥ��O team[i]
        for (int i = 0; i < team.Length; i++) { if (team[i].character == null) { emptySlotIndex = i; break; } }

        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            CamControl cam = characterObject.GetComponentInChildren<CamControl>(true);
            Transform followTarget = FindInChildren(characterObject.transform, "Cam Follow Target") ?? characterObject.transform;
            if (pm == null || cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, missing components!"); return false; }
            TeamUnit newUnit = new TeamUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");
            if (possessAfterAdding) { EnterPossessingMode(emptySlotIndex); }
            else { SetUnitControl(newUnit, false, true); }

            // ������ �[�J�s������A�]�j���s�@�����G ������
            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
            // ��������������������������������������������

            return true;
        }
        else { Debug.Log("Team is full!"); return false; }
    }

    // ������ �s�W�G�������⪺���}��k ������
    public void RemoveCharacterFromTeam(GameObject characterObject)
    {
        int foundIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] ���� team[i] �᭱�� '?'
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

            // ���T�α����v�A�T�O�����|�A�d��
            SetUnitControl(unitToRemove, false, true);
            // !! [�״_] �q������� (�� new TeamUnit() �M�� struct)
            team[foundIndex] = new TeamUnit();

            // �ˬd�Q�������O�_�O��e�ޱ�������
            if (currentState == GameState.Possessing && activeCharacterIndex == foundIndex)
            {
                Debug.Log("Caught character was the active one. Attempting to switch to another team member...");
                // ���դ�����U�@�ӥi�Ϊ�����
                SwitchToPreviousOrSpectator(foundIndex); // �ǤJ�Q����������
            }
            // �p�G���������O��e����A�h���򳣤��ΰ��A�~��ޱ��N�n

            // �q�� HighlightManager ��s
            if (highlightManager != null) highlightManager.ForceHighlightUpdate();

            // �i��G�T�� GameObject
            // characterObject.SetActive(false);
        }
        else { Debug.LogWarning($"Attempted to remove {characterObject.name}, but it wasn't found."); }
    }

    // ������ �s�W�G�M��U�@�ӥi�Ψ��⪺���U��k ������
    private void SwitchToPreviousOrSpectator(int removedIndex)
    {
        int nextAvailableIndex = -1;
        // �q�Q������m�� *�e�@��* ��m�}�l�ϦV�j���]�o�˧�ŦX Q/E ���Pı�^
        for (int i = 1; i < team.Length; i++)
        {
            int checkIndex = (removedIndex - i + team.Length) % team.Length;
            // !! [�״_] ���� team[checkIndex] �᭱�� '?'
            if (team[checkIndex].character != null)
            {
                nextAvailableIndex = checkIndex;
                break; // ���F�I
            }
        }

        if (nextAvailableIndex != -1)
        {
            // �p�G���F�Ʀs�̡A�N������
            Debug.Log($"Switching control to team member at index {nextAvailableIndex}.");
            EnterPossessingMode(nextAvailableIndex);
        }
        else
        {
            // �p�G¶�F�@�鳣�S���]��������^�A�~�^���[��̼Ҧ�
            Debug.Log("No other team members available. Switching to Spectator mode.");
            EnterSpectatorMode();
        }
    }

    // --- EnterSpectatorMode ---
    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        // �b���m index ���e�A���T�η�e���D������ (�p�G������)
        // !! [�״_] �ˬd .character �O�_�� null
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} before entering Spectator.");
            SetUnitControl(team[activeCharacterIndex], false, true); // Use forceDisable = true
        }
        activeCharacterIndex = -1; // �M��~���m index
        spectatorCameraObject.SetActive(true);
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        Debug.Log("Entered Spectator Mode.");
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex)
    {
        // �b�����e�A�T�O�n������ unit �O���Ī�
        // !! [�״_] �ˬd .character �O�_�� null
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex].character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode(); // �O�I���I
            return;
        }

        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
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
            // !! [�״_] ���� team[nextIndex] �᭱�� '?'
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
            // !! [�״_] ���� team[prevIndex] �᭱�� '?'
            if (team[prevIndex].character != null) { SwitchToCharacter(prevIndex); return; }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex)
    {
        // !! [�״_] �ˬd .character �O�_�� null
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);

        // ������ �֤߭ק�G����������A�ߨ�j���s���G ������
        if (highlightManager != null)
        {
            highlightManager.ForceHighlightUpdate();
        }
        else
        {
            Debug.LogError("HighlightManager reference is missing in TeamManager!");
        }
        // ������������������������������������������������������
    }

    // --- SetUnitControl ---
    private void SetUnitControl(TeamUnit unit, bool isActive, bool forceDisable = false)
    {
        // �ˬd���⥻���O�_�s�b
        if (unit.character == null)
        {
            // Debug.LogWarning("SetUnitControl received a unit with no character script.");
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
            // �ҥΨý��
            if (unit.characterCamera != null)
            {
                // �ҥ���v������
                unit.characterCamera.gameObject.SetActive(true);
                // ��o�x��v���� Transform �ǵ� PlayerMovement
                unit.character.cameraTransform = unit.characterCamera.transform;
            }
            else
            {
                // �ƮסG�p�G��v���O null�A�ܤֶ�ӪF���קK PlayerMovement ����
                Debug.LogError($"{unit.character.name} has no camera assigned! Movement will be based on Spectator.");
                // !! [�״_] �T�O spectatorController �s�b
                if (spectatorController != null)
                {
                    unit.character.cameraTransform = spectatorController.transform;
                }
            }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        }
        else
        {
            // �T��
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
            // !! [�״_] ���� team[i] �᭱�� '?'
            if (team[i].character?.gameObject == charObject) { return team[i]; }
        }

        PlayerMovement pm = charObject.GetComponent<PlayerMovement>();
        CamControl cam = charObject.GetComponentInChildren<CamControl>(true);
        Transform followTarget = FindInChildren(charObject.transform, "Cam Follow Target") ?? charObject.transform;
        if (pm != null && cam != null)
        {
            // !! [�״_] ���t�ഫ�� TeamUnit?
            return new TeamUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
        }
        // !! [�״_] `return null` �{�b�X�k�F
        return null;
    }

    // --- IsInTeam ---
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] ���� team[i] �᭱�� '?'
            if (team[i].character?.gameObject == characterObject) { return true; }
        }
        return false;
    }
}