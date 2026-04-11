using UnityEngine;
using UnityEngine.SceneManagement; // 💀 新增這行：用來讀取場景與關卡資訊！

public class ExtractionZone : MonoBehaviour
{
    [Header("破關設定")]
    [Tooltip("如果填 0，程式會自動抓取當前關卡編號。如果想手動強制解鎖特定關卡，請填入數字。")]
    [SerializeField] private int customNextLevel = 0;

    [Header("音效設定 (Blingbling✨)")]
    [Tooltip("用來播放瞬間過關音效的喇叭")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("你千辛萬苦找來的 Blingbling 音檔")]
    [SerializeField] private AudioClip successSound;
    [Tooltip("(選填) 撤離點原本持續發出聲響的喇叭，過關時會把它關掉")]
    [SerializeField] private AudioSource idleBlingSource;

    private void OnTriggerEnter(Collider other)
    {
        // 1. 檢查進入的是不是玩家
        if (other.GetComponentInParent<PlayerMovement>() != null)
        {
            Debug.Log("[撤離點] 玩家成功抵達！準備結算...");

            // ✨ 音效魔法 1：播放通關音效！
            if (audioSource != null && successSound != null)
            {
                audioSource.PlayOneShot(successSound);
            }

            // ✨ 音效魔法 2：(選填) 關閉持續的引導聲，讓結算畫面更乾淨
            if (idleBlingSource != null && idleBlingSource.isPlaying)
            {
                idleBlingSource.Stop();
            }

            // 💀 防呆機制 1：檢查 DataManager 是否在片場
            if (DataManager.Instance != null)
            {
                // 🎈 全自動關卡讀取魔法
                int levelToSave = customNextLevel;

                if (levelToSave == 0)
                {
                    // 抓取目前的場景編號 (Build Index)，並 +1 作為下一關解鎖目標
                    // 例如目前是 Level 2 (假設 Index 是 2)，過關就解鎖 Level 3
                    levelToSave = SceneManager.GetActiveScene().buildIndex + 1;
                }

                DataManager.Instance.SaveLevelProgress(levelToSave);
                Debug.Log($"[撤離點] 進度儲存成功！已解鎖關卡進度：{levelToSave}");
            }
            else
            {
                Debug.LogError("🚨 [系統警告] 找不到 DataManager！可能因為你是直接從本關按下Play，沒有經過主選單初始化。");
            }

            // 💀 防呆機制 2：檢查 GameDirector 是否在片場
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.TriggerVictory();
            }
            else
            {
                Debug.LogError("🚨 [系統警告] 找不到 GameDirector！導演不在片場，無法播放勝利畫面！");
            }
        }
    }
}