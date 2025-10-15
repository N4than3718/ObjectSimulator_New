using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ControllableUnit
{
    [Tooltip("角色物件 (必須掛載 PlayerMovement2 腳本)")]
    public PlayerMovement2 character;
    [Tooltip("這個角色專屬的攝影機 (必須掛載 CamControl 腳本)")]
    public CamControl characterCamera;
    [Tooltip("攝影機實際要跟隨的點 (通常是角色底下的一個空物件)")]
    public Transform cameraFollowTarget;
}

public class TeamManager : MonoBehaviour
{
    [Header("團隊列表")]
    public List<ControllableUnit> team;

    private int activeCharacterIndex = 0;

    void Start()
    {
        if (team == null || team.Count == 0)
        {
            Debug.LogError("TeamManager 的團隊列表是空的！", this);
            return;
        }

        foreach (var unit in team)
        {
            if (unit.character != null) unit.character.enabled = false;
            if (unit.character != null)
            {
                var animator = unit.character.GetComponent<MovementAnimator>();
                if (animator != null) animator.enabled = false;
            }
            if (unit.characterCamera != null) unit.characterCamera.gameObject.SetActive(false);
        }

        SwitchToCharacter(0);
    }

    void Update()
    {
        if (team.Count <= 1) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            int nextIndex = (activeCharacterIndex + 1) % team.Count;
            SwitchToCharacter(nextIndex);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            int prevIndex = (activeCharacterIndex - 1 + team.Count) % team.Count;
            SwitchToCharacter(prevIndex);
        }
    }

    private void SwitchToCharacter(int newIndex)
    {
        // 停用舊的
        if (team[activeCharacterIndex].character != null) team[activeCharacterIndex].character.enabled = false;
        var oldAnimator = team[activeCharacterIndex].character?.GetComponent<MovementAnimator>();
        if (oldAnimator != null) oldAnimator.enabled = false;
        if (team[activeCharacterIndex].characterCamera != null) team[activeCharacterIndex].characterCamera.gameObject.SetActive(false);

        // 更新索引
        activeCharacterIndex = newIndex;
        ControllableUnit newUnit = team[activeCharacterIndex];

        // 啟用新的
        if (newUnit.character != null && newUnit.characterCamera != null && newUnit.cameraFollowTarget != null)
        {
            newUnit.characterCamera.gameObject.SetActive(true);
            newUnit.character.enabled = true;
            var newAnimator = newUnit.character.GetComponent<MovementAnimator>();
            if (newAnimator != null) newAnimator.enabled = true;

            newUnit.character.cameraTransform = newUnit.characterCamera.transform;

            //更新攝影機的跟隨目標
            newUnit.characterCamera.FollowTarget = newUnit.cameraFollowTarget;
        }
        else
        {
            Debug.LogError($"團隊中索引為 {newIndex} 的單位設定不完整！");
        }
    }
}