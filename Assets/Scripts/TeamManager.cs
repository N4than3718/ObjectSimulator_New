using System;
using System.Collections;
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

[RequireComponent(typeof(AudioSource))] // <-- [新增] 強制要有 AudioSource
public class TeamManager : MonoBehaviour
{
    public enum GameState { Spectator, Possessing }
    public enum SwitchMethod { Sequential, Direct, Unknown }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Spectator;
    private bool isTransitioning = false;

    [Header("Team Setup")]
    private const int MaxTeamSize = 8;
    // 陣列會在 Start 時被視為 "空" (因為 .character 都是 null)
    public TeamUnit[] team = new TeamUnit[MaxTeamSize];

    [Header("Scene References")]
    public GameObject spectatorCameraObject;
    private SpectatorController spectatorController; // !! <-- [修復] 必須宣告

    [Header("References (Add HighlightManager)")]
    [SerializeField] private HighlightManager highlightManager;

    [Header("音效設定 (SFX)")] // <-- [新增]
    [SerializeField] private AudioClip sequentialSwitchSound; // <-- [新增] Q/E 音效 (例如 Switch_short)
    [SerializeField] private AudioClip directSwitchSound;     // <-- [新增] 輪盤/直接選擇音效 (例如 Switch)
    private AudioSource audioSource;

    [Header("視覺效果")] // <--- [新增]
    [SerializeField] private float directTransitionDuration = 0.5f;   // <-- [改名/新增] 輪盤/直接選擇的動畫時間
    [SerializeField] private float sequentialTransitionDuration = 0.2f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 動畫曲線 (可選)

    private int activeCharacterIndex = -1;
    private InputSystem_Actions playerActions;
    public GameState CurrentGameState => currentState;
    private Camera spectatorCameraComponent; // <--- [新增] 存 Spectator 的 Camera 元件

    // --- 公開屬性 ---
    public Transform CurrentCameraTransform
    {
        get
        {
            if (currentState == GameState.Spectator && spectatorCameraObject != null)
            {
                return spectatorCameraObject.transform;
            }
            // !! [修復] struct 不能用 '?'
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
            // !! [修復] struct 不能用 '?'
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

        audioSource = GetComponent<AudioSource>(); // <-- [新增]
        if (audioSource == null) Debug.LogError("TeamManager 缺少 AudioSource 元件!", this.gameObject); // <-- [新增]
        else audioSource.playOnAwake = false;

        if (highlightManager == null) highlightManager = FindAnyObjectByType<HighlightManager>();

        // !! [修復] 取得 SpectatorController 元件
        if (spectatorCameraObject != null)
        {
            spectatorCameraComponent = spectatorCameraObject.GetComponent<Camera>(); // [新增]
            if (spectatorCameraComponent == null) Debug.LogError("SpectatorCameraObject 缺少 Camera 元件!", spectatorCameraObject); // [新增]
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
        // 檢查 Spectator 和 HighlightManager 是否存在
        if (spectatorController == null || highlightManager == null)
        {
            Debug.LogError("TeamManager has missing references! (SpectatorController or HighlightManager)");
            return;
        }

        // --- 解決方案：把「強制禁用」邏輯加回來 ---

        // 作業 1: [關鍵] 找到場景中 *所有* 的 PlayerMovement 腳本並強制禁用它們
        // 這可以防止那些「還沒加入隊伍」的物件偷聽輸入。
        Debug.Log("Start: Finding and disabling all PlayerMovement scripts in scene...");
        var allCharacterScripts = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (var characterScript in allCharacterScripts)
        {
            // 1. 直接禁用腳本 (觸發 OnDisable 關閉輸入)
            characterScript.enabled = false;

            // 2. 禁用它在 PlayerMovement 上連結的攝影機 (如果有的話)
            // (這是使用我們在上一動新增的 myCharacterCamera 欄位)
            if (characterScript.myCharacterCamera != null)
            {
                characterScript.myCharacterCamera.gameObject.SetActive(false);
            }

            // 3. 禁用動畫 (好習慣)
            var animator = characterScript.GetComponent<MovementAnimator>();
            if (animator != null) animator.enabled = false;
        }
        Debug.Log($"Start: Force disabled {allCharacterScripts.Length} characters.");

        // 作業 2: [保留] 初始化任何 *已經* 在 Inspector 裡設定好的隊友
        // (對你目前的「空」陣列來說，這一步會全部跳過，是正常的)
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i].character != null) // 只有當欄位真的有東西才執行
            {
                SetUnitControl(team[i], false, true);
            }
        }
        // ------------------------------------

        // 作業 3: [不變] 進入觀察者模式
        EnterSpectatorMode();
    }

    // --- Update (空的) ---
    void Update() { }

    // --- PossessCharacter ---
    public void PossessCharacter(GameObject characterObject)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 檢查 .character
            if (team[i].character?.gameObject == characterObject) { EnterPossessingMode(i, SwitchMethod.Sequential); return; }
        }
        TryAddCharacterToTeam(characterObject, true);
    }

    // --- TryAddCharacterToTeam ---
    public bool TryAddCharacterToTeam(GameObject characterObject, bool possessAfterAdding = false)
    {
        for (int i = 0; i < team.Length; i++)
        {
            // !! [修復] 檢查 .character
            if (team[i].character?.gameObject == characterObject)
            {
                Debug.Log($"{characterObject.name} is already in the team.");
                if (possessAfterAdding) EnterPossessingMode(i);
                return false;
            }
        }

        int emptySlotIndex = -1;
        // !! [修復] 檢查 .character 是否為 null 來找空位
        for (int i = 0; i < team.Length; i++) { if (team[i].character == null) { emptySlotIndex = i; break; } }

        if (emptySlotIndex != -1)
        {
            PlayerMovement pm = characterObject.GetComponent<PlayerMovement>();
            if (pm == null) { Debug.LogError($"Object {characterObject.name} has no PlayerMovement script!"); return false; }

            // =================================================================
            // !! <-- [核心解決方案] <-- !!
            // 我們不再 "猜"，而是直接去 "問" PlayerMovement 它的攝影機在哪
            // 這需要你已完成 [步驟 1] 和 [步驟 2]
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
            // !! [修復] 檢查 .character
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
            // !! [修復] 用 new TeamUnit() 清空 struct
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
            // !! [修復] 檢查 .character
            if (team[checkIndex].character != null)
            {
                nextAvailableIndex = checkIndex;
                Debug.Log($"SwitchToPreviousOrSpectator: Found next available character at index {nextAvailableIndex}"); // <-- [新增]
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
        // !! [修復] 檢查 .character
        if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
        {
            Debug.Log($"Disabling character {team[activeCharacterIndex].character.name} before entering Spectator.");
            SetUnitControl(team[activeCharacterIndex], false, true);
        }
        activeCharacterIndex = -1;
        spectatorCameraObject.SetActive(true);
        if (spectatorController != null) spectatorController.enabled = true; // 確保
        if (highlightManager != null) highlightManager.ForceHighlightUpdate();
        Debug.Log("Entered Spectator Mode.");
    }

    // --- EnterPossessingMode ---
    private void EnterPossessingMode(int newIndex, SwitchMethod method = SwitchMethod.Sequential)
    {
        Debug.Log($"EnterPossessingMode called for index {newIndex}, method: {method}. isTransitioning={isTransitioning}"); // <-- [新增]
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring possess request."); return; } // 防止重入
        // !! [修復] 檢查 .character
        if (newIndex < 0 || newIndex >= team.Length || team[newIndex].character == null)
        {
            Debug.LogError($"Attempted to possess invalid team index {newIndex}. Switching to Spectator.");
            EnterSpectatorMode();
            return;
        }
        Debug.Log($"EnterPossessingMode: Index valid. Calling SwitchToCharacterByIndex..."); // <-- [新增]
        SwitchToCharacterByIndex(newIndex, method); // 把 method 傳下去
    }

    // --- SwitchNextCharacter ---
    private void SwitchNextCharacter(SwitchMethod method = SwitchMethod.Sequential)
    {
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring switch request."); return; } // 防止重入
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int teamSize = team.Length;
        int currentValidIndex = activeCharacterIndex;

        for (int i = 1; i < teamSize; i++) // 最多查找 teamSize - 1 次
        {
            int nextIndex = (currentValidIndex + i) % teamSize;
            if (team[nextIndex].character != null) // 找到了下一個有效的隊友
            {
                Debug.Log($"SwitchNextCharacter found target index: {nextIndex}. Calling SwitchToCharacterByIndex...");
                SwitchToCharacterByIndex(nextIndex, method); // <--- [核心修改] 呼叫統一入口
                return; // 找到就結束
            }
        }
    }

    // --- SwitchPreviousCharacter ---
    private void SwitchPreviousCharacter(SwitchMethod method = SwitchMethod.Sequential)
    {
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring switch request."); return; } // 防止重入
        if (currentState != GameState.Possessing || team.Length <= 1) return;
        int teamSize = team.Length;
        int currentValidIndex = activeCharacterIndex;

        for (int i = 1; i < teamSize; i++) // 最多查找 teamSize - 1 次
        {
            int prevIndex = (currentValidIndex + i) % teamSize;
            if (team[prevIndex].character != null) // 找到了下一個有效的隊友
            {
                Debug.Log($"SwitchNextCharacter found target index: {prevIndex}. Calling SwitchToCharacterByIndex...");
                SwitchToCharacterByIndex(prevIndex, method); // <--- [核心修改] 呼叫統一入口
                return; // 找到就結束
            }
        }
    }

    // --- SwitchToCharacter ---
    private void SwitchToCharacter(int newIndex, SwitchMethod method = SwitchMethod.Unknown)
    {
        // !! [修復] 檢查 .character
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
        if (isTransitioning) { Debug.LogWarning("Already transitioning, ignoring switch request."); return; } // 防止重入
        // 基本的邊界和有效性檢查
        if (index < 0 || index >= team.Length || team[index].character == null)
        {
            Debug.LogWarning($"SwitchToCharacterByIndex: 無效的索引 {index} 或該位置無角色。");
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
                return; // 已經是當前角色，不做事
            }

            startTransform = (activeCharacterIndex >= 0 && team[activeCharacterIndex].characterCamera != null) ? team[activeCharacterIndex].characterCamera.transform : spectatorCameraObject.transform; // 從舊角色攝影機開始
            if (activeCharacterIndex >= 0 && activeCharacterIndex < team.Length && team[activeCharacterIndex].character != null)
            {
                SetUnitControl(team[activeCharacterIndex], false, true); // 強制禁用
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
            case SwitchMethod.Unknown: // 如果不知道來源，可以播預設或不播
            default:
                Debug.LogWarning("PlaySwitchSound called with Unknown method.");
                // clipToPlay = sequentialSwitchSound; // 或者選一個預設
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

        // 1. 加入 Spectator Controller
        if (spectatorController != null)
        {
            controllers.Add(spectatorController);
        }

        // 2. 加入隊伍中所有角色的 Camera Controller
        foreach (var unit in team)
        {
            if (unit.characterCamera != null) // 檢查 CamControl 引用
            {
                controllers.Add(unit.characterCamera);
            }
        }
        return controllers;
    }

    // --- SetUnitControl ---
    private void SetUnitControl(TeamUnit unit, bool isActive, bool forceDisable = false)
    {
        // 檢查角色本身是否存在
        if (unit.character == null)
        {
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

        if (unit.characterCamera != null)
        {
            // Coroutine 結束時 isTransitioning 應為 false, isActive 為 true
            // 禁用舊角色時 isTransitioning 可能為 true, isActive 為 false
            unit.characterCamera.gameObject.SetActive(isActive); // <-- 控制 Camera GO

            // 啟用 CamControl 腳本 (如果有的話)
            CamControl camScript = unit.characterCamera.GetComponent<CamControl>();
            if (camScript != null) camScript.enabled = isActive;
        }

        if (isActive)
        {
            if (unit.characterCamera != null)
            {
                unit.characterCamera.gameObject.SetActive(true);
                // 把這台攝影機的 Transform 傳給 PlayerMovement
                unit.character.cameraTransform = unit.characterCamera.transform;
                // !! [重要] 確保 CamControl 跟隨正確的目標
                unit.characterCamera.FollowTarget = unit.cameraFollowTarget;
                CamControl camScript = unit.characterCamera.GetComponent<CamControl>();
                if (camScript != null) camScript.FollowTarget = unit.cameraFollowTarget;
            }
            else
            {
                Debug.LogError($"{unit.character.name} has no camera assigned! Movement will be based on Spectator.");
                if (spectatorController != null) // !! [修復] 檢查
                {
                    unit.character.cameraTransform = spectatorController.transform;
                }
            }

            // (ForceHighlightUpdate 移到 SwitchToCharacter 結尾)
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
    // !! [修復] 更改回傳類型為 TeamUnit? (nullable struct)
    private TeamUnit? FindUnitByCharacter(GameObject charObject)
    {
        for (int i = 0; i < team.Length; ++i)
        {
            if (team[i].character?.gameObject == charObject) { return team[i]; }
        }

        // !! [修復] 刪除 "GetComponent" 邏輯.
        // 如果它不在 `team` 陣列中, 它就 "not found".

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
    /// 處理攝影機平滑過渡的 Coroutine
    /// </summary>
    /// <param name="startTransform">起始位置/旋轉</param>
    /// <param name="endTransform">目標位置/旋轉</param>
    /// <param name="targetIndex">動畫結束後要啟用的角色索引</param>
    private IEnumerator TransitionCameraCoroutine(Transform startTransform, Transform endTransform, int targetIndex, SwitchMethod method = SwitchMethod.Unknown)
    {
        isTransitioning = true; // 標記開始轉換
        Debug.Log($"Starting camera transition to index {targetIndex}...");

        float duration = (method == SwitchMethod.Sequential) ? sequentialTransitionDuration : directTransitionDuration;

        // --- 準備階段 ---
        // 1. 確保 Spectator 攝影機物件是 Active 的，但控制器是 Inactive 的
        if (spectatorController != null) spectatorController.enabled = false;
        if (spectatorCameraObject != null) spectatorCameraObject.SetActive(true);
        if (spectatorCameraComponent != null) spectatorCameraComponent.enabled = true; // 確保 Camera 元件啟用

        // 2. 確保目標角色的所有東西 (腳本, Camera GO) 都是 Inactive
        if (targetIndex >= 0 && targetIndex < team.Length && team[targetIndex].character != null)
        {
            SetUnitControl(team[targetIndex], false, true); // 強制禁用目標
        }
        else { Debug.LogError($"Transition target index {targetIndex} is invalid!"); isTransitioning = false; yield break; }


        // 3. 把 Spectator 攝影機立刻"瞬移"到起始位置
        Transform transitionCamTransform = spectatorCameraObject.transform;
        transitionCamTransform.position = startTransform.position;
        transitionCamTransform.rotation = startTransform.rotation;

        // --- 動畫階段 ---
        float elapsedTime = 0f;
        PlaySwitchSound(method);
        while (elapsedTime < duration)
        {
            // 如果目標物件在動畫中途被摧毀了，終止動畫
            if (endTransform == null || team[targetIndex].characterCamera == null)
            {
                Debug.LogWarning("Camera transition target destroyed mid-animation. Aborting.");
                // 可能需要決定回到 Spectator 模式或做其他處理
                EnterSpectatorMode(); // 回到 Spectator 比較安全
                isTransitioning = false;
                yield break;
            }

            elapsedTime += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 避免受 TimeScale 影響
            float t = Mathf.Clamp01(elapsedTime / duration);
            float curvedT = transitionCurve.Evaluate(t); // 使用曲線

            transitionCamTransform.position = Vector3.Lerp(startTransform.position, endTransform.position, curvedT);
            transitionCamTransform.rotation = Quaternion.Slerp(startTransform.rotation, endTransform.rotation, curvedT);

            yield return null; // 等待下一幀
        }

        // --- 結束階段 ---
        Debug.Log($"Camera transition to index {targetIndex} finished.");
        // 1. 精確設定到最終位置/旋轉
        transitionCamTransform.position = endTransform.position;
        transitionCamTransform.rotation = endTransform.rotation;

        // 2. 停用 Spectator 攝影機物件
        if (spectatorCameraObject != null) spectatorCameraObject.SetActive(false);
        if (spectatorCameraComponent != null) spectatorCameraComponent.enabled = false;

        // 3. 更新狀態 (必須在啟用新角色之前！)
        currentState = GameState.Possessing;
        activeCharacterIndex = targetIndex;

        // 4. 啟用目標角色 (SetUnitControl 會啟用 PlayerMovement, CamControl 和 Camera GO)
        SetUnitControl(team[targetIndex], true);

        // 5. 解除轉換標記
        isTransitioning = false;
        Debug.Log($"Now possessing {team[targetIndex].character.name}");

        // 6. (可選) 強制更新高亮？SetUnitControl 裡面應該會做了
        // if (highlightManager != null) highlightManager.ForceHighlightUpdate();
    }
}