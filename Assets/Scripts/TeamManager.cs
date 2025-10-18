using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ▼▼▼ 確保 ControllableUnit 定義在 TeamManager 外部 ▼▼▼
[System.Serializable]
public class ControllableUnit
{
    public PlayerMovement character; // 確保引用的是 PlayerMovement
    public CamControl characterCamera;
    public Transform cameraFollowTarget;
}
// ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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
        // ▼▼▼ 自動查找 HighlightManager ▼▼▼
        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();
        if (highlightManager == null) Debug.LogError("TeamManager cannot find HighlightManager!");
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
        if (spectatorCameraObject == null) { Debug.LogError("Spectator Camera Object not assigned!"); return; }

        var allCharacters = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var characterScript in allCharacters)
        {
            var unit = FindUnitByCharacter(characterScript.gameObject);
            SetUnitControl(unit, false, true); // 強制禁用
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
            Transform followTarget = FindInChildren(characterObject.transform, "Cam Follow Target") ?? characterObject.transform;
            if (pm == null || cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, missing components!"); return false; }
            ControllableUnit newUnit = new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
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

    // ▼▼▼ 新增：尋找下一個可用角色的輔助方法 ▼▼▼
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
        currentState = GameState.Spectator;
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false, true);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        // ▼▼▼ 進入觀察者模式後，強制更新高亮 ▼▼▼
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex)
    {
        // 在附身前，確保要附身的 unit 是有效的
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex]?.character == null)
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
    private void SetUnitControl(ControllableUnit unit, bool isActive, bool forceDisable = false)
    {
        if (unit?.character == null) return;

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