using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum GameState { Playing, Paused, GameOver, Victory }

    [Header("Game State")]
    public GameState CurrentState;

    [Header("UI References")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    [Tooltip("主選單的場景名稱 (破關後回去用)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("系統設定")]
    public int targetFrameRate = 60;
    public bool useVSync = false;

    private void Awake()
    {
        // 單例模式 (Singleton Pattern) - 確保只有一個導演
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        Application.targetFrameRate = targetFrameRate;
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
}