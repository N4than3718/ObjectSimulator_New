using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ControllableUnit
{
    [Tooltip("���⪫�� (�������� PlayerMovement2 �}��)")]
    public PlayerMovement2 character;
    [Tooltip("�o�Ө���M�ݪ���v�� (�������� CamControl �}��)")]
    public CamControl characterCamera;
    [Tooltip("��v����ڭn���H���I (�q�`�O���⩳�U���@�ӪŪ���)")]
    public Transform cameraFollowTarget;
}

public class TeamManager : MonoBehaviour
{
    [Header("�ζ��C��")]
    public List<ControllableUnit> team;

    private int activeCharacterIndex = 0;

    void Start()
    {
        if (team == null || team.Count == 0)
        {
            Debug.LogError("TeamManager ���ζ��C��O�Ū��I", this);
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
        // �����ª�
        if (team[activeCharacterIndex].character != null) team[activeCharacterIndex].character.enabled = false;
        var oldAnimator = team[activeCharacterIndex].character?.GetComponent<MovementAnimator>();
        if (oldAnimator != null) oldAnimator.enabled = false;
        if (team[activeCharacterIndex].characterCamera != null) team[activeCharacterIndex].characterCamera.gameObject.SetActive(false);

        // ��s����
        activeCharacterIndex = newIndex;
        ControllableUnit newUnit = team[activeCharacterIndex];

        // �ҥηs��
        if (newUnit.character != null && newUnit.characterCamera != null && newUnit.cameraFollowTarget != null)
        {
            newUnit.characterCamera.gameObject.SetActive(true);
            newUnit.character.enabled = true;
            var newAnimator = newUnit.character.GetComponent<MovementAnimator>();
            if (newAnimator != null) newAnimator.enabled = true;

            newUnit.character.cameraTransform = newUnit.characterCamera.transform;

            //��s��v�������H�ؼ�
            newUnit.characterCamera.FollowTarget = newUnit.cameraFollowTarget;
        }
        else
        {
            Debug.LogError($"�ζ������ެ� {newIndex} �����]�w������I");
        }
    }
}