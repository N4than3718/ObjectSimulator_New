using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("過渡效果")]
    [Tooltip("請拖入一個覆蓋全螢幕的黑色 Panel 的 CanvasGroup")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    private void Awake()
    {
        // --- 1. 絕對單例模式 (跨場景存活) ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 如果已經有一個經理了，新來的自殺
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 【關鍵】切換場景時，不要銷毀我
    }

    private void Start()
    {
        // 遊戲開始時，如果有設定 CanvasGroup，執行淡入 (變透明)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = false; // 允許點擊
            StartCoroutine(FadeRoutine(0f)); // 1 -> 0 (淡入)
        }
    }

    public void ReloadCurrentScene()
    {
        if (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.9f) return;

        string currentScene = SceneManager.GetActiveScene().name;
        StartCoroutine(LoadSceneRoutine(currentScene));
    }

    public void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        // 檢查是否還有下一關
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            StartCoroutine(LoadSceneRoutine(nextIndex));
        }
        else
        {
            Debug.LogWarning("已經是最後一關了！回到主選單 (index 0)");
            StartCoroutine(LoadSceneRoutine(0)); // 或回到主選單
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(LoadSceneRoutine(sceneIndex));
    }

    // 支援字串名稱載入
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return StartCoroutine(TransitionOut()); // 變黑

        // 💀 不要在這裡找 Spectator 或 TeamManager，因為它們即將被毀掉
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        // 💀 等待一幀確保新場景的 Awake/Start 全部跑完
        yield return new WaitForEndOfFrame();

        yield return StartCoroutine(TransitionIn()); // 變亮
    }

    // 支援 Index 載入 (比較快)
    private IEnumerator LoadSceneRoutine(int sceneIndex)
    {
        yield return StartCoroutine(TransitionOut()); // 變黑

        // 💀 不要在這裡找 Spectator 或 TeamManager，因為它們即將被毀掉
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneIndex);
        while (!op.isDone) yield return null;

        // 💀 等待一幀確保新場景的 Awake/Start 全部跑完
        yield return new WaitForEndOfFrame();

        yield return StartCoroutine(TransitionIn()); // 變亮
    }

    // --- 過渡動畫 ---

    private IEnumerator TransitionOut() // 變黑 (Loading 開始)
    {
        // 鎖定時間，避免轉場時遊戲還在跑
        Time.timeScale = 1f; // 確保動畫能播

        if (GameDirector.Instance != null && GameDirector.Instance.playerActions != null)
        {
            GameDirector.Instance.playerActions.Player.Disable();
            Debug.Log("TransitionOut: Input System Disabled.");
        }

        var allRbs = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        foreach (var rb in allRbs)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // 鎖住主角操作 (如果有多主角系統)
        if (PlayerMovement.Current != null) PlayerMovement.Current.enabled = false;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true; // 阻擋滑鼠點擊
            yield return StartCoroutine(FadeRoutine(1f));
        }
    }

    private IEnumerator TransitionIn() // 變透明 (Loading 結束)
    {
        // 確保時間恢復流動
        Time.timeScale = 1f;

        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeRoutine(0f));
            fadeCanvasGroup.blocksRaycasts = false;
        }

        // 💀 [新增] 畫面完全變亮後，才准許輸入系統重新啟用
        if (GameDirector.Instance != null && GameDirector.Instance.playerActions != null)
        {
            GameDirector.Instance.playerActions.Player.Enable();
            Debug.Log("TransitionIn: Input System Re-enabled.");
        }
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = fadeCanvasGroup.alpha;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime; // 使用 unscaled 確保不受 TimeScale 影響
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = targetAlpha;
    }
}