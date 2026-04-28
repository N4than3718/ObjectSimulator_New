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

    [Header("冷卻設定")]
    [Tooltip("傳送後，水箱需要幾秒才能補滿水？")]
    [SerializeField] private float cooldownTime = 5.0f; // 💀 5秒冷卻

    [Header("沉浸感")]
    [SerializeField] private AudioClip flushSound;
    [SerializeField] private ParticleSystem waterSplash;

    private bool isFlushing = false;
    private bool isOnCooldown = false; // 💀 追蹤是否在冷卻中

    private void OnTriggerEnter(Collider other)
    {
        if (destination = null) return;

        PlayerMovement playerScript = other.GetComponentInParent<PlayerMovement>();

        // 💀 關鍵修改：必須「不是沖水中」且「不是冷卻中」才能觸發！
        if (playerScript != null && !isFlushing && !isOnCooldown)
        {
            if (destination != null)
            {
                Debug.Log("[Toilet Debug] 身分確認！啟動沖水！");
                StartCoroutine(FlushRoutine(playerScript.gameObject));
            }
        }
        // 💀 加上冷卻中的 Debug 提示，方便你測試
        else if (playerScript != null && isOnCooldown)
        {
            Debug.Log("[Toilet Debug] 馬桶水箱還沒滿，請等幾秒再跳進來！");
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
        // 1. 瞬間移動座標與面向
        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        // 💀 關鍵黑魔法：強制物理引擎立刻更新座標！
        // 很多時候你改了 position，但物理引擎還停留在上一幀，導致你一開碰撞就掉下樓。
        // 這行程式碼會大喊：「喂！他已經在這裡了，馬上給我更新地板碰撞！」
        Physics.SyncTransforms();

        // 2. 清空殘留慣性
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. 依序解開物理封印
        foreach (Collider c in allColliders)
        {
            if (c != null) c.enabled = true;
        }
        if (rb != null) rb.isKinematic = false;
        if (movementScript != null) movementScript.enabled = true;

        isFlushing = false;
        Debug.Log("[Toilet] 沖水傳送完畢，這次沒有帶走整棟房子！");

        StartCoroutine(CooldownTimer());
    }

    private IEnumerator CooldownTimer()
    {
        isOnCooldown = true; // 鎖上馬桶

        yield return new WaitForSeconds(cooldownTime); // 死等 5 秒鐘

        isOnCooldown = false; // 5 秒後重新開放
        Debug.Log("[Toilet] 水箱補滿了！馬桶可以再次使用了！");
    }
}