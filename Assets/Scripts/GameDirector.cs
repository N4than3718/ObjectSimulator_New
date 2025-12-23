using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum GameState { Playing, Paused, GameOver, Victory }

    [Header("Game State")]
    public GameState CurrentState;

    [Header("UI References (Drag & Drop later)")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    [Header("幀率設定")]
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

        // 顯示勝利畫面
        if (victoryPanel) victoryPanel.SetActive(true);

        // 選擇性：暫停遊戲或進入慢動作
        // Time.timeScale = 0f; 
    }

    public void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.GameOver;
        Debug.Log("Game Director: Cut! Bad take. (Game Over)");

        // 顯示失敗畫面
        if (gameOverPanel) gameOverPanel.SetActive(true);

        // 暫停遊戲，避免玩家被抓後還能亂跑
        Time.timeScale = 0f;
    }

    // 給 UI 按鈕呼叫的
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}