using UnityEngine;
using System.Collections;

public class ToiletTeleporter : MonoBehaviour
{
    [Header("傳送設定")]
    [Tooltip("要把玩家沖到哪裡？")]
    [SerializeField] private Transform destination;
    [Tooltip("沖水動畫要播幾秒？")]
    [SerializeField] private float flushDelay = 1.5f;

    [Header("漩渦動畫設定")]
    [Tooltip("旋轉速度 (數字越大轉越快)")]
    [SerializeField] private float spinSpeed = 720f;
    [Tooltip("要下沉多深？ (微觀世界建議設小一點，例如 0.2)")]
    [SerializeField] private float sinkDepth = 0.2f;

    [Header("沉浸感")]
    [SerializeField] private AudioClip flushSound;
    [SerializeField] private ParticleSystem waterSplash;

    private bool isFlushing = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isFlushing)
        {
            if (destination != null)
            {
                StartCoroutine(FlushRoutine(other.gameObject));
            }
        }
    }

    private IEnumerator FlushRoutine(GameObject player)
    {
        isFlushing = true;

        // --- 第一階段：封印玩家的控制權與物理 ---
        // 取得玩家身上的移動腳本與剛體，並暫時關閉，避免跟我們的下沉動畫打架
        PlayerMovement movementScript = player.GetComponent<PlayerMovement>();
        Rigidbody rb = player.GetComponent<Rigidbody>();
        Collider[] allColliders = player.GetComponentsInChildren<Collider>();

        if (movementScript != null) movementScript.enabled = false;
        if (rb != null)
        {
            rb.isKinematic = true; // 關閉重力與物理推擠
            rb.linearVelocity = Vector3.zero; // 煞車
        }
        foreach (Collider col in allColliders)
        {
            col.enabled = false;
        }

        // 播放音效與水花
        if (flushSound != null) AudioSource.PlayClipAtPoint(flushSound, transform.position);
        if (waterSplash != null) waterSplash.Play();

        // --- 第二階段：漩渦動畫 (邊轉邊沉) ---
        float elapsedTime = 0f;
        Vector3 startPosition = player.transform.position;
        // 計算目標下沉位置 (往下 sinkDepth 的距離)
        Vector3 targetPosition = startPosition + Vector3.down * sinkDepth;

        // 💀 核心魔法：每一幀不斷更新位置與旋轉，直到時間結束
        while (elapsedTime < flushDelay)
        {
            elapsedTime += Time.deltaTime;
            float percent = elapsedTime / flushDelay; // 0 到 1 的進度條

            // 1. 平滑下沉 (Lerp)
            player.transform.position = Vector3.Lerp(startPosition, targetPosition, percent);

            // 2. 瘋狂旋轉 (沿著 Y 軸轉)
            player.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            // 等待下一個影格再來執行迴圈
            yield return null;
        }

        // --- 第三階段：從目的地噴出來 ---
        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        // 💥 解除封印：把控制權與物理還給玩家！
        if (movementScript != null) movementScript.enabled = true;
        if (rb != null) rb.isKinematic = false;
        foreach (Collider col in allColliders)
        {
            col.enabled = true;
        }

        isFlushing = false;
        Debug.Log("[Toilet] 沖水傳送完畢！");
    }
}