using UnityEngine;

public class FakePhysics : MonoBehaviour, IInteractable
{
    // --- 列舉定義 ---
    public enum MotionType { Rotate, Slide }
    public enum DoorType { Automatic, Manual }

    [Header("模式設定")]
    public MotionType motionType = MotionType.Rotate; // 是轉的還是滑的？
    public DoorType doorType = DoorType.Manual;       // 是手動還是自動？

    [Header("核心引用")]
    public Transform doorVisuals;    // 會動的模型 (兒子)
    public Transform interactionPoint; // 感應中心點 (空物件)

    [Header("通用參數")]
    public float speed = 5.0f;       // 動畫速度
    public bool isLocked = false;    // 鎖定狀態
    public bool autoClose = true;    // 自動關閉 (手動模式通常設 false)

    [Header("自動門專用設定")]
    [Tooltip("自動門：距離中心點多近才會觸發？")]
    public float activationDistance = 2.5f;
    [Tooltip("自動門：勾選後，門永遠往「遠離玩家」的方向開")]
    public bool openAwayFromPlayer = true;

    [Header("旋轉模式設定 (Rotate)")]
    public float openAngle = 90f;    // 開門角度

    [Header("滑動模式設定 (Slide)")]
    public Vector3 slideDirection = Vector3.back; // 滑動方向 (Local)
    public float slideDistance = 0.5f;            // 拉出距離

    [Header("音效 (選填)")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    // --- 內部狀態變數 ---
    private float currentValue = 0f; // 0 = 關, 1 = 開
    private float targetValue = 0f;  // 目標值 (0 或 1，或是 -1 代表反向開)

    private Quaternion initialRotation; // 初始旋轉
    private Vector3 initialPosition;    // 初始位置

    private bool isOpen = false;     // 手動模式的開關狀態
    private int peopleInZone = 0;    // 觸發區人數計數器

    void Start()
    {
        if (doorVisuals == null) doorVisuals = transform.GetChild(0);
        if (interactionPoint == null) interactionPoint = transform;

        // 🔥 功能回歸：自動歸零校正
        initialRotation = Quaternion.identity;
        initialPosition = doorVisuals.localPosition;
    }

    // --- IInteractable 介面實作 (給手動模式用) ---
    public void Interact()
    {
        if (doorType == DoorType.Automatic) return; // 自動門不給按

        if (isLocked)
        {
            PlaySound(lockedSound);
            Debug.Log("🔒 鎖住了！");
            return;
        }

        // 切換狀態
        isOpen = !isOpen;
        targetValue = isOpen ? 1f : 0f; // 手動模式只會在 0 和 1 之間切換

        PlaySound(isOpen ? openSound : closeSound);
    }

    public string GetInteractionPrompt()
    {
        if (isLocked) return "鎖住了";
        return isOpen ? "關閉" : "開啟";
    }

    public void UnlockDoor()
    {
        isLocked = false;
        PlaySound(openSound);
    }

    void Update()
    {
        // 1. 自動關門邏輯 (適用於自動門，或是你想讓手動櫃子也自動關)
        if (autoClose && peopleInZone <= 0 && !isOpen)
        {
            targetValue = 0f;
            if (peopleInZone < 0) peopleInZone = 0;
        }

        // 2. 平滑插值運算 (Lerp)
        // 使用 MoveTowards 或 Lerp 都可以，這裡用 Lerp 比較平滑
        currentValue = Mathf.Lerp(currentValue, targetValue, Time.deltaTime * speed);

        // 3. 應用變形 (Transform)
        ApplyMotion();
    }

    void ApplyMotion()
    {
        if (motionType == MotionType.Rotate)
        {
            // 🔥 旋轉邏輯
            // targetValue 可能是 1 (正開) 或 -1 (反開)
            float angle = currentValue * openAngle;
            doorVisuals.localRotation = initialRotation * Quaternion.Euler(0, angle, 0);
        }
        else if (motionType == MotionType.Slide)
        {
            // 🔥 滑動邏輯
            // 取絕對值是因為抽屜通常只有一個方向拉出來，不支援「往後桶穿」
            // 當然如果你想做雙向滑門，可以拿掉 Abs
            float slideFactor = Mathf.Abs(currentValue);
            Vector3 offset = slideDirection.normalized * (slideDistance * slideFactor);
            doorVisuals.localPosition = initialPosition + offset;
        }
    }

    // --- 自動感應核心 (OnTriggerStay) ---
    // 🔥 這是原本「功能不見」的地方，現在加回來了
    void OnTriggerStay(Collider other)
    {
        // 只有自動門需要跑這裡的邏輯
        if (doorType == DoorType.Manual) return;
        if (isLocked) return;

        if (CanOpen(other))
        {
            // 1. 🔥 功能回歸：距離檢測 (防誤觸)
            float dist = Vector3.Distance(interactionPoint.position, other.transform.position);
            bool isClosed = Mathf.Abs(currentValue) < 0.05f;

            // 如果門關著，且距離太遠，就不開
            if (isClosed && dist > activationDistance) return;

            // 2. 🔥 功能回歸：方向鎖定 (防抖動)
            // 如果門已經在開了 (currentValue 離開 0 了)，就鎖定方向，不再重新計算
            // 除非門快關上了 ( < 0.1f ) 才允許改變主意
            if (Mathf.Abs(targetValue) > 0.1f) return;

            // 3. 計算開啟方向
            float directionMultiplier = 1f;

            // 只有旋轉門才需要判斷前後 (抽屜通常只有一個方向)
            if (motionType == MotionType.Rotate && openAwayFromPlayer)
            {
                // 🔥 功能回歸：向量內積判斷前後
                // 使用 transform.forward (門框的朝向) 最準
                Vector3 directionToPlayer = other.transform.position - transform.position;
                float dot = Vector3.Dot(transform.forward, directionToPlayer);

                // 如果人在前面 (dot > 0)，門就要往負向開 (-1)，反之亦然
                // 這裡的邏輯取決於你的門軸設定，如果開反了就去掉負號
                directionMultiplier = (dot > 0) ? 1f : -1f;
            }

            // 設定目標 (1 或 -1)
            targetValue = 1f * directionMultiplier;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (CanOpen(other)) peopleInZone++;
    }

    void OnTriggerExit(Collider other)
    {
        if (CanOpen(other)) peopleInZone--;
    }

    bool CanOpen(Collider other)
    {
        return other.CompareTag("Player") || other.CompareTag("NPC");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmos()
    {
        // 畫出感應中心與距離
        Gizmos.color = Color.yellow;
        Vector3 center = interactionPoint != null ? interactionPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, activationDistance); // 自動感應圈

        // 畫出滑動路徑預覽
        if (motionType == MotionType.Slide && doorVisuals != null)
        {
            Gizmos.color = Color.green;
            Vector3 start = doorVisuals.position;
            Vector3 dir = transform.TransformDirection(slideDirection);
            Gizmos.DrawRay(start, dir * slideDistance);
            Gizmos.DrawWireCube(start + dir * slideDistance, Vector3.one * 0.1f);
        }

        // 畫出旋轉門的正面方向 (Debug用)
        if (motionType == MotionType.Rotate)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }
}