using UnityEngine;

public class FakePhysics : MonoBehaviour
{
    [Header("必填！請把門的模型拖進來")]
    public Transform doorVisuals; // 🔥 新增：這是我們要轉動的兒子 (門板)

    [Header("設定")]
    public float openSpeed = 5.0f;     // 開門速度
    public float maxAngle = 90f;       // 最大開門角度
    public float minAngle = -90f;      // 最小開門角度
    public bool autoClose = true;      // 自動關門

    [Header("感應設定")]
    [Tooltip("只有當玩家距離門小於這個數字時，門才會開")]
    public float activationDistance = 0.5f; // 🔥 新功能：感應距離

    [Header("方向除錯")]
    [Tooltip("如果門開的方向永遠相反，勾選這個")]
    public bool reverseDirection = false;

    [Tooltip("勾選後，場景會出現一條紅線，代表門的『正面』方向")]
    public bool showDebugLine = true;

    private float currentAngle = 0f;
    private float targetAngle = 0f;
    private Quaternion initialRotation; // 🔥 新增：用來記住門一開始的「關閉狀態」

    // 計數器
    private int peopleInZone = 0;

    void Start()
    {
        // 🔥 防呆：如果你忘記拉模型，我幫你抓第一個子物件
        if (doorVisuals == null)
            doorVisuals = transform.GetChild(0);

        // 🔥 關鍵修正：遊戲開始時，記住現在的旋轉角度當作「0度（關閉）」
        initialRotation = Quaternion.identity;
    }

    void Update()
    {
        Debug.Log($"目標: {doorVisuals.name} | 目前角度: {currentAngle} | Root旋轉: {transform.localEulerAngles.y}");

        // 2. 自動關門邏輯
        if (autoClose && peopleInZone <= 0)
        {
            // 🔥 優化：不需要在這裡 Lerp，直接設目標為 0，讓下面的主 Lerp 去跑動畫就好
            // 這樣關門手感會比較乾脆，不會拖泥帶水
            targetAngle = 0f;

            // 保險機制
            if (peopleInZone < 0) peopleInZone = 0;
        }

        // 1. 平滑旋轉計算 (插值)
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * openSpeed);

        // 🔥 關鍵修正：基於「初始角度」進行旋轉疊加
        // 這樣無論你在場景裡怎麼擺這個門，0度永遠等於你擺放時的樣子
        doorVisuals.localRotation = initialRotation * Quaternion.Euler(0, currentAngle, 0);
    }

    bool CanOpenDoor(Collider other)
    {
        return other.CompareTag("Player") || other.CompareTag("NPC");
    }

    void OnTriggerEnter(Collider other)
    {
        if (CanOpenDoor(other))
        {
            peopleInZone++;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (CanOpenDoor(other))
        {
            peopleInZone--;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (CanOpenDoor(other))
        {
            // --- 判斷門現在是不是關著的 ---
            bool isClosed = Mathf.Abs(currentAngle) < 5.0f;

            // --- 🔥 關鍵邏輯：距離判斷 ---
            float dist = Vector3.Distance(transform.position, other.transform.position);

            // 如果門是「關著」的，且玩家還「太遠」，就什麼都不做 (保持關閉)
            // 這就是為什麼你的 Collider 可以設很大，但門不會亂開的原因
            if (isClosed && dist > activationDistance) return;

            if (Mathf.Abs(targetAngle) > 0.1f) return;

            // 計算開門方向
            Vector3 directionToPlayer = other.transform.position - transform.position;

            // 直接用根物件的 forward 來算，超穩
            float dot = Vector3.Dot(transform.forward, directionToPlayer);

            bool isInFront = dot > 0;
            if (reverseDirection) isInFront = !isInFront;

            if (isInFront)
            {
                targetAngle = minAngle;
            }
            else
            {
                targetAngle = maxAngle;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (showDebugLine)
        {
            Gizmos.color = Color.red;
            // 讓 Gizmos 跟隨物體當前的旋轉顯示，方便除錯
            Vector3 direction = reverseDirection ? -transform.forward : transform.forward;
            Gizmos.DrawRay(transform.position, direction * 2.0f);
            Gizmos.DrawSphere(transform.position + direction * 2.0f, 0.1f);

            // 🔥 畫出感應距離的圈圈 (黃色)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationDistance);
        }
    }
}