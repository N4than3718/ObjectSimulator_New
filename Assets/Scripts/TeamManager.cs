using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct TeamUnit
{
    public PlayerMovement character; // ���⪫�� (PlayerMovement �}��)
    public CamControl characterCamera; // <--- �令�����ޥ� CamControl �}��
    public Transform cameraFollowTarget;
    public bool isAvailable;
}

[RequireComponent(typeof(AudioSource))] // <-- [�s�W] �j��n�� AudioSource
public class TeamManager : MonoBehaviour
{
    public enum GameState { Spectator, Possessing }
    public enum SwitchMethod { Sequential, Direct, Unknown }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Spectator;
    private bool isTransitioning = false;

    [Header("Team Setup")]
    private const int MaxTeamSize = 8;
    // �}�C�|�b Start �ɳQ���� "��" (�]�� .character ���O null)
    public TeamUnit[] team = new TeamUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;
    private SpectatorController spectatorController; // !! <-- [�״_] �����ŧi

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager;

    [Header("���ĳ]�w (SFX)")] // <-- [�s�W]
    [SerializeField] private AudioClip sequentialSwitchSound; // <-- [�s�W] Q/E ���� (�Ҧp Switch_short)
    [SerializeField] private AudioClip directSwitchSound;     // <-- [�s�W] ���L/������ܭ��� (�Ҧp Switch)
    private AudioSource audioSource;

    [Header("��ı�ĪG")] // <--- [�s�W]
    [SerializeField] private float directTransitionDuration = 0.5f;   // <-- [��W/�s�W] ���L/������ܪ��ʵe�ɶ�
    [SerializeField] private float sequentialTransitionDuration = 0.2f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // �ʵe���u (�i��)

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;
    public GameState CurrentGameState => currentState;
    private Camera spectatorCameraComponent; // <--- [�s�W] �s Spectator �� Camera ����

    // --- ���}�ݩ� ---
    public Transform CurrentCameraTransform
    {
        get
        {
            if (currentState == GameState.Spectator && spectatorCameraObject != null)
            {
                return spectatorCameraObject.transform;
            }
            // !! [�״_] struct ����� '?'
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
            // !! [�״_] struct ����� '?'
            if (currentState == GameState.Possessing && activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
            {
                return team[activeCharacterIndex].character.gameObject;
            }
            return null;
        }
    }

    // --- Awake ---
    void Awake()
    {
        playerActions = new InputSystem_Actions();

        audioSource = GetComponent<AudioSource>(); // <-- [�s�W]
        if (audioSource == null) Debug.LogError("TeamManager �ʤ� AudioSource ����!", this.gameObject); // <-- [�s�W]
        else audioSource.playOnAwake = false;

        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();

        // !! [�״_] ���o SpectatorController ����
        if (spectatorCameraObject != null)
        {
            spectatorCameraComponent = spectatorCameraObject.GetComponent<Camera>(); // [�s�W]
            if (spectatorCameraComponent == null) Debug.LogError("SpectatorCameraObject �ʤ� Camera ����!", spectatorCameraObject); // [�s�W]
            spectatorController = spectatorCameraObject.GetComponent<SpectatorController>();
        }

        if (highlightManager == null) Debug.LogError("TeamManager cannot find HighlightManager!");
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
            Debug.LogError("Input Action 'SwitchNext' not found!");

        InputAction switchPrevAction = playerActions.Player.Previous;
        if (switchPrevAction != null)
            switchPrevAction.performed += ctx => SwitchPreviousCharacter();
        else
            Debug.LogError("Input Action 'SwitchPrevious' not found!");
    }

    // --- OnDisable ---
    private void OnDisable()
    {
        if (playerActions != null)
        {
            playerActions.Player.Disable();
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
        // �ˬd Spectator �M HighlightManager �O�_�s�b
        if (spectatorController == null || highlightManager == null)
        {
            Debug.LogError("TeamManager has missing references! (SpectatorController or HighlightManager)");
            return;
        }

        // --- �ѨM��סG��u�j��T�Ρv�޿�[�^�� ---

        // �@�~ 1: [����] �������� *�Ҧ�* �� PlayerMovement �}���ñj��T�Υ���
        // �o�i�H����ǡu�٨S�[�J����v������ť��J�C
        Debug.Log("Start: Finding and disabling all PlayerMovement scripts in scene...");
        var allCharacterScripts = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (var characterScript in allCharacterScripts)
        {
            // 1. �����T�θ}�� (Ĳ�o OnDisable ������J)
            characterScript.enabled = false;

            // 2. �T�Υ��b PlayerMovement �W�s������v�� (�p�G������)
            // (�o�O�ϥΧڭ̦b�W�@�ʷs�W�� myCharacterCamera ���)
            if (characterScript.myCharacterCamera != null)
            {
                characterScript.myCharacterCamera.gameObject.SetActive(false);
            }

            // 3. �T�ΰʵe (�n�ߺD)
            var animator = characterScript.GetComponent<MovementAnimator>();
            if (animator != null) animator.enabled = false;
        }
        Debug.Log($"Start: Force disabled {allCharacterScripts.Length} characters.");

        // �@�~ 2: [�O�d] ��l�ƥ��� *�w�g* �b Inspector �̳]�w�n������
        // (��A�ثe���u�šv�}�C�ӻ��A�o�@�B�|�������L�A�O���`��)
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i].character != null) // �u�������u�����F��~����
            {
                SetUnitControl(team[i], false, true);
            }
        }
        // ------------------------------------

        // �@�~ 3: [����] �i�J�[��̼Ҧ�
        EnterSpectatorMode();
    }

    // --- Update (�Ū�) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] �ˬd .character
            if (team[i].character?.gameObject == characterObject) { EnterPossessingMode(i, SwitchMethod.Sequential); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] �ˬd .character
            if (team[i].character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }

        int emptySlotIndex = -1;
        // !! [�״_] �ˬd .character �O�_�� null �ӧ�Ŧ�
        for (int i = 0; i < team.Length; i++) { if (team[i].character == null) { emptySlotIndex = i; break; } }

        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            if (pm == null) { Debug.LogError($"Object {characterObject.name} has no PlayerMovement script!"); return false; }

            // =================================================================
            // !! <-- [�֤߸ѨM���] <-- !!
            // �ڭ̤��A "�q"�A�ӬO�����h "��" PlayerMovement ������v���b��
            // �o�ݭn�A�w���� [�B�J 1] �M [�B�J 2]
            CamControl cam = pm.myCharacterCamera;
            Transform followTarget = pm.myFollowTarget;
            // =================================================================

            if (cam == null) { Debug.LogError($"Object {characterObject.name} cannot be added, its 'myCharacterCamera' field is not set in PlayerMovement!"); return false; }
            if (followTarget == null)
            {
                Debug.LogWarning($"{characterObject.name} has no follow target, using its own transform.");
                followTarget = characterObject.transform;
            }

            TeamUnit newUnit = new TeamUnit { character = pm, characterCamera = cam, cameraFollowTarget = followTarget, isAvailable = true };
            team[emptySlotIndex] = newUnit;
            Debug.Log($"Added {characterObject.name} to team slot {emptySlotIndex}.");

            if (possessAfterAdding) { EnterPossessingMode(emptySlotIndex, SwitchMethod.Sequential); }
            else { SetUnitControl(newUnit, false, true); }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
            return true;
        }
        else { Debug.Log("Team is full!"); return false; }
    }

    // --- RemoveCharacterFromTeam ---
    public void RemoveCharacterFromTeam(GameObject characterObject)
    {
        int foundIndex = -1;
        for (int i = 0; i < team.Length; i++)
        {
            // !! [�״_] �ˬd .character
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

            SetUnitControl(unitToRemove, false, true);
            // !! [�״_] �� new TeamUnit() �M�� struct
            team[foundIndex] = new TeamUnit();

            Debug.Log($"RemoveCharacter: FoundIndex={foundIndex}, CurrentActiveIndex={activeCharacterIndex}, CurrentState={currentState}");
            if (currentState == GameState.Possessing && activeCharacterIndex == foundIndex)
            {
                Debug.Log("Caught character was the active one. Attempting to switch...");
                SwitchToPreviousOrSpectator(foundIndex);
            }

            if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        }
        else { Debug.LogWarning($"Attempted to remove {characterObject.name}, but it wasn't found."); }
    }

    // --- SwitchToPreviousOrSpectator ---
    private void SwitchToPreviousOrSpectator(int removedIndex)
    {
        int nextAvailableIndex = -1;
        for (int i = 1; i < team.Length; i++)
        {
            int checkIndex = (removedIndex - i + team.Length) % team.Length;
            Debug.Log($"SwitchToPreviousOrSpectator: Checking index {checkIndex}. Character is {(team[checkIndex].character == null ? "NULL" : team[checkIndex].character.name)}");
            // !! [�״_] �ˬd .character
            if (team[checkIndex].character != null)
            {
                nextAvailableIndex = checkIndex;
                Debug.Log($"SwitchToPreviousOrSpectator: Found next available character at index {nextAvailableIndex}"); // <-- [�s�W]
                break;
            }
        }

        if (nextAvailableIndex != -1)
        {
            Debug.Log($"Switching control to team member at index {nextAvailableIndex}.");
            EnterPossessingMode(nextAvailableIndex);
        }
        else
        {
            Debug.Log("No other team members available. Switching to Spectator mode.");
            EnterSpectatorMode();
        }
    }

    // --- EnterSpectatorMode ---
    private void EnterSpectatorMode()
    {
        currentState = GameState.Spectator;
        // !! [�״_] �ˬd .character
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} before entering Spectator.");
            SetUnitControl(team[activeCharacterIndex], false, true);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        if (spectatorController != null) spectatorController.enabled = true; // �T�O
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        Debug.Log("Entered Spectator Mode.");
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex, SwitchMethod method = SwitchMethod.Sequential)
    {
        Debug.Log($"EnterPossessingMode called for index {newIndex}, method: {method}. isTransitioning={isTransitioning}"); // <-- [�s�W]
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring possess request."); return; } // ����J
        // !! [�״_] �ˬd .character
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex].character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode();
            return;
        }
        Debug.Log($"EnterPossessingMode: Index valid. Calling SwitchToCharacterByIndex..."); // <-- [�s�W]
        SwitchToCharacterByIndex(newIndex, method); // �� method �ǤU�h
    }

    // --- SwitchNextCharacter ---
    private void SwitchNextCharacter(SwitchMethod method = SwitchMethod.Sequential)
    {
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring switch request."); return; } // ����J
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int teamSize = team.Length;
        int currentValidIndex = activeCharacterIndex;

        for (int i = 1; i < teamSize; i++) // �̦h�d�� teamSize - 1 ��
        {
            int nextIndex = (currentValidIndex + i) % teamSize;
            if (team[nextIndex].character != null) // ���F�U�@�Ӧ��Ī�����
            {
                Debug.Log($"SwitchNextCharacter found target index: {nextIndex}. Calling SwitchToCharacterByIndex...");
                SwitchToCharacterByIndex(nextIndex, method); // <--- [�֤߭ק�] �I�s�Τ@�J�f
                return; // ���N����
            }
        }
    }

    // --- SwitchPreviousCharacter ---
    private void SwitchPreviousCharacter(SwitchMethod method = SwitchMethod.Sequential)
    {
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring switch request."); return; } // ����J
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int teamSize = team.Length;
        int currentValidIndex = activeCharacterIndex;

        for (int i = 1; i < teamSize; i++) // �̦h�d�� teamSize - 1 ��
        {
            int prevIndex = (currentValidIndex + i) % teamSize;
            if (team[prevIndex].character != null) // ���F�U�@�Ӧ��Ī�����
            {
                Debug.Log($"SwitchNextCharacter found target index: {prevIndex}. Calling SwitchToCharacterByIndex...");
                SwitchToCharacterByIndex(prevIndex, method); // <--- [�֤߭ק�] �I�s�Τ@�J�f
                return; // ���N����
            }
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex, SwitchMethod method = SwitchMethod.Unknown)
    {
        // !! [�״_] �ˬd .character
        if (activeCharacterIndex != -1 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            SetUnitControl(team[activeCharacterIndex], false);
        }
        activeCharacterIndex = newIndex;
        SetUnitControl(team[activeCharacterIndex], true);
        PlaySwitchSound(method);

        if (highlightManager != null)
        {
            highlightManager.ForceHighlightUpdate();
        }
        else
        {
            Debug.LogError("HighlightManager reference is missing in TeamManager!");
        }
    }

    public void SwitchToCharacterByIndex(int index, SwitchMethod method = SwitchMethod.Direct)
    {
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring switch request."); return; } // ����J
        // �򥻪���ɩM���ĩ��ˬd
        if (index < 0 || index >= team.Length || team[index].character == null)
        {
            Debug.LogWarning($"SwitchToCharacterByIndex: �L�Ī����� {index} �θӦ�m�L����C");
            return;
        }

        Transform startTransform = null;
        Transform endTransform = team[index].characterCamera.transform;

        if (currentState == GameState.Spectator)
        {
            startTransform = (spectatorCameraObject != null) ? spectatorCameraObject.transform : this.transform;
        }
        else if (currentState == GameState.Possessing)
        {
            if (index == activeCharacterIndex)
            {
                Debug.Log($"SwitchToCharacterByIndex: Index {index} is already active.");
                return; // �w�g�O��e����A������
            }

            startTransform = (activeCharacterIndex >= 0 && team[activeCharacterIndex].characterCamera != null) ? team[activeCharacterIndex].characterCamera.transform : spectatorCameraObject.transform; // �q�¨�����v���}�l
            if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
            {
                SetUnitControl(team[activeCharacterIndex], false, true); // �j��T��
            }
        }
        else { /* ... Error Log ... */ return; }

        if (startTransform == null || endTransform == null) { Debug.LogError("SwitchToCharacterByIndex: Start or End Transform is null!"); return; }
        StartCoroutine(TransitionCameraCoroutine(startTransform, endTransform, index, method));
    }

    private void PlaySwitchSound(SwitchMethod method)
    {
        AudioClip clipToPlay = null;
        switch (method)
        {
            case SwitchMethod.Sequential:
                clipToPlay = sequentialSwitchSound;
                break;
            case SwitchMethod.Direct:
                clipToPlay = directSwitchSound;
                break;
            case SwitchMethod.Unknown: // �p�G�����D�ӷ��A�i�H���w�]�Τ���
            default:
                Debug.LogWarning("PlaySwitchSound called with Unknown method.");
                // clipToPlay = sequentialSwitchSound; // �Ϊ̿�@�ӹw�]
                break;
        }

        if (audioSource != null && clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
        else if (clipToPlay == null)
        {
            Debug.LogWarning($"Switch sound not played: Clip for method '{method}' is not assigned.");
        }
    }

    public List<MonoBehaviour> GetAllCameraControllers()
    {
        List<MonoBehaviour> controllers = new List<MonoBehaviour>();

        // 1. �[�J Spectator Controller
        if (spectatorController != null)
        {
            controllers.Add(spectatorController);
        }

        // 2. �[�J����Ҧ����⪺ Camera Controller
        foreach (var unit in team)
        {
            if (unit.characterCamera != null) // �ˬd CamControl �ޥ�
            {
                controllers.Add(unit.characterCamera);
            }
        }
        return controllers;
    }

    // --- SetUnitControl ---
    private void SetUnitControl(TeamUnit unit, bool isActive, bool forceDisable = false)
    {
        // �ˬd���⥻���O�_�s�b
        if (unit.character == null)
        {
            return;
        }

        // �ˬd��v���ޥάO�_�s�b
        if (unit.characterCamera == null)
        {
            Debug.LogWarning($"SetUnitControl: {unit.character.name} is missing its CharacterCamera reference!");
        }

        unit.character.enabled = isActive;
        var animator = unit.character.GetComponent<MovementAnimator>();
        if (animator != null) animator.enabled = isActive;

        if (unit.characterCamera != null)
        {
            // Coroutine ������ isTransitioning ���� false, isActive �� true
            // �T���¨���� isTransitioning �i�ର true, isActive �� false
            unit.characterCamera.gameObject.SetActive(isActive); // <-- ���� Camera GO

            // �ҥ� CamControl �}�� (�p�G������)
            CamControl camScript = unit.characterCamera.GetComponent<CamControl>();
            if (camScript != null) camScript.enabled = isActive;
        }

        if (isActive)
        {
            if (unit.characterCamera != null)
            {
                unit.characterCamera.gameObject.SetActive(true);
                // ��o�x��v���� Transform �ǵ� PlayerMovement
                unit.character.cameraTransform = unit.characterCamera.transform;
                // !! [���n] �T�O CamControl ���H���T���ؼ�
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
                CamControl camScript = unit.characterCamera.GetComponent<CamControl>();
                if (camScript != null) camScript.FollowTarget = unit.cameraFollowTarget;
            }
            else
            {
                Debug.LogError($"{unit.character.name} has no camera assigned! Movement will be based on Spectator.");
                if (spectatorController != null) // !! [�״_] �ˬd
                {
                    unit.character.cameraTransform = spectatorController.transform;
                }
            }

            // (ForceHighlightUpdate ���� SwitchToCharacter ����)
        }
        else
        {
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
    // !! [�״_] ���^�������� TeamUnit? (nullable struct)
    private TeamUnit? FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i)
        {
            if (team[i].character?.gameObject == charObject) { return team[i]; }
        }

        // !! [�״_] �R�� "GetComponent" �޿�.
        // �p�G�����b `team` �}�C��, ���N "not found".

        return null;
    }

    // --- IsInTeam ---
    public bool IsInTeam(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i].character?.gameObject == characterObject) { return true; }
        }
        return false;
    }

    /// <summary>
    /// �B�z��v�����ƹL�窺 Coroutine
    /// </summary>
    /// <param name="startTransform">�_�l��m/����</param>
    /// <param name="endTransform">�ؼЦ�m/����</param>
    /// <param name="targetIndex">�ʵe������n�ҥΪ��������</param>
    private IEnumerator TransitionCameraCoroutine(Transform startTransform, Transform endTransform, int targetIndex, SwitchMethod method = SwitchMethod.Unknown)
    {
        isTransitioning = true; // �аO�}�l�ഫ
        Debug.Log($"Starting camera transition to index {targetIndex}...");

        float duration = (method == SwitchMethod.Sequential) ? sequentialTransitionDuration : directTransitionDuration;

        // --- �ǳƶ��q ---
        // 1. �T�O Spectator ��v������O Active ���A������O Inactive ��
        if (spectatorController != null) spectatorController.enabled = false;
        if (spectatorCameraObject != null) spectatorCameraObject.SetActive(true);
        if (spectatorCameraComponent != null) spectatorCameraComponent.enabled = true; // �T�O Camera ����ҥ�

        // 2. �T�O�ؼШ��⪺�Ҧ��F�� (�}��, Camera GO) ���O Inactive
        if (targetIndex >= 0 && targetIndex < team.Length && team[targetIndex].character != null)
        {
            SetUnitControl(team[targetIndex], false, true); // �j��T�Υؼ�
        }
        else { Debug.LogError($"Transition target index {targetIndex} is invalid!"); isTransitioning = false; yield break; }


        // 3. �� Spectator ��v���ߨ�"����"��_�l��m
        Transform transitionCamTransform = spectatorCameraObject.transform;
        transitionCamTransform.position = startTransform.position;
        transitionCamTransform.rotation = startTransform.rotation;

        // --- �ʵe���q ---
        float elapsedTime = 0f;
        PlaySwitchSound(method);
        while (elapsedTime < duration)
        {
            // �p�G�ؼЪ���b�ʵe���~�Q�R���F�A�פ�ʵe
            if (endTransform == null || team[targetIndex].characterCamera == null)
            {
                Debug.LogWarning("Camera transition target destroyed mid-animation. Aborting.");
                // �i��ݭn�M�w�^�� Spectator �Ҧ��ΰ���L�B�z
                EnterSpectatorMode(); // �^�� Spectator ����w��
                isTransitioning = false;
                yield break;
            }

            elapsedTime += Time.unscaledDeltaTime; // �ϥ� unscaledDeltaTime �קK�� TimeScale �v�T
            float t = Mathf.Clamp01(elapsedTime / duration);
            float curvedT = transitionCurve.Evaluate(t); // �ϥΦ��u

            transitionCamTransform.position = Vector3.Lerp(startTransform.position, endTransform.position, curvedT);
            transitionCamTransform.rotation = Quaternion.Slerp(startTransform.rotation, endTransform.rotation, curvedT);

            yield return null; // ���ݤU�@�V
        }

        // --- �������q ---
        Debug.Log($"Camera transition to index {targetIndex} finished.");
        // 1. ��T�]�w��̲צ�m/����
        transitionCamTransform.position = endTransform.position;
        transitionCamTransform.rotation = endTransform.rotation;

        // 2. ���� Spectator ��v������
        if (spectatorCameraObject != null) spectatorCameraObject.SetActive(false);
        if (spectatorCameraComponent != null) spectatorCameraComponent.enabled = false;

        // 3. ��s���A (�����b�ҥηs���⤧�e�I)
        currentState = GameState.Possessing;
        activeCharacterIndex = targetIndex;

        // 4. �ҥΥؼШ��� (SetUnitControl �|�ҥ� PlayerMovement, CamControl �M Camera GO)
        SetUnitControl(team[targetIndex], true);

        // 5. �Ѱ��ഫ�аO
        isTransitioning = false;
        Debug.Log($"Now possessing {team[targetIndex].character.name}");

        // 6. (�i��) �j���s���G�HSetUnitControl �̭����ӷ|���F
        // if (highlightManager != null) highlightManager.ForceHighlightUpdate();
    }
}