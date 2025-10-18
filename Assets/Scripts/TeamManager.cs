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
    // 現在這裡可以正確找到 ControllableUnit 了
    public TeamUnit[] team = new TeamUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;
    private SpectatorController spectatorController; // !! <-- [修復] 宣告變數

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager; // 把 HighlightManager 拖到這裡

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
            // !! [修復] 移除 team[activeCharacterIndex] 後面的 '?'
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
            // !! [修復] 移除 team[activeCharacterIndex] 後面的 '?'
            if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
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

        // !! [修復] 刪除 `team[i] = null;` 這行，`struct` 陣列不能存 `null`
        // for (int i = 0; i < team.Length; i++) team[i] = null; // <-- 這是錯誤的

        // ▼▼▼ 自動查找 HighlightManager & SpectatorController ▼▼▼
        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();

        // !! [修復] 取得 SpectatorController 元件
        if (spectatorCameraObject != null)
        {
            spectatorController = spectatorCameraObject.GetComponent<SpectatorController>();
        }

        // !! [修復] 更新錯誤檢查
        if (highlightManager == null) Debug.LogError("TeamManager cannot find HighlightManager!");
        if (spectatorController == null) Debug.LogError("TeamManager cannot find SpectatorController on SpectatorCameraObject!");
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }

    // --- OnEnable ---
    private void OnEnable()
    {
        if (playerActions == null) playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();

        // ▼▼▼ 加入 Action 存在性檢查 ▼▼▼
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
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
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
        // !! [修復] 檢查 spectatorController 而不是 spectatorCameraObject
        if (spectatorController == null || highlightManager == null)
        {
            Debug.LogError("TeamManager has missing references! (SpectatorController or HighlightManager)");
            return;
        }

        // --- 解決方案：直接遍歷 Inspector 陣列 ---
        Debug.Log($"Initializing {team.Length} units from Inspector.");
        for (int i = 0; i < team.Length; i++)
        {
            // 使用 team[i] 這個 struct
            SetUnitControl(team[i], false, true);
        }
        // ------------------------------------

        EnterSpectatorMode();
    }

    // --- Update (空的) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 移除 team[i] 後面的 '?'
            if (team[i].character?.gameObject == characterObject) { EnterPossessingMode(i); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 移除 team[i] 後面的 '?'
            if (team[i].character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }
        int emptySlotIndex = -1;
        // !! [修復] 檢查 team[i].character 是否為 null，而不是 team[i]
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

            // ▼▼▼ 加入新成員後，也強制更新一次高亮 ▼▼▼
            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            return true;
        }
        else { Debug.Log("Team is full!"); return false; }
    }

    // ▼▼▼ 新增：移除角色的公開方法 ▼▼▼
    public void RemoveCharacterFromTeam(GameObject characterObject)
    {
        int foundIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 移除 team[i] 後面的 '?'
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

            // 先禁用控制權，確保它不會再搞事
            SetUnitControl(unitToRemove, false, true);
            // !! [修復] 從隊伍中移除 (用 new TeamUnit() 清空 struct)
            team[foundIndex] = new TeamUnit();

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

    // ▼▼▼ 新增：尋找下一個可用角色的輔助方法 ▼▼▼
    private void SwitchToPreviousOrSpectator(int removedIndex)
    {
        int nextAvailableIndex = -1;
        // 從被移除位置的 *前一個* 位置開始反向搜索（這樣更符合 Q/E 的感覺）
        for (int i = 1; i < team.Length; i++)
        {
            int checkIndex = (removedIndex - i + team.Length) % team.Length;
            // !! [修復] 移除 team[checkIndex] 後面的 '?'
            if (team[checkIndex].character != null)
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
        currentState = GameState.Spectator;
        // 在重置 index 之前，先禁用當前活躍的角色 (如果有的話)
        // !! [修復] 檢查 .character 是否為 null
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} before entering Spectator.");
            SetUnitControl(team[activeCharacterIndex], false, true); // Use forceDisable = true
        }
        activeCharacterIndex = -1; // 然後才重置 index
        spectatorCameraObject.SetActive(true);
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        Debug.Log("Entered Spectator Mode.");
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex)
    {
        // 在附身前，確保要附身的 unit 是有效的
        // !! [修復] 檢查 .character 是否為 null
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex].character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode(); // 保險措施
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
            // !! [修復] 移除 team[nextIndex] 後面的 '?'
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
            // !! [修復] 移除 team[prevIndex] 後面的 '?'
            if (team[prevIndex].character != null) { SwitchToCharacter(prevIndex); return; }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex)
    {
        // !! [修復] 檢查 .character 是否為 null
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);

        // ▼▼▼ 核心修改：切換完成後，立刻強制更新高亮 ▼▼▼
        if (highlightManager != null)
        {
            highlightManager.ForceHighlightUpdate();
        }
        else
        {
            Debug.LogError("HighlightManager reference is missing in TeamManager!");
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }

    // --- SetUnitControl ---
    private void SetUnitControl(TeamUnit unit, bool isActive, bool forceDisable = false)
    {
        // 檢查角色本身是否存在
        if (unit.character == null)
        {
            // Debug.LogWarning("SetUnitControl received a unit with no character script.");
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
            // 啟用並賦值
            if (unit.characterCamera != null)
            {
                // 啟用攝影機物件
                unit.characterCamera.gameObject.SetActive(true);
                // 把這台攝影機的 Transform 傳給 PlayerMovement
                unit.character.cameraTransform = unit.characterCamera.transform;
            }
            else
            {
                // 備案：如果攝影機是 null，至少塞個東西避免 PlayerMovement 報錯
                Debug.LogError($"{unit.character.name} has no camera assigned! Movement will be based on Spectator.");
                // !! [修復] 確保 spectatorController 存在
                if (spectatorController != null)
                {
                    unit.character.cameraTransform = spectatorController.transform;
                }
            }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        }
        else
        {
            // 禁用
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
            // !! [修復] 移除 team[i] 後面的 '?'
            if (team[i].character?.gameObject == charObject) { return team[i]; }
        }

        PlayerMovement pm = charObject.GetComponent<PlayerMovement>();
        CamControl cam = charObject.GetComponentInChildren<CamControl>(true);
        Transform followTarget = FindInChildren(charObject.transform, "Cam Follow Target") ?? charObject.transform;
        if (pm != null && cam != null)
        {
            // !! [修復] 隱含轉換為 TeamUnit?
            return new TeamUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
        }
        // !! [修復] `return null` 現在合法了
        return null;
    }

    // --- IsInTeam ---
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 移除 team[i] 後面的 '?'
            if (team[i].character?.gameObject == characterObject) { return true; }
        }
        return false;
    }
}