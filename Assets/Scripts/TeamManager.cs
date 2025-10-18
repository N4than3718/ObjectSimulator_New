using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ControllableUnit 類別保持不變
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
    [Tooltip("隊伍的最大容量")]
    private const int MaxTeamSize = 8; // 設定固定容量為 8
    // ▼▼▼ 核心修改：從 List 改為固定大小的陣列 ▼▼▼
    public ControllableUnit[] team = new ControllableUnit[MaxTeamSize];
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    [Header("Scene References")]
    public GameObject spectatorCameraObject;

    private int activeCharacterIndex = -1; // -1 代表沒有角色被附身
    private InputSystem_Actions playerActions;

    void Awake()
    {
        playerActions = new InputSystem_Actions();
        // 初始化陣列，確保所有位置都是 null
        for (int i = 0; i < team.Length; i++)
        {
            team[i] = null;
        }
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
        foreach (var unit in team)
        {
            SetUnitControl(unit, false); // 這個函式會處理 null 的情況
        }

        EnterSpectatorMode();
    }

    // Update 是空的

    // ▼▼▼ 核心修改：PossessCharacter 現在包含「加入隊伍」的邏輯 ▼▼▼
    public void PossessCharacter(GameObject characterObject)
    {
        // 1. 檢查是否已在隊伍中
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                EnterPossessingMode(i); // 如果已在隊伍中，直接附身
                return;
            }
        }

        // 2. 如果不在隊伍中，尋找空格
        int emptySlotIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] == null || team[i].character == null) // 找到第一個空格
            {
                emptySlotIndex = i;
                break;
            }
        }

        // 3. 如果找到空格，則加入隊伍並附身
        if (emptySlotIndex != -1)
        {
            // 嘗試獲取必要的元件
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            CamControl cam = characterObject.GetComponentInChildren<CamControl>(true); // 包含非活動的子物件
            Transform followTarget = FindInChildren(characterObject.transform, "Cam Follow Target"); // 假設跟隨點叫這個名字

            if (pm == null) { Debug.LogError($"Selected object {characterObject.name} missing PlayerMovement script!"); return; }
            if (cam == null) { Debug.LogError($"Selected object {characterObject.name} missing child Camera with CamControl script!"); return; }
            if (followTarget == null)
            {
                Debug.LogWarning($"Selected object {characterObject.name} missing child 'Cam Follow Target'. Falling back to root.");
                followTarget = characterObject.transform;
            }

            // 創建新的 ControllableUnit
            ControllableUnit newUnit = new ControllableUnit
            {
                character = pm,
                characterCamera = cam,
                cameraFollowTarget = followTarget
            };

            // 放入空格
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");

            // 立刻附身新加入的角色
            EnterPossessingMode(emptySlotIndex);
        }
        else
        {
            Debug.Log("Team is full! Cannot add more characters.");
            // 可以在這裡加個音效提示玩家隊伍已滿
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        // Debug.Log("Entered Spectator Mode."); // 可以註解掉避免 Console 刷屏
    }

    private void EnterPossessingMode(int newIndex)
    {
        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false);
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name} (Slot {newIndex}).");
    }

    // ▼▼▼ 核心修改：切換邏輯現在會跳過空格 ▼▼▼
    private void SwitchNextCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int nextIndex = (activeCharacterIndex + 1) % team.Length;

        // 循環查找下一個非空的格子
        while (nextIndex != initialIndex)
        {
            if (team[nextIndex] != null && team[nextIndex].character != null)
            {
                SwitchToCharacter(nextIndex);
                return; // 找到並切換成功
            }
            nextIndex = (nextIndex + 1) % team.Length;
        }
        // 如果繞了一圈都沒找到其他可用的角色
        Debug.Log("No other controllable character found in the team.");
    }

    private void SwitchPreviousCharacter()
    {
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int initialIndex = activeCharacterIndex;
        int prevIndex = (activeCharacterIndex - 1 + team.Length) % team.Length; // 確保索引總是正數

        // 循環查找上一個非空的格子
        while (prevIndex != initialIndex)
        {
            if (team[prevIndex] != null && team[prevIndex].character != null)
            {
                SwitchToCharacter(prevIndex);
                return; // 找到並切換成功
            }
            prevIndex = (prevIndex - 1 + team.Length) % team.Length;
        }
        Debug.Log("No other controllable character found in the team.");
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // SwitchToCharacter 保持不變，但在呼叫前已確保 newIndex 是有效的
    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    // SetUnitControl 加入了對 unit 本身的 null 檢查
    private void SetUnitControl(ControllableUnit unit, bool isActive)
    {
        if (unit == null) return; // 如果這個格子是空的，直接返回

        if (unit.character != null)
        {
            unit.character.enabled = isActive;
            var animator = unit.character.GetComponent<MovementAnimator>();
            if (animator != null) animator.enabled = isActive;
        }
        if (unit.characterCamera != null)
        {
            unit.characterCamera.gameObject.SetActive(isActive);
            if (isActive && unit.character != null && unit.cameraFollowTarget != null)
            {
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
        }
    }

    // 輔助函式：遞迴查找子物件
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
}