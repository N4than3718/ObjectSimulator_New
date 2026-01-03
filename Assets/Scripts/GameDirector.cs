using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }
    public InputSystem_Actions playerActions; // 💀 核心：統一提供輸入源
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
    public bool IsPaused { get; private set; } = false;

    private void Awake()
    {
        // 單例模式 (Singleton Pattern) - 確保只有一個導演
        if (Instance != null && Instance != this)
        {
            this.enabled = false; // 防止多餘實例執行邏輯
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerActions = new InputSystem_Actions();
        playerActions.Player.Enable();
        playerActions.Player.UnlockCursor.performed += OnTogglePauseInput;

        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        Application.targetFrameRate = targetFrameRate;
        Time.timeScale = 1f;
    }

    private void OnDisable()
    {
        if (Instance == this && playerActions != null)
        {
            playerActions.Player.Disable(); // 💀 只有真正的 Instance 才能關閉電源
        }
    }

    private void OnTogglePauseInput(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
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
        if (pauseMenuUI == null) return;

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
        Debug.Log("Game Director: Restarting Level...");
        Time.timeScale = 1f; // 確保時間流動

        // 直接載入場景即可，TeamManager 會自己處理後續
        int currentIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentIndex);
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

    public void OnClickSave()
    {
        // 1. 執行真實存檔
        SaveSystem ss = FindFirstObjectByType<SaveSystem>();
        if (ss != null) ss.SaveGame();

        // 2. 執行視覺回饋
        if (UIManager.Instance != null)
        {
            UIManager.Instance.StopAllCoroutines();
            StartCoroutine(UIManager.Instance.ShowSaveNotification());
        }
    }

    public void OnClickLoad()
    {
        SaveSystem ss = FindFirstObjectByType<SaveSystem>();
        if (ss != null)
        {
            Debug.Log("Game Director: Loading Level...");
            ss.LoadGame();

            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            if (pauseMenuUI) pauseMenuUI.SetActive(false);

            CurrentState = GameState.Playing;
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (CamControl.Current != null) CamControl.Current.IsInputPaused = false;
            if (cameraScript != null) cameraScript.IsInputPaused = false;

            IsPaused = false;
        }
    }
}