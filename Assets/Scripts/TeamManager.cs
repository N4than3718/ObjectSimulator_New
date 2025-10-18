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
    public GameObject spectatorCameraObject; // �O�� GameObject �ޥ�

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

        // �T�O�C���@�}�l�Ҧ��i�઺�w�]�������B��D���ʪ��A
        // Note: �p�G�A������@�}�l�N�b�����̡A�o�Ӱj��~����
        // �p�G����O�ʺA�ͦ����A�ݭn�b�ͦ��ɸT�Υ���
        // �ڭ̰��]���̤@�}�l�N�b������
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
            // �p�G���b��lteam�}�C��(���M�{�b�O�Ū�)�A�]�T�Υ�
            // �T�O�������Ҧ��i�ޱ�����@�}�l���O������
            // if (!isInTeamArray) {
            var unit = FindUnitByCharacter(characterScript.gameObject);
            SetUnitControl(unit, false, true); // �j��T��
                                               // }
        }


        EnterSpectatorMode();
    }

    // PossessCharacter �޿褣��
    public void PossessCharacter(GameObject characterObject)
    {
        // 1. �ˬd�O�_�w�b���
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == characterObject)
            {
                EnterPossessingMode(i);
                return;
            }
        }
        // 2. �M��Ů�
        int emptySlotIndex = -1;
        for (int i = 0; i < team.Length; i++) { if (team[i] == null || team[i].character == null) { emptySlotIndex = i; break; } }
        // 3. �[�J����
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

    // ������ �֤߭ק�G�T�O���������¨��� ������
    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        // �ˬd���ެO�_����
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} (Slot {activeCharacterIndex}).");
            SetUnitControl(team[activeCharacterIndex], false, true); // �j���
        }
        else
        {
            Debug.Log("No active character to disable.");
        }
        activeCharacterIndex = -1; // ���m����
        spectatorCameraObject.SetActive(true); // �ҥ��[��̬۾�
        Debug.Log("Entered Spectator Mode.");
    }
    // ��������������������������������������

    // EnterPossessingMode �޿褣��
    private void EnterPossessingMode(int newIndex)
    {
        currentState = GameState.Possessing;
        spectatorCameraObject.SetActive(false); // �T���[��̬۾�
        SwitchToCharacter(newIndex);
        Debug.Log($"Possessing {team[newIndex].character.name} (Slot {newIndex}).");
    }

    // �����޿褣�� (���L�Ů�)
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

    // SwitchToCharacter �޿褣��
    private void SwitchToCharacter(int newIndex)
    {
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex] != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
    }

    // ������ �֤߭ק�G�W�[ forceDisable �ѼơA�T�O��v���Q���� ������
    private void SetUnitControl(ControllableUnit unit, bool isActive, bool forceDisable = false)
    {
        if (unit == null || unit.character == null) return;

        unit.character.enabled = isActive;
        var animator = unit.character.GetComponent<MovementAnimator>();
        if (animator != null) animator.enabled = isActive;

        if (unit.characterCamera != null)
        {
            // �p�G�O�j��ΡA�Ϊ̥��`�ҥ�/���ΡA���]�w SetActive
            if (forceDisable || unit.characterCamera.gameObject.activeSelf != isActive)
            {
                unit.characterCamera.gameObject.SetActive(isActive);
            }

            if (isActive && unit.cameraFollowTarget != null)
            {
                // �T�O�ޥΦb�ҥήɳQ���T�]�w
                unit.character.cameraTransform = unit.characterCamera.transform;
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
            }
        }
    }
    // ������������������������������������������

    // ���U�禡����
    private Transform FindInChildren(Transform parent, string name)
    {
        // ... (�O������) ...
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // �s�W���U�禡�A�ھ� GameObject �d�� Unit (�p�G�ݭn����)
    private ControllableUnit FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i)
        {
            if (team[i] != null && team[i].character != null && team[i].character.gameObject == charObject)
            {
                return team[i];
            }
        }
        // �p�G���bteam�}�C�̡A���ձq����ۨ��������ӳЫؤ@���{�ɪ�Unit�ޥΡA�H�K�T��
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