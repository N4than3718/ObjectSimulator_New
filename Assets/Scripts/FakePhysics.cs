using UnityEngine;

public class FakePhysics : MonoBehaviour
{
    [Header("設定")]
    public float openSpeed = 5.0f;     // 開門速度
    public float maxAngle = 90f;       // 最大開門角度
    public float minAngle = -90f;      // 最小開門角度
    public bool autoClose = true;      // 自動關門

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
            // 計算開門方向 (跟之前一樣)
            Vector3 directionToTarget = other.transform.position - transform.position;
            float dot = Vector3.Dot(transform.forward, directionToTarget.normalized);

            if (dot > 0)
                targetAngle = minAngle; // 往內
            else
                targetAngle = maxAngle; // 往外
        }
    }
}
