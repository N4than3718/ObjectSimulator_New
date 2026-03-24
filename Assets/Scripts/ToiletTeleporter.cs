using UnityEngine;
using System.Collections; // 💀 使用協程必須加這行

public class ToiletTeleporter : MonoBehaviour
{
    [Header("傳送設定")]
    [Tooltip("要把玩家沖到哪裡？ (把出口的空物件或另一個馬桶拖進來)")]
    [SerializeField] private Transform destination;

    [Tooltip("沖水要花幾秒鐘？")]
    [SerializeField] private float flushDelay = 1.0f;

    [Header("沉浸感 (選填)")]
    [SerializeField] private AudioClip flushSound;
    [SerializeField] private ParticleSystem waterSplash;

    private bool isFlushing = false;

    // 當有東西碰到馬桶的觸發區域時...
    private void OnTriggerEnter(Collider other)
    {
        // 1. 確認掉進來的是不是「玩家」，而且馬桶目前沒有在沖水
        if (other.CompareTag("Player") && !isFlushing)
        {
            if (destination != null)
            {
                // 2. 啟動沖水流程！
                StartCoroutine(FlushRoutine(other.gameObject));
            }
            else
            {
                Debug.LogWarning("[Toilet] 靠北！馬桶沒有設定出口，塞住了！");
            }
        }
    }

    // 💀 沖水協程 (可以中途等待的特殊函式)
    private IEnumerator FlushRoutine(GameObject player)
    {
        isFlushing = true;

        // --- 第一階段：掉進馬桶 ---
        Debug.Log("玩家掉進馬桶了！開始沖水！");

        if (flushSound != null)
            AudioSource.PlayClipAtPoint(flushSound, transform.position);

        if (waterSplash != null)
            waterSplash.Play();

        player.GetComponent<PlayerMovement>().enabled = false;

        // --- 第二階段：在管線裡流動 (等待) ---
        yield return new WaitForSeconds(flushDelay);

        // --- 第三階段：從目的地噴出來 (瞬間移動) ---

        // 真正執行瞬間移動
        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation; // 讓玩家看向出口的方向

        // 重新開啟控制器
        player.GetComponent<PlayerMovement>().enabled = true;

        Debug.Log("傳送完成！");
        isFlushing = false;
    }
}