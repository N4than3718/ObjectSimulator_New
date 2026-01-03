using UnityEngine;

public class KeySkill : BaseSkill
{
    [Header("解鎖技能設定")]
    [SerializeField] private float interactionRange = 3.0f; // 鑰匙能勾到的距離
    [SerializeField] private LayerMask doorLayer;           // 請設定為包含 "Default" 或門所在的 Layer

    [Header("特效 (選填)")]
    [SerializeField] private ParticleSystem unlockEffect;   // 解鎖時的特效 (例如火花)
    [SerializeField] private AudioClip unlockSound;         // 喀擦聲
    [SerializeField] private AudioSource audioSource;       // 發聲源

    // 實作 BaseSkill 要求的抽象方法
    protected override void Activate()
    {
        // 1. 決定射線起點 (從攝影機發射最準，如果沒有就從物件前方)
        Vector3 rayOrigin = transform.position;
        Vector3 rayDir = transform.forward;

        if (Camera.main != null)
        {
            rayOrigin = Camera.main.transform.position;
            rayDir = Camera.main.transform.forward;
        }

        // 2. 發射射線檢測
        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, interactionRange, doorLayer))
        {
            // 3. 嘗試取得門的腳本
            // GetComponentInParent 是為了防止打到門把或門框子物件
            FakePhysics door = hit.collider.GetComponentInParent<FakePhysics>();

            if (door != null)
            {
                if (door.isLocked)
                {
                    PerformUnlock(door, hit.point);
                }
                else
                {
                    Debug.Log("🔒 這扇門沒鎖，不需要浪費鑰匙。");
                    // 因為沒成功使用，我們可以把冷卻重置 (可選)
                    // isReady = true; 
                }
            }
        }
        else
        {
            Debug.Log("❌ 太遠了，或是沒對準門！");
        }
    }

    private void PerformUnlock(FakePhysics door, Vector3 hitPoint)
    {
        // A. 執行解鎖
        door.UnlockDoor();

        // B. 播放特效
        if (unlockEffect != null)
        {
            Instantiate(unlockEffect, hitPoint, Quaternion.identity);
        }

        if (unlockSound != null && audioSource != null)
        {
            // 使用 PlayOneShot 避免物件銷毀時聲音被切斷 (雖然物件銷毀還是會斷，建議用 AudioManger)
            // 這裡簡單處理：在銷毀前播一下，或者生成一個臨時聲音物件
            AudioSource.PlayClipAtPoint(unlockSound, transform.position);
        }

        Debug.Log($"🔑 {skillName} 發動成功！門已解鎖。");

        // C. 消耗鑰匙 (一次性道具)
        ConsumeKey();
    }

    private void ConsumeKey()
    {
        Debug.Log("👋 鑰匙任務完成，啟動銷毀程序...");

        // 1. 先確認 TeamManager 活著
        if (TeamManager.Instance != null)
        {
            // 2. 通知經紀人：我要退團了，請把鏡頭轉給別人
            // 這裡很重要！Manager 內部必須處理 "如果移除的是當前操控角色，要切換鏡頭"
            TeamManager.Instance.RemoveCharacterFromTeam(this.gameObject);
        }
        else
        {
            Debug.LogWarning("⚠️ 找不到 TeamManager！將直接強制銷毀。");
        }

        // 3. 卸載這把鑰匙身上的所有物理和控制，避免在銷毀前的一瞬間出錯
        // 關閉移動控制
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        // 關閉碰撞 (避免銷毀瞬間還被物理引擎運算)
        var coll = GetComponent<Collider>();
        if (coll != null) coll.enabled = false;

        // 4. 🔥 延遲銷毀 (关键！)
        // 給 Unity 一點時間 (1秒) 去處理 TeamManager 的鏡頭切換和 List 更新
        // 這樣可以避免 Null Reference 錯誤導致銷毀失敗
        Destroy(this.gameObject, 1.0f);
    }

    // 畫出偵測範圍 (Debug用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * interactionRange);
    }
}