using UnityEngine;
using UnityEngine.SceneManagement; // 💀 新增這行：用來讀取場景與關卡資訊！

public class ExtractionZone : MonoBehaviour
{
    [Header("破關設定")]
    [Tooltip("如果填 0，程式會自動抓取當前關卡編號。如果想手動強制解鎖特定關卡，請填入數字。")]
    [SerializeField] private int customNextLevel = 0;

    private void OnTriggerEnter(Collider other)
    {
        // 1. 檢查進入的是不是玩家
        if (other.GetComponentInParent<PlayerMovement>() != null)
        {
            Debug.Log("[撤離點] 玩家成功抵達！準備結算...");

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