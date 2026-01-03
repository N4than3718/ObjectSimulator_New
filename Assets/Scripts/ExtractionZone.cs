using UnityEngine;

public class ExtractionZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 檢查進入的是不是玩家 (確保你的物品都有 "Player" tag)
        if (other.CompareTag("Player"))
        {
            Debug.Log("🎉 通關成功!");

            // 停止計時
            if (LevelTimer.Instance != null)
            {
                LevelTimer.Instance.StopTimer();
                float finalTime = LevelTimer.Instance.CurrentTime;

                // 💾 儲存進度與時間
                if (DataManager.Instance != null)
                {
                    DataManager.Instance.SaveLevelProgress(2); // 解鎖下一關
                    DataManager.Instance.SaveBestTime(1, finalTime); // 存入最佳時間
                }
            }

            // 呼叫導演喊卡
            GameDirector.Instance.TriggerVictory();
        }
    }
}