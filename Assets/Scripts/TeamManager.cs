using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Team & Scene References")]
    public List<ControllableUnit> team;
    public GameObject spectatorCameraObject;

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    void Awake()
    {
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // ▼▼▼ 核心修改：移除 Unpossess 的訂閱 ▼▼▼
        // playerActions.Player.Unpossess.performed += OnUnpossess; 
        playerActions.Player.Next.performed += ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed += ctx => SwitchPreviousCharacter();
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // ▼▼▼ 核心修改：移除 Unpossess 的取消訂閱 ▼▼▼
        // playerActions.Player.Unpossess.performed -= OnUnpossess;
        playerActions.Player.Next.performed -= ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed -= ctx => SwitchPreviousCharacter();
    }

    void Start()
    {
        if (team == null || team.Count == 0) return;
        if (spectatorCameraObject == null) return;

        foreach (var unit in team)
        {
            SetUnitControl(unit, false);
        }

        EnterSpectatorMode();
    }

    void Update() { }

    public void PossessCharacter(GameObject characterObject)
    {
        int characterIndex = -1;
        for (int i = 0; i < team.Count; i++)
        {
            if (team[i].character.gameObject == characterObject)
            {
                characterIndex = i;
                break;
            }
        }

        if (characterIndex != -1)
        {
            EnterPossessingMode(characterIndex);
        }
    }

    // ▼▼▼ 核心修改：整個 OnUnpossess 函式被移除 ▼▼▼
    /*
    private void OnUnpossess(InputAction.CallbackContext context)
    {
        if (currentState == GameState.Possessing)
        {
            EnterSpectatorMode();
        }
    }
    */

    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        if (activeCharacterIndex != -1)
        {
            SetUnitControl(team[activeCharacterIndex], false);
            activeCharacterIndex = -1;
        }
        spectatorCameraObject.SetActive(true);
        Debug.Log("Entered Spectator Mode.");
    }

    private void EnterPossessingMode(int newIndex)
    {
        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name}.");
    }

    private void SwitchNextCharacter()
    {
        if (currentState != GameState.Possessing || team.Count <= 1) return;
        int nextIndex = (activeCharacterIndex + 1) % team.Count;
        SwitchToCharacter(nextIndex);
    }

    private void SwitchPreviousCharacter()
    {
        if (currentState != GameState.Possessing || team.Count <= 1) return;
        int prevIndex = (activeCharacterIndex - 1 + team.Count) % team.Count;
        SwitchToCharacter(prevIndex);
    }

    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    private void SetUnitControl(ControllableUnit unit, bool isActive)
    {
        if (unit.character != null)
        {
            unit.character.enabled = isActive;
            var animator = unit.character.GetComponent<MovementAnimator>();
            if (animator != null) animator.enabled = isActive;
        }
        if (unit.characterCamera != null)
        {
            unit.characterCamera.gameObject.SetActive(isActive);
            if (isActive)
            {
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
        }
    }
}