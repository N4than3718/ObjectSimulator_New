using System.Collections;
using UnityEditor.Overlays;
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
    [SerializeField] private CanvasGroup gameOverPanel;
    [SerializeField] private CanvasGroup victoryPanel;
    [SerializeField] private CanvasGroup menuPanel;
    [Tooltip("把所有選單裡的 Load 按鈕都拖進來")]
    [SerializeField] private UnityEngine.UI.Button[] loadButton;

    [Header("系統設定")]
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private bool useVSync = false;

    [SerializeField] private SpectatorController cameraScript;
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

        SetPanelActive(gameOverPanel, false);
        SetPanelActive(victoryPanel, false);
        SetPanelActive(menuPanel, false);
    }

    private void OnDisable()
    {
        if (Instance == this && playerActions != null)
        {
            playerActions.Player.Disable(); // 💀 只有真正的 Instance 才能關閉電源
        }
    }

    private void SetPanelActive(CanvasGroup cg, bool isActive)
    {
        if (cg == null) return;

        cg.alpha = isActive ? 1f : 0f;          // 控制透明度
        cg.interactable = isActive;             // 控制是否可點擊
        cg.blocksRaycasts = isActive;           // 控制是否攔截滑鼠

        if (isActive)
        {
            // 💀 強制播放進場動畫，解決物件原本隱藏導致動畫不跑的問題
            Animator anim = cg.GetComponent<Animator>();
            if (anim != null) anim.Play("In", 0, 0f);
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
        SetPanelActive(menuPanel, true);
        CheckSaveFile();

        // 解鎖滑鼠
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 💀 Coder: 暫停所有攝影機輸入
        if (CamControl.Current != null) CamControl.Current.IsInputPaused = true;
        if (cameraScript != null) cameraScript.IsInputPaused = true;
    }

    public void Resume()
    {
        if (menuPanel == null) return;

        IsPaused = false;
        Time.timeScale = 1f;
        SetPanelActive(menuPanel, false);

        // 鎖回滑鼠
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CamControl.Current != null) CamControl.Current.IsInputPaused = false;
        if (cameraScript != null) cameraScript.IsInputPaused = false;
    }

    public void CheckSaveFile()
    {
        string path = Application.persistentDataPath + "/savefile.json";

        if (loadButton != null)
        {
            // 💀 如果檔案不存在，按鈕就不可點擊，並變灰
            foreach (var btn in loadButton)
            {
                if (btn != null)
                {
                    btn.interactable = System.IO.File.Exists(path);
                }

                CanvasGroup cg = btn.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = btn.gameObject.AddComponent<CanvasGroup>();
                }

                // 直接調整 CanvasGroup 的整體透明度 (1 = 完全不透明, 0.4 = 半透明反灰感)
                cg.alpha = System.IO.File.Exists(path) ? 1f : 0.4f;
            }
        }
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
        if (gameOverPanel) SetPanelActive(menuPanel, false);
        if (victoryPanel) SetPanelActive(menuPanel, false);

        Debug.Log("Game Director: Action! 🎬");
    }

public void TriggerVictory()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.Victory;
        Debug.Log("Game Director: Cut! It's a wrap. (Victory)");

        // 💀 新增這段：在彈出面板前，命令 UI 更新數據
        if (victoryPanel != null)
        {
            VictoryUI uiLogic = victoryPanel.GetComponent<VictoryUI>();
            if (uiLogic != null)
            {
                uiLogic.UpdateVictoryData();
            }
        }

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
    private void EndGameLogic(CanvasGroup panelToShow)
    {
        // 1. 顯示對應 UI
        if (panelToShow) SetPanelActive(panelToShow, true);
        CheckSaveFile();

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

        Resume();
    }

    public void OnClickLoad()
    {
        SaveSystem ss = FindFirstObjectByType<SaveSystem>();
        if (ss != null)
        {
            Debug.Log("Game Director: Loading Level...");
            ss.LoadGame();

            if (gameOverPanel) SetPanelActive(menuPanel, false);
            if (victoryPanel) SetPanelActive(menuPanel, false);
            if (menuPanel) SetPanelActive(menuPanel, false);

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