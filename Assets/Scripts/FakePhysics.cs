using UnityEngine;

public class FakePhysics : MonoBehaviour
{
    [Header("設定")]
    public float openSpeed = 5.0f;     // 開門速度
    public float maxAngle = 90f;       // 最大開門角度
    public float minAngle = -90f;      // 最小開門角度
    public bool autoClose = true;      // 自動關門

    [Header("方向除錯")]
    [Tooltip("如果門開的方向永遠相反，勾選這個")]
    public bool reverseDirection = false;

    [Tooltip("勾選後，場景會出現一條紅線，代表門的『正面』方向")]
    public bool showDebugLine = true;

    private float currentAngle = 0f;
    private float targetAngle = 0f;

    // 🔥 改進點 1：使用計數器，而不是 true/false
    // 這樣可以處理 "玩家和 NPC 同時在門口" 的情況
    private int peopleInZone = 0;

    void Update()
    {
        // 1. 平滑旋轉
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * openSpeed);
        transform.localRotation = Quaternion.Euler(0, currentAngle, 0);

        // 2. 自動關門邏輯
        // 只有當 "沒有人" (peopleInZone <= 0) 在門口時，才關門
        if (autoClose && peopleInZone <= 0)
        {
            targetAngle = Mathf.Lerp(targetAngle, 0, Time.deltaTime * 2.0f);

            // 保險機制：修正計數器可能變成負數的 Bug
            if (peopleInZone < 0) peopleInZone = 0;
        }
    }

    // 🔥 為了方便管理，我們寫一個函式來判斷 "誰有資格開門"
    bool CanOpenDoor(Collider other)
    {
        // 只要對方的 Tag 是 "Player" 或者 "NPC"，都回傳 true
        // 如果你的 NPC Tag 叫 "Enemy" 或其他名字，請加在後面
        return other.CompareTag("Player") || other.CompareTag("NPC");
    }

    // 當有人進入感應區
    void OnTriggerEnter(Collider other)
    {
        if (CanOpenDoor(other))
        {
            peopleInZone++; // 人數 +1
        }
    }

    // 當有人離開感應區
    void OnTriggerExit(Collider other)
    {
        if (CanOpenDoor(other))
        {
            peopleInZone--; // 人數 -1
        }
    }

    // 當人在感應區內移動時 (計算開門方向)
    void OnTriggerStay(Collider other)
    {
        if (CanOpenDoor(other))
        {
            // 計算開門方向
            Vector3 localPos = transform.InverseTransformPoint(other.transform.position);

            bool isInFront = localPos.z > 0;

            // 如果勾選了反轉，就把判斷結果顛倒
            if (reverseDirection) isInFront = !isInFront;

            if (isInFront)
            {
                // 玩家在正面，門往負向開 (遠離玩家)
                targetAngle = minAngle;
            }
            else
            {
                // 玩家在背面，門往正向開
                targetAngle = maxAngle;
            }
        }
    }

    // 🎨 畫出輔助線 (只有在 Scene視窗 看得見)
    void OnDrawGizmos()
    {
        if (showDebugLine)
        {
            Gizmos.color = Color.red;
            // 畫一條線表示 "Z軸正前方"
            Vector3 direction = reverseDirection ? -transform.forward : transform.forward;
            Gizmos.DrawRay(transform.position, direction * 2.0f);
            Gizmos.DrawSphere(transform.position + direction * 2.0f, 0.1f);
        }
    }
}
