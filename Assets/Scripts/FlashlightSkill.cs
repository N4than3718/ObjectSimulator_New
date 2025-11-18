using UnityEngine;

public class FlashlightSkill : BaseSkill
{
    [Header("手電筒設定")]
    [SerializeField] private Light spotlight; // 拖曳聚光燈元件
    [SerializeField] private AudioClip clickSound; // 開關音效
    [SerializeField] private AudioSource audioSource;

    // 覆寫 Activate 方法，定義具體行為
    protected override void Activate()
    {
        if (spotlight != null)
        {
            // 切換開關
            spotlight.enabled = !spotlight.enabled;
            Debug.Log($"手電筒已 {(spotlight.enabled ? "開啟" : "關閉")}");

            // 播放音效
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }

            // 這裡發出一個小小的聲音訊號給 AI (開關聲)
            StealthManager.MakeNoise(transform.position, 3f, 2f);
        }
        else
        {
            Debug.LogWarning("FlashlightSkill: 缺少 Light 元件!");
        }
    }

    // 覆寫初始化，確保一開始的狀態
    private void Start()
    {
        if (spotlight == null) spotlight = GetComponentInChildren<Light>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }
}