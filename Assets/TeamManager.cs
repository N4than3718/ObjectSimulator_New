using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // �ɤJ�s�� Input System �R�W�Ŷ�

// ���U���O�O������
[System.Serializable]
public class ControllableUnit
{
    [Tooltip("���⪫�� (�������� PlayerMovement2 �}��)")]
    public PlayerMovement2 character;
    [Tooltip("�o�Ө���M�ݪ���v�� (�������� CamControl �}��)")]
    public CamControl characterCamera;
    [Tooltip("��v����ڭn���H���I")]
    public Transform cameraFollowTarget;
}

public class TeamManager : MonoBehaviour
{
    [Header("�ζ��C��")]
    public List<ControllableUnit> team;

    private int activeCharacterIndex = 0;
    private InputSystem_Actions playerActions; // �s�W Input System Action ���

    void Awake()
    {
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // �q�\�����ƥ�
        playerActions.Player.Next.performed += ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed += ctx => SwitchPreviousCharacter();
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        // �����q�\
        playerActions.Player.Next.performed -= ctx => SwitchNextCharacter();
        playerActions.Player.Previous.performed -= ctx => SwitchPreviousCharacter();
    }

    void Start()
    {
        if (team == null || team.Count == 0)
        {
            Debug.LogError("TeamManager ���ζ��C��O�Ū��I", this);
            return;
        }

        // ��l�ƩҦ����⪬�A
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

    // Update �禡�{�b�O�Ū��A�]���ڭ̤��A�ݭn���ӽ��߿�J
    void Update() { }

    // --- �s���ƥ�B�z�禡 ---
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
        // �����ª�
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

        // ��s���ިñҥηs��
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
            Debug.LogError($"�ζ������ެ� {newIndex} �����]�w������I");
        }
    }
}