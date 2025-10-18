using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct TeamUnit
{
    public PlayerMovement character; // 角色物件 (PlayerMovement 腳本)
    public CamControl characterCamera; // <--- 改成直接引用 CamControl 腳本
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
    // 陣列會在 Start 時被視為 "空" (因為 .character 都是 null)
    public TeamUnit[] team = new TeamUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;
    private SpectatorController spectatorController; // !! <-- [修復] 必須宣告

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager;

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    // --- 公開屬性 ---
    public Transform CurrentCameraTransform
    {
        get
        {
            if (currentState == GameState.Spectator && spectatorCameraObject != null)
            {
                return spectatorCameraObject.transform;
            }
            // !! [修復] struct 不能用 '?'
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
            // !! [修復] struct 不能用 '?'
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

        // !! [修復] 刪除 `team[i] = null;`。struct 陣列會自動初始化為 "空" (所有欄位為預設值)

        // ▼▼▼ 自動查找 ▼▼▼
        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();

        // !! [修復] 取得 SpectatorController 元件
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
        // !! [修復] 檢查 spectatorController
        if (spectatorController == null || highlightManager == null)
        {
            Debug.LogError("TeamManager has missing references! (SpectatorController or HighlightManager)");
            return;
        }

        // --- 解決方案：直接遍歷 Inspector 陣列 ---
        Debug.Log($"Initializing {team.Length} units from Inspector.");
        for (int i = 0; i < team.Length; i++)
        {
            // 由於陣列是空的 (team[i].character == null), 
            // SetUnitControl (line 388) 會直接 return, 這是正確的行為。
            SetUnitControl(team[i], false, true);
        }

        EnterSpectatorMode();
    }

    // --- Update (空的) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 檢查 .character
            if (team[i].character?.gameObject == characterObject) { EnterPossessingMode(i); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 檢查 .character
            if (team[i].character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }

        int emptySlotIndex = -1;
        // !! [修復] 檢查 .character 是否為 null 來找空位
        for (int i = 0; i < team.Length; i++) { if (team[i].character == null) { emptySlotIndex = i; break; } }

        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            if (pm == null) { Debug.LogError($"Object {characterObject.name} has no PlayerMovement script!"); return false; }

            // =================================================================
            // !! <-- [核心解決方案] <-- !!
            // 我們不再 "猜"，而是直接去 "問" PlayerMovement 它的攝影機在哪
            // 這需要你已完成 [步驟 1] 和 [步驟 2]
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
            // !! [修復] 檢查 .character
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
            // !! [修復] 用 new TeamUnit() 清空 struct
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
            // !! [修復] 檢查 .character
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
        // !! [修復] 檢查 .character
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} before entering Spectator.");
            SetUnitControl(team[activeCharacterIndex], false, true);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        if (spectatorController != null) spectatorController.enabled = true; // 確保
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        Debug.Log("Entered Spectator Mode.");
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex)
    {
        // !! [修復] 檢查 .character
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex].character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode();
            return;
        }

        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
        if (spectatorController != null) spectatorController.enabled = false; // 確保
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
            // !! [修復] 檢查 .character
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
            // !! [修復] 檢查 .character
            if (team[prevIndex].character != null) { SwitchToCharacter(prevIndex); return; }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex)
    {
        // !! [修復] 檢查 .character
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
        // 檢查角色本身是否存在
        if (unit.character == null)
        {
            return;
        }

        // 檢查攝影機引用是否存在
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
                // 把這台攝影機的 Transform 傳給 PlayerMovement
                unit.character.cameraTransform = unit.characterCamera.transform;
                // !! [重要] 確保 CamControl 跟隨正確的目標
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
            else
            {
                Debug.LogError($"{unit.character.name} has no camera assigned! Movement will be based on Spectator.");
                if (spectatorController != null) // !! [修復] 檢查
                {
                    unit.character.cameraTransform = spectatorController.transform;
                }
            }

            // (ForceHighlightUpdate 移到 SwitchToCharacter 結尾)
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
    // !! [修復] 更改回傳類型為 TeamUnit? (nullable struct)
    private TeamUnit? FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i)
        {
            if (team[i].character?.gameObject == charObject) { return team[i]; }
        }

        // !! [修復] 刪除 "GetComponent" 邏輯.
        // 如果它不在 `team` 陣列中, 它就 "not found".

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