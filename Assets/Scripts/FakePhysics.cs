using UnityEngine;

// 🔥 記得繼承 IInteractable
public class FakePhysics : MonoBehaviour, IInteractable
{
    public enum DoorType { Automatic, Manual }

    [Header("必填！請把門的模型拖進來")]
    public Transform doorVisuals;
    [Header("自訂感應中心")]
    public Transform interactionPoint;

    [Header("模式設定")]
    [Tooltip("Automatic = 靠近自動開 (自動門)\nManual = 按互動鍵才開 (櫃子/寶箱)")]
    public DoorType doorType = DoorType.Manual; // 🔥 新增：預設改成手動
    public bool isLocked = false;

    [Header("參數設定")]
    public float openSpeed = 5.0f;
    public float maxAngle = 90f;
    public bool autoClose = false; // 手動櫃子通常不自動關，除非你要做鬧鬼效果

    [Header("音效 (選填)")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    // 內部變數
    private float currentAngle = 0f;
    private float targetAngle = 0f;
    private Quaternion initialRotation;
    private int peopleInZone = 0;
    private bool isOpen = false; // 手動模式用的開關狀態

    void Start()
    {
        if (doorVisuals == null) doorVisuals = transform.GetChild(0);
        initialRotation = Quaternion.identity;
        if (interactionPoint == null) interactionPoint = transform;

        // 確保初始狀態正確
        targetAngle = 0f;
    }

    // 🔥 實作 IInteractable 的接口
    public void Interact()
    {
        // 1. 如果是自動門，就不給按 (或者你可以設計成按了鎖定)
        if (doorType == DoorType.Automatic) return;

        // 2. 檢查鎖定
        if (isLocked)
        {
            Debug.Log("🔒 門鎖著，打不開！");
            PlaySound(lockedSound);
            return;
        }

        // 3. 切換開關狀態
        isOpen = !isOpen;

        // 設定目標角度
        targetAngle = isOpen ? maxAngle : 0f;

        // 播放音效
        PlaySound(isOpen ? openSound : closeSound);
    }

    public string GetInteractionPrompt()
    {
        if (isLocked) return "鎖住了";
        return isOpen ? "關閉" : "開啟";
    }

    void Update()
    {
        // ----------------------------------------------------
        // 🔥 核心分歧點：根據模式決定 targetAngle 怎麼算
        // ----------------------------------------------------

        if (doorType == DoorType.Automatic)
        {
            // 舊邏輯：感應區有人就開
            if (peopleInZone > 0 && !isLocked)
            {
                // 這裡簡化了方向判斷，如果需要原本的雙向開門，請保留原本的 Dot Product 邏輯
                targetAngle = maxAngle;
            }
            else
            {
                targetAngle = 0f;
            }
        }
        else // DoorType.Manual
        {
            // 新邏輯：完全聽 isOpen 的話
            // targetAngle 已經在 Interact() 裡面設好了，這裡只要確保它不跑掉
        }

        // 平滑旋轉 (通用)
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * openSpeed);
        doorVisuals.localRotation = initialRotation * Quaternion.Euler(0, currentAngle, 0);
    }

    // 輔助函式：解鎖 (給鑰匙用)
    public void UnlockDoor()
    {
        isLocked = false;
        PlaySound(openSound); // 解鎖順便彈開一點感覺很爽
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    // --- Trigger 區塊 (只對自動門有效) ---
    // 為了避免手動櫃子被誤觸，我們加一個檢查
    bool CanAutoOpen(Collider other)
    {
        if (doorType == DoorType.Manual) return false; // 🔥 手動門忽略碰撞
        if (isLocked) return false;
        return other.CompareTag("Player") || other.CompareTag("NPC");
    }

    void OnTriggerEnter(Collider other) { if (CanAutoOpen(other)) peopleInZone++; }
    void OnTriggerExit(Collider other) { if (CanAutoOpen(other)) peopleInZone--; }
}