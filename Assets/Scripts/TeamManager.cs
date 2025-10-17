using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 導入新的 Input System 命名空間

// 輔助類別保持不變
[System.Serializable]
public class ControllableUnit
{
    [Tooltip("角色物件 (必須掛載 PlayerMovement2 腳本)")]
    public PlayerMovement2 character;
    [Tooltip("這個角色專屬的攝影機 (必須掛載 CamControl 腳本)")]
    public CamControl characterCamera;
    [Tooltip("攝影機實際要跟隨的點")]
    public Transform cameraFollowTarget;
}

public class TeamManager : MonoBehaviour
{
    [Header("團隊列表")]
    public List<ControllableUnit> team;

    private int activeCharacterIndex = 0;
    private InputSystem_Actions playerActions; // 新增 Input System Action 實例

    void Awake()
    {
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // 訂閱切換事件
        playerActions.Player.Next.performed += ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed += ctx => SwitchPreviousCharacter();
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // 取消訂閱
        playerActions.Player.Next.performed -= ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed -= ctx => SwitchPreviousCharacter();
    }

    void Start()
    {
        if (team == null || team.Count == 0)
        {
            Debug.LogError("TeamManager 的團隊列表是空的！", this);
            return;
        }

        // 初始化所有角色狀態
        foreach (var unit in team)
        {
            if (unit.character != null)
            {
                unit.character.enabled = false;
                var animator = unit.character.GetComponent<MovementAnimator>();
                if (animator != null) animator.enabled = false;
            }
            if (unit.characterCamera != null)
            {
                unit.characterCamera.gameObject.SetActive(false);
            }
        }

        SwitchToCharacter(0);
    }

    // Update 函式現在是空的，因為我們不再需要它來輪詢輸入
    void Update() { }

    // --- 新的事件處理函式 ---
    private void SwitchNextCharacter()
    {
        if (team.Count <= 1) return;
        int nextIndex = (activeCharacterIndex + 1) % team.Count;
        SwitchToCharacter(nextIndex);
    }

    private void SwitchPreviousCharacter()
    {
        if (team.Count <= 1) return;
        int prevIndex = (activeCharacterIndex - 1 + team.Count) % team.Count;
        SwitchToCharacter(prevIndex);
    }

    private void SwitchToCharacter(int newIndex)
    {
        // 停用舊的
        if (team[activeCharacterIndex].character != null)
        {
            team[activeCharacterIndex].character.enabled = false;
            var oldAnimator = team[activeCharacterIndex].character.GetComponent<MovementAnimator>();
            if (oldAnimator != null) oldAnimator.enabled = false;
        }
        if (team[activeCharacterIndex].characterCamera != null)
        {
            team[activeCharacterIndex].characterCamera.gameObject.SetActive(false);
        }

        // 更新索引並啟用新的
        activeCharacterIndex = newIndex;
        ControllableUnit newUnit = team[activeCharacterIndex];

        if (newUnit.character != null && newUnit.characterCamera != null && newUnit.cameraFollowTarget != null)
        {
            newUnit.characterCamera.gameObject.SetActive(true);
            newUnit.character.enabled = true;
            var newAnimator = newUnit.character.GetComponent<MovementAnimator>();
            if (newAnimator != null) newAnimator.enabled = true;

            newUnit.character.cameraTransform = newUnit.characterCamera.transform;
            newUnit.characterCamera.FollowTarget = newUnit.cameraFollowTarget;
        }
        else
        {
            Debug.LogError($"團隊中索引為 {newIndex} 的單位設定不完整！");
        }
    }
}