using UnityEngine;

// 繼承 IInteractable 支援互動
public class FakePhysics : MonoBehaviour, IInteractable
{
    // 🔥 新增：動作模式 (轉動 vs 滑動)
    public enum MotionType { Rotate, Slide }
    public enum DoorType { Automatic, Manual }

    [Header("核心設定")]
    public MotionType motionType = MotionType.Rotate; // 預設是旋轉
    public DoorType doorType = DoorType.Manual;       // 預設是手動

    [Header("必填！模型引用")]
    public Transform doorVisuals; // 門板 或 抽屜本身
    public Transform interactionPoint;

    [Header("通用參數")]
    public float speed = 5.0f;     // 開啟速度
    public bool isLocked = false;
    public bool autoClose = false;

    [Header("旋轉模式設定 (門)")]
    public float openAngle = 90f;   // 開門角度
    public bool reverseRotate = false; // 反向旋轉

    [Header("滑動模式設定 (抽屜)")]
    public Vector3 slideDirection = Vector3.back; // 抽屜往哪個方向開？(通常是 Z軸負向 back)
    public float slideDistance = 0.5f;            // 抽屜拉出來多長？

    [Header("音效")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    // --- 內部變數 ---
    private float currentValue = 0f; // 目前的進度 (0 = 關, 1 = 開)
    private float targetValue = 0f;  // 目標進度

    private Quaternion initialRotation; // 記錄旋轉初始值
    private Vector3 initialPosition;    // 記錄位置初始值

    private bool isOpen = false;
    private int peopleInZone = 0;

    void Start()
    {
        if (doorVisuals == null) doorVisuals = transform.GetChild(0);
        if (interactionPoint == null) interactionPoint = transform;

        // 記住初始狀態
        initialRotation = Quaternion.identity; // 假設子物件已經擺好在 0,0,0
        initialPosition = doorVisuals.localPosition; // 記住抽屜原本塞在裡面的位置
    }

    // 實作互動介面
    public void Interact()
    {
        if (doorType == DoorType.Automatic) return;

        if (isLocked)
        {
            PlaySound(lockedSound);
            Debug.Log("🔒 鎖住了！");
            return;
        }

        // 切換開關狀態
        isOpen = !isOpen;
        targetValue = isOpen ? 1f : 0f; // 1代表全開，0代表全關

        PlaySound(isOpen ? openSound : closeSound);
    }

    public string GetInteractionPrompt()
    {
        if (isLocked) return "鎖住了";
        return isOpen ? "關閉" : "開啟"; // 這裡也可以寫 "拉開" / "推回"
    }

    public void UnlockDoor()
    {
        isLocked = false;
        PlaySound(openSound);
    }

    void Update()
    {
        // 自動門邏輯
        if (doorType == DoorType.Automatic)
        {
            if (peopleInZone > 0 && !isLocked) targetValue = 1f;
            else targetValue = 0f;
        }

        // 平滑插值 (0 到 1 之間變化)
        currentValue = Mathf.Lerp(currentValue, targetValue, Time.deltaTime * speed);

        // 🔥 核心分歧：你是門還是抽屜？
        if (motionType == MotionType.Rotate)
        {
            // --- 旋轉邏輯 (原本的門) ---
            float angle = currentValue * openAngle;
            if (reverseRotate) angle = -angle;

            doorVisuals.localRotation = initialRotation * Quaternion.Euler(0, angle, 0);
        }
        else if (motionType == MotionType.Slide)
        {
            // --- 滑動邏輯 (抽屜) ---
            // 公式：現在位置 = 初始位置 + (方向 * (距離 * 進度0~1))
            Vector3 offset = slideDirection.normalized * (slideDistance * currentValue);
            doorVisuals.localPosition = initialPosition + offset;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    // Trigger 區塊 (保持不變，給自動門用的)
    void OnTriggerEnter(Collider other) { if (CheckAuto(other)) peopleInZone++; }
    void OnTriggerExit(Collider other) { if (CheckAuto(other)) peopleInZone--; }

    bool CheckAuto(Collider other)
    {
        if (doorType == DoorType.Manual) return false;
        return other.CompareTag("Player") || other.CompareTag("NPC");
    }

    void OnDrawGizmos()
    {
        // 畫出互動中心
        Gizmos.color = Color.yellow;
        Vector3 center = interactionPoint != null ? interactionPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, 0.2f); // 這裡只是示意互動點

        // 畫出抽屜滑動方向 (只有在 Slide 模式下)
        if (motionType == MotionType.Slide && doorVisuals != null)
        {
            Gizmos.color = Color.green;
            Vector3 start = doorVisuals.position;
            // 轉換方向到世界座標
            Vector3 dir = transform.TransformDirection(slideDirection);
            Gizmos.DrawRay(start, dir * slideDistance);
            Gizmos.DrawWireCube(start + dir * slideDistance, Vector3.one * 0.1f);
        }
    }
}