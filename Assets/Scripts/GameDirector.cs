using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.InputSystem;

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum GameState { Playing, Paused, GameOver, Victory }

    [Header("Game State")]
    public GameState CurrentState;

    [Header("UI References")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public GameObject pauseMenuUI;
    [Tooltip("主選單的場景名稱 (破關後回去用)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("系統設定")]
    public int targetFrameRate = 60;
    public bool useVSync = false;

    public SpectatorController cameraScript;
    public InputSystem_Actions playerActions;

    public bool IsPaused { get; private set; } = false;

    private void Awake()
    {
        // 單例模式 (Singleton Pattern) - 確保只有一個導演
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        playerActions = new InputSystem_Actions(); // 整個遊戲只 new 這一次
        playerActions.Player.Enable(); // 在這裡統一起動

        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        Application.targetFrameRate = targetFrameRate;
        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        // 💀 Coder: 只綁定一個 Toggle 函數，避免邏輯衝突
        playerActions.Player.UnlockCursor.performed += OnTogglePauseInput;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.UnlockCursor.performed -= OnTogglePauseInput;
    }

    // 處理 Input System 的事件 (需要參數)
    private void OnTogglePauseInput(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    // 真正的邏輯中心：UI 按鈕也可以直接呼叫這個方法 (不需要參數)
    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        Debug.Log("GameManager: 嘗試暫停遊戲..."); // 🔍 這裡會告訴你事件有沒有觸發
        if (pauseMenuUI == null)
        {
            Debug.LogError("GameManager: 錯誤！pauseMenuUI 欄位是空的，請把選單物件拖進來！");
            return;
        }

        IsPaused = true;
        Time.timeScale = 0f; // 凍結物理與時間
        pauseMenuUI.SetActive(true);

        // 解鎖滑鼠
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 💀 Coder: 暫停所有攝影機輸入
        if (CamControl.Current != null) CamControl.Current.IsInputPaused = true;
        if (cameraScript != null) cameraScript.IsInputPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        pauseMenuUI.SetActive(false);

        // 鎖回滑鼠
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CamControl.Current != null) CamControl.Current.IsInputPaused = false;
        if (cameraScript != null) cameraScript.IsInputPaused = false;
    }

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        CurrentState = GameState.Playing;
        Time.timeScale = 1f; // 確保時間流動

        // 隱藏所有結算 UI
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);

        Debug.Log("Game Director: Action! 🎬");
    }

    public void TriggerVictory()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.Victory;
        Debug.Log("Game Director: Cut! It's a wrap. (Victory)");

        EndGameLogic(victoryPanel);
    }

    public void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.GameOver;
        Debug.Log("Game Director: Cut! Bad take. (Game Over)");

        EndGameLogic(gameOverPanel);
    }

    // 共用的結束邏輯
    private void EndGameLogic(GameObject panelToShow)
    {
        // 1. 顯示對應 UI
        if (panelToShow) panelToShow.SetActive(true);

        // 2. 暫停遊戲
        Time.timeScale = 0f;
        if (CamControl.Current != null) CamControl.Current.enabled = false;
        if (cameraScript != null) cameraScript.enabled = false;

        // 3. 解鎖滑鼠 (讓玩家可以點按鈕)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 給 UI 按鈕呼叫的
    public void RestartLevel()
    {
        Debug.Log("Game Director: Restarting...");
        // 直接呼叫經理
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.ReloadCurrentScene();
        }
        else
        {
            // 防呆：如果沒放經理，就用原始方法
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void GoToNextLevel()
    {
        Debug.Log("Game Director: Next Level...");

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadNextLevel();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // 請確保你的場景命名一致
    }
}