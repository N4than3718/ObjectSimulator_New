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
    // 現在這裡可以正確找到 ControllableUnit 了
    public ControllableUnit[] team = new ControllableUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager; // 把 HighlightManager 拖到這裡

    private SpectatorController spectatorController;

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;

    // --- 新增公開屬性，供 HighlightManager 使用 ---
    public Transform CurrentCameraTransform
    {
        get
        {
            if (currentState == GameState.Spectator && spectatorCameraObject != null)
            {
                return spectatorCameraObject.transform;
            }
            else if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex]?.characterCamera != null)
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
            if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex]?.character != null)
            {
                return team[activeCharacterIndex].character.gameObject;
            }
            return null; // 如果不在操控狀態或索引無效，返回 null
        }
    }

    // --- Awake ---
    void Awake()
    {
        playerActions = new InputSystem_Actions();
        for (int i = 0; i < team.Length; i++) team[i] = null;
        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();
        if (highlightManager == null) Debug.LogError("TeamManager cannot find HighlightManager!");

        if (spectatorCameraObject != null)
        {
            spectatorController = spectatorCameraObject.GetComponent<SpectatorController>();
        }
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
            Debug.LogError("Input Action 'SwitchNext' not found in Player map! Please check InputSystem_Actions asset.");

        InputAction switchPrevAction = playerActions.Player.Previous;
        if (switchPrevAction != null)
            switchPrevAction.performed += ctx => SwitchPreviousCharacter();
        else
            Debug.LogError("Input Action 'SwitchPrevious' not found in Player map! Please check InputSystem_Actions asset.");
    }

    // --- OnDisable ---
    private void OnDisable()
    {
        if (playerActions != null)
        {
            playerActions.Player.Disable();
            // 同樣加上檢查再取消訂閱
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
        if (spectatorCameraObject == null || highlightManager == null) { Debug.LogError("TeamManager has missing references!"); return; }

        // 找到場景中 *所有* PlayerMovement 腳本
        var allCharacterScripts = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Debug.Log($"Found {allCharacterScripts.Length} PlayerMovement scripts in scene.");

        // 強制禁用它們及其相關組件
        foreach (var characterScript in allCharacterScripts)
        {
            Debug.Log($"Force disabling control for {characterScript.gameObject.name} on Start.");
            // 即使不在 team 陣列中，也嘗試查找或創建臨時引用來禁用
            var unitRef = FindUnitByCharacter(characterScript.gameObject);
            SetUnitControl(unitRef, false, true); // 使用 forceDisable = true
        }

        // 最後才進入觀察者模式
        EnterSpectatorMode();
    }

    // --- Update (空的) ---
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
            CamControl cam = pm?.myCameraControl;
            Transform followTarget = FindInChildren(characterObject.transform, "Cam Follow Target") ?? characterObject.transform;
            if (pm == null || cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, missing components!"); return false; }
            ControllableUnit newUnit = new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");
            if (possessAfterAdding) { EnterPossessingMode(emptySlotIndex); }
            else { SetUnitControl(newUnit, false, true); }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();

            return true;
        }
        else { Debug.Log("Team is full!"); return false; }
    }

    public void RemoveCharacterFromTeam(GameObject characterObject)
    {
        int foundIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i]?.character?.gameObject == characterObject)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex != -1)
        {
            Debug.Log($"Removing {characterObject.name} from team slot {foundIndex}.");
            ControllableUnit unitToRemove = team[foundIndex];

            // 先禁用控制權，確保它不會再搞事
            SetUnitControl(unitToRemove, false, true);
            // 從隊伍中移除
            team[foundIndex] = null;

            // 檢查被移除的是否是當前操控的角色
            if (currentState == GameState.Possessing && activeCharacterIndex == foundIndex)
            {
                Debug.Log("Caught character was the active one. Attempting to switch to another team member...");
                // 嘗試切換到下一個可用的角色
                SwitchToPreviousOrSpectator(foundIndex); // 傳入被移除的索引
            }
            // 如果移除的不是當前角色，則什麼都不用做，繼續操控就好

            // 通知 HighlightManager 更新
            if (highlightManager != null) highlightManager.ForceHighlightUpdate();

            // 可選：禁用 GameObject
            // characterObject.SetActive(false);
        }
        else { Debug.LogWarning($"Attempted to remove {characterObject.name}, but it wasn't found."); }
    }

    private void SwitchToPreviousOrSpectator(int removedIndex)
    {
        int nextAvailableIndex = -1;
        // 從被移除位置的 *前一個* 位置開始反向搜索（這樣更符合 Q/E 的感覺）
        for (int i = 1; i < team.Length; i++)
        {
            int checkIndex = (removedIndex - i + team.Length) % team.Length;
            if (team[checkIndex]?.character != null)
            {
                nextAvailableIndex = checkIndex;
                break; // 找到了！
            }
        }

        if (nextAvailableIndex != -1)
        {
            // 如果找到了倖存者，就附身它
            Debug.Log($"Switching control to team member at index {nextAvailableIndex}.");
            EnterPossessingMode(nextAvailableIndex);
        }
        else
        {
            // 如果繞了一圈都沒找到（隊伍全滅），才回到觀察者模式
            Debug.Log("No other team members available. Switching to Spectator mode.");
            EnterSpectatorMode();
        }
    }

    // --- EnterSpectatorMode ---
    private void EnterSpectatorMode()
    {
        Debug.Log("Attempting to enter Spectator Mode...");
        currentState = GameState.Spectator;

        // 1. 強制停用當前活躍角色（如果有的話）
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            Debug.Log($"Disabling character in slot {activeCharacterIndex}: {team[activeCharacterIndex].character?.name}");
            SetUnitControl(team[activeCharacterIndex], false, true); // Force disable
        }
        activeCharacterIndex = -1; // 重置索引

        // 2. 啟用觀察者攝影機 GameObject
        if (spectatorCameraObject != null)
        {
            spectatorCameraObject.SetActive(true);
            Debug.Log("Spectator Camera GameObject Activated.");
        }
        else { Debug.LogError("Spectator Camera Object is null!"); return; }

        // 3. 啟用觀察者控制器腳本
        if (spectatorController != null)
        {
            spectatorController.enabled = true;
            Debug.Log("SpectatorController Script Enabled.");
        }
        else { Debug.LogError("SpectatorController reference is null!"); return; }


        // 4. 更新高亮
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
    }

    private void EnterPossessingMode(int newIndex)
    {
        // 附身前檢查
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex]?.character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode();
            return;
        }

        Debug.Log($"Attempting to possess {team[newIndex].character.name} (Slot {newIndex})...");
        currentState = GameState.Possessing;

        // 1. 停用觀察者控制器腳本
        if (spectatorController != null)
        {
            spectatorController.enabled = false;
            Debug.Log("SpectatorController Script Disabled.");
        }
        else { Debug.LogError("SpectatorController reference is null!"); }

        // 2. 停用觀察者攝影機 GameObject
        if (spectatorCameraObject != null)
        {
            spectatorCameraObject.SetActive(false);
            Debug.Log("Spectator Camera GameObject Deactivated.");
        }
        else { Debug.LogError("Spectator Camera Object is null!"); }


        // 3. 切換到新角色 (SwitchToCharacter 會啟用新角色並更新高亮)
        SwitchToCharacter(newIndex);
    }

    // --- SwitchNextCharacter ---
    private void SwitchNextCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int nextIndex = (activeCharacterIndex + 1) % team.Length;
        while (nextIndex != initialIndex)
        {
            if (team[nextIndex]?.character != null) { SwitchToCharacter(nextIndex); return; }
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
            if (team[prevIndex]?.character != null) { SwitchToCharacter(prevIndex); return; }
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
    private void SetUnitControl(ControllableUnit unit, bool isActive, bool forceDisable = false)
    {
        // 稍微簡化 null 檢查
        if (unit?.character == null)
        {
            // Debug.LogWarning("SetUnitControl called with null unit or character.");
            return;
        }

        // 啟用/禁用腳本
        unit.character.enabled = isActive;
        var animator = unit.character.GetComponent<MovementAnimator>();
        if (animator != null) animator.enabled = isActive;

        // 啟用/禁用攝影機 GameObject
        if (unit.characterCamera != null)
        {
            // 如果是強制停用，或者當前狀態與目標狀態不符，就執行 SetActive
            if (forceDisable || unit.characterCamera.gameObject.activeSelf != isActive)
            {
                // Debug.Log($"Setting camera {unit.characterCamera.name} active state to: {isActive}");
                unit.characterCamera.gameObject.SetActive(isActive);
            }

            // 如果是啟用狀態，確保引用被正確設定
            if (isActive && unit.cameraFollowTarget != null)
            {
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
        }
        else if (isActive)
        {
            Debug.LogWarning($"Character {unit.character.name} has no assigned Character Camera in TeamManager unit.");
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
        for (int i = 0; i < team.Length; ++i) { if (team[i]?.character?.gameObject == charObject) { return team[i]; } }
        PlayerMovement pm = charObject.GetComponent<PlayerMovement>();
        CamControl cam = charObject.GetComponentInChildren<CamControl>(true);
        Transform followTarget = FindInChildren(charObject.transform, "Cam Follow Target") ?? charObject.transform;
        if (pm != null && cam != null) { return new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget }; }
        return null;
    }

    // --- IsInTeam ---
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++) { if (team[i]?.character?.gameObject == characterObject) { return true; } }
        return false;
    }
}