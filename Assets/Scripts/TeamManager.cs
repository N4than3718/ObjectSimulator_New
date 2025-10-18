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

    [Header("Team Setup")]
    private const int MaxTeamSize = 8;
    public ControllableUnit[] team = new ControllableUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    void Awake()
    {
        playerActions = new InputSystem_Actions();
        for (int i = 0; i < team.Length; i++) team[i] = null;
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
        if (spectatorCameraObject == null) { Debug.LogError("Spectator Camera Object not assigned!"); return; }
        var allCharacters = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var characterScript in allCharacters)
        {
            var unit = FindUnitByCharacter(characterScript.gameObject);
            SetUnitControl(unit, false, true);
        }
        EnterSpectatorMode();
    }

    // ▼▼▼ 新增的公開方法 ▼▼▼
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                return true; // 找到了
            }
        }
        return false; // 沒找到
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲

    // PossessCharacter 現在只負責「附身」，不再包含「加入」邏輯
    public void PossessCharacter(GameObject characterObject)
    {
        // 檢查是否已在隊伍中
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                EnterPossessingMode(i); // 直接附身
                return;
            }
        }
        // 如果不在隊伍中 (Spectator 點擊了一個未加入的物件)，嘗試加入
        TryAddCharacterToTeam(characterObject, true); // 傳入 true 表示加入後立刻附身
    }

    // ▼▼▼ 新增的公開方法，供 PlayerMovement 呼叫 ▼▼▼
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        // 1. 檢查是否已在隊伍中
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                // 如果要求加入後附身，就直接附身
                if (possessAfterAdding) EnterPossessingMode(i);
                return false; // 告知 PlayerMovement 其實沒加成功 (因為本來就在)
            }
        }

        // 2. 尋找空格
        int emptySlotIndex = -1;
        for (int i = 0; i < team.Length; i++) { if (team[i] == null || team[i].character == null) { emptySlotIndex = i; break; } }

        // 3. 加入隊伍
        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            CamControl cam = characterObject.GetComponentInChildren<CamControl>(true);
            Transform followTarget = FindInChildren(characterObject.transform, "CameraFollowTarget") ?? characterObject.transform;

            if (pm == null || cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, missing components!"); return false; }

            ControllableUnit newUnit = new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");

            // 根據參數決定是否立刻附身
            if (possessAfterAdding)
            {
                EnterPossessingMode(emptySlotIndex);
            }
            else
            {
                // 如果不立刻附身，要確保新加入的成員的控制是關閉的
                SetUnitControl(newUnit, false, true);
            }
            return true; // 告知 PlayerMovement 添加成功
        }
        else
        {
            Debug.Log("Team is full!");
            return false; // 告知 PlayerMovement 隊伍已滿
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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

    private void EnterPossessingMode(int newIndex)
    {
        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name} (Slot {newIndex}).");
    }

    private void SwitchNextCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int nextIndex = (activeCharacterIndex + 1) % team.Length;
        while (nextIndex != initialIndex)
        {
            if (team[nextIndex] != null && team[nextIndex].character != null) { SwitchToCharacter(nextIndex); return; }
            nextIndex = (nextIndex + 1) % team.Length;
        }
    }
    private void SwitchPreviousCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int prevIndex = (activeCharacterIndex - 1 + team.Length) % team.Length;
        while (prevIndex != initialIndex)
        {
            if (team[prevIndex] != null && team[prevIndex].character != null) { SwitchToCharacter(prevIndex); return; }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
    }

    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    private void SetUnitControl(ControllableUnit unit, bool isActive, bool forceDisable = false)
    {
        if (unit == null || unit.character == null) return;

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

    private ControllableUnit FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i) { if (team[i] != null && team[i].character != null && team[i].character.gameObject == charObject) { return team[i]; } }
        PlayerMovement pm = charObject.GetComponent<PlayerMovement>();
        CamControl cam = charObject.GetComponentInChildren<CamControl>(true);
        Transform followTarget = FindInChildren(charObject.transform, "CameraFollowTarget") ?? charObject.transform;
        if (pm != null && cam != null) { return new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget }; }
        return null;
    }
}