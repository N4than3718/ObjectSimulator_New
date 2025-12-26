using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// 繼承 MonoBehaviour，這樣我們可以把技能直接掛在物件上設定參數
public abstract class BaseSkill : MonoBehaviour
{
    [Header("技能基本設定")]
    [SerializeField] protected string skillName = "未命名技能";
    [SerializeField] protected float cooldownTime = 2.0f; // 冷卻時間
    [SerializeField] protected Sprite skillIcon; // UI 圖示 (之後用)

    // 執行期間的狀態
    protected float cooldownTimer = 0f;
    protected bool isReady = true;

    // UI 事件 (之後可以用來更新冷卻條)
    public Sprite GetSkillIcon() => skillIcon;
    public float GetCooldownRatio() => isReady ? 0f : (cooldownTimer / cooldownTime);

    protected virtual void Update()
    {
        // 處理冷卻倒數
        if (!isReady)
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownTimer <= 0f)
            {
                cooldownTimer = 0f;
                isReady = true;
                OnSkillReady(); // 觸發冷卻完成的邏輯
            }
        }
    }

    public virtual void OnInput(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            TryActivate();
        }
    }

    /// <summary>
    /// 嘗試觸發技能 (由外部呼叫，例如 SkillManager)
    /// </summary>
    public bool TryActivate()
    {
        if (!this.enabled)
        {
            this.enabled = true;
            // Debug.Log($"{skillName} 已重新啟用 (Re-enabled by TryActivate)");
        }

        if (isReady)
        {
            Activate(); // 執行實際技能邏輯
            StartCooldown();
            return true;
        }
        else
        {
            Debug.Log($"{skillName} 冷卻中... 剩餘 {cooldownTimer:F1} 秒");
            // 這裡可以播放「無法使用」的音效
            return false;
        }
    }

    /// <summary>
    /// 開始計算冷卻
    /// </summary>
    protected void StartCooldown()
    {
        isReady = false;
        cooldownTimer = cooldownTime;
    }

    /// <summary>
    /// 技能冷卻完成時的回呼 (可供子類別覆寫)
    /// </summary>
    protected virtual void OnSkillReady() {  }

    // --- 必須由子類別實作的核心邏輯 ---
    protected abstract void Activate();
}