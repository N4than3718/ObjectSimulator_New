using UnityEngine;

public class ExtractionZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 檢查進入的是不是玩家 (確保你的物品都有 "Player" tag)
        if (other.CompareTag("Player"))
        {
            GameDirector.Instance.SaveLevelProgress(1); // 假設這是第一關

            // 呼叫導演喊卡
            GameDirector.Instance.TriggerVictory();
        }
    }
}