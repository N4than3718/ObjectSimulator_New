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
    public GameObject spectatorCameraObject; // 保持 GameObject 引用

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
        if (spectatorCameraObject == null)
        {
            Debug.LogError("Spectator Camera Object not assigned in TeamManager!");
            return;
        }

        // 確保遊戲一開始所有可能的預設隊員都處於非活動狀態
        // Note: 如果你的角色一開始就在場景裡，這個迴圈才有用
        // 如果角色是動態生成的，需要在生成時禁用它們
        // 我們假設它們一開始就在場景中
        var allCharacters = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var characterScript in allCharacters)
        {
            bool isInTeamArray = false;
            for (int i = 0; i < team.Length; ++i)
            {
                if (team[i] != null && team[i].character == characterScript)
                {
                    isInTeamArray = true;
                    break;
                }
            }
            // 如果不在初始team陣列中(雖然現在是空的)，也禁用它
            // 確保場景中所有可操控物件一開始都是關閉的
            // if (!isInTeamArray) {
            var unit = FindUnitByCharacter(characterScript.gameObject);
            SetUnitControl(unit, false, true); // 強制禁用
                                               // }
        }


        EnterSpectatorMode();
    }

    // PossessCharacter 邏輯不變
    public void PossessCharacter(GameObject characterObject)
    {
        // 1. 檢查是否已在隊伍中
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                EnterPossessingMode(i);
                return;
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

            if (pm == null || cam == null) { Debug.LogError($"Selected object {characterObject.name} is missing required components!"); return; }

            ControllableUnit newUnit = new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");
            EnterPossessingMode(emptySlotIndex);
        }
        else { Debug.Log("Team is full!"); }
    }

    // ▼▼▼ 核心修改：確保徹底停用舊角色 ▼▼▼
    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        // 檢查索引是否有效
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} (Slot {activeCharacterIndex}).");
            SetUnitControl(team[activeCharacterIndex], false, true); // 強制停用
        }
        else
        {
            Debug.Log("No active character to disable.");
        }
        activeCharacterIndex = -1; // 重置索引
        spectatorCameraObject.SetActive(true); // 啟用觀察者相機
        Debug.Log("Entered Spectator Mode.");
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // EnterPossessingMode 邏輯不變
    private void EnterPossessingMode(int newIndex)
    {
        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false); // 禁用觀察者相機
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name} (Slot {newIndex}).");
    }

    // 切換邏輯不變 (跳過空格)
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

    // SwitchToCharacter 邏輯不變
    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    // ▼▼▼ 核心修改：增加 forceDisable 參數，確保攝影機被關閉 ▼▼▼
    private void SetUnitControl(ControllableUnit unit, bool isActive, bool forceDisable = false)
    {
        if (unit == null || unit.character == null) return;

        unit.character.enabled = isActive;
        var animator = unit.character.GetComponent<MovementAnimator>();
        if (animator != null) animator.enabled = isActive;

        if (unit.characterCamera != null)
        {
            // 如果是強制停用，或者正常啟用/停用，都設定 SetActive
            if (forceDisable || unit.characterCamera.gameObject.activeSelf != isActive)
            {
                unit.characterCamera.gameObject.SetActive(isActive);
            }

            if (isActive && unit.cameraFollowTarget != null)
            {
                // 確保引用在啟用時被正確設定
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // 輔助函式不變
    private Transform FindInChildren(Transform parent, string name)
    {
        // ... (保持不變) ...
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // 新增輔助函式，根據 GameObject 查找 Unit (如果需要的話)
    private ControllableUnit FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == charObject)
            {
                return team[i];
            }
        }
        // 如果不在team陣列裡，嘗試從物件自身獲取元件來創建一個臨時的Unit引用，以便禁用
        PlayerMovement pm = charObject.GetComponent<PlayerMovement>();
        CamControl cam = charObject.GetComponentInChildren<CamControl>(true);
        Transform followTarget = FindInChildren(charObject.transform, "CameraFollowTarget") ?? charObject.transform;
        if (pm != null && cam != null)
        {
            return new ControllableUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget };
        }

        return null;
    }
}