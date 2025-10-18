using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ������ �֤߭ק�G�T�O ControllableUnit �w�q�b TeamManager �~�� ������
[System.Serializable]
public class ControllableUnit
{
    public PlayerMovement character; // �T�{�O PlayerMovement
    public CamControl characterCamera;
    public Transform cameraFollowTarget;
}
// ����������������������������������������������������������������

public class TeamManager : MonoBehaviour
{
    public enum GameState { Spectator, Possessing }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Spectator;

    [Header("Team Setup")]
    private const int MaxTeamSize = 8;
    // �{�b�o�̥i�H���T��� ControllableUnit �F
    public ControllableUnit[] team = new ControllableUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    // --- Awake ---
    void Awake()
    {
        playerActions = new InputSystem_Actions();
        for (int i = 0; i < team.Length; i++) team[i] = null;
    }

    // --- OnEnable ---
    private void OnEnable()
    {
        // �T�O playerActions ��Ҧs�b
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();

        // ������ �֤߭ק�G�ˬd Action �O�_�s�b ������
        if (playerActions.Player.Next != null)
            playerActions.Player.Next.performed += ctx => SwitchNextCharacter();
        else
            Debug.LogError("Input Action 'SwitchNext' not found in Player map!");

        if (playerActions.Player.Previous != null)
            playerActions.Player.Previous.performed += ctx => SwitchPreviousCharacter();
        else
            Debug.LogError("Input Action 'SwitchPrevious' not found in Player map!");
        // ����������������������������������������������
    }

    // --- OnDisable ---
    private void OnDisable()
    {
        if (playerActions != null)
        {
            playerActions.Player.Disable();
            // �P�˥[�W null �ˬd
            if (playerActions.Player.Next != null)
                playerActions.Player.Next.performed -= ctx => SwitchNextCharacter();
            if (playerActions.Player.Previous != null)
                playerActions.Player.Previous.performed -= ctx => SwitchPreviousCharacter();
        }
    }

    // --- Start ---
    void Start()
    {
        if (spectatorCameraObject == null) { Debug.LogError("Spectator Camera Object not assigned!"); return; }

        // �T�γ������Ҧ� PlayerMovement (���]���̤@�}�l�N�b������)
        var allCharacters = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var characterScript in allCharacters)
        {
            var unit = FindUnitByCharacter(characterScript.gameObject);
            SetUnitControl(unit, false, true); // �j��T��
        }
        EnterSpectatorMode();
    }

    // --- Update (�Ū�) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i]?.character?.gameObject == characterObject) { EnterPossessingMode(i); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i]?.character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }
        int emptySlotIndex = -1;
        for (int i = 0; i < team.Length; i++) { if (team[i] == null || team[i].character == null) { emptySlotIndex = i; break; } }
        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            CamControl cam = characterObject.GetComponentInChildren<CamControl>(true);
            Transform followTarget = FindInChildren(characterObject.transform, "CameraFollowTarget") ?? characterObject.transform;
            if (pm == null || cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, missing components!"); return false; }
            ControllableUnit newUnit = new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");
            if (possessAfterAdding) { EnterPossessingMode(emptySlotIndex); }
            else { SetUnitControl(newUnit, false, true); }
            return true;
        }
        else { Debug.Log("Team is full!"); return false; }
    }

    // --- EnterSpectatorMode ---
    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false, true);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex)
    {
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
            if (team[nextIndex]?.character != null) { SwitchToCharacter(nextIndex); return; } // ²�� null �ˬd
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
            if (team[prevIndex]?.character != null) { SwitchToCharacter(prevIndex); return; } // ²�� null �ˬd
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    // --- SetUnitControl ---
    private void SetUnitControl(ControllableUnit unit, bool isActive, bool forceDisable = false)
    {
        if (unit?.character == null) return; // ²�� null �ˬd

        unit.character.enabled = isActive;
        var animator = unit.character.GetComponent<MovementAnimator>();
        if (animator != null) animator.enabled = isActive;

        if (unit.characterCamera != null)
        {
            if (forceDisable || unit.characterCamera.gameObject.activeSelf != isActive)
            {
                unit.characterCamera.gameObject.SetActive(isActive);
            }

            if (isActive && unit.cameraFollowTarget != null)
            {
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
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
    private ControllableUnit FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i) { if (team[i]?.character?.gameObject == charObject) { return team[i]; } } // ²�� null �ˬd
        PlayerMovement pm = charObject.GetComponent<PlayerMovement>();
        CamControl cam = charObject.GetComponentInChildren<CamControl>(true);
        Transform followTarget = FindInChildren(charObject.transform, "CameraFollowTarget") ?? charObject.transform;
        if (pm != null && cam != null) { return new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget }; }
        return null;
    }

    // --- IsInTeam ---
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++) { if (team[i]?.character?.gameObject == characterObject) { return true; } } // ²�� null �ˬd
        return false;
    }
}