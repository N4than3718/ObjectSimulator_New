using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // 建議加上，確保有物理屬性
public class ImpactSoundEmitter : MonoBehaviour
{
    [Header("碰撞設定")]
    [SerializeField] private float impactThreshold = 2.0f; // 撞擊力道閾值
    [SerializeField] private float soundCooldown = 0.2f;
    private float lastSoundTime = 0f;

    // ▼▼▼ [新增] Debug 可視化變數 ▼▼▼
    [Header("Debug 可視化")]
    [SerializeField]
    [Tooltip("是否在 Scene 視窗顯示噪音範圍")]
    private bool showDebugGizmos = true;

    // 用來記錄最後一次發出的聲音資訊
    private float _lastNoiseTime = -10f;
    private float _lastNoiseRadius;
    private Vector3 _lastNoisePos;

    private ObjectStats stats;
    private Rigidbody rb;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void Start()
    {
        stats = GetComponent<ObjectStats>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < lastSoundTime + soundCooldown) return;

        // 根據相對速度計算撞擊力
        float impactForce = collision.relativeVelocity.magnitude;

        // 如果有 ObjectStats，用它的重量來加權撞擊力 (重物聲音更大)
        float weightFactor = (stats != null) ? stats.weight : 1.0f;
        float materialFactor = (stats != null) ? stats.noiseMultiplier : 1.0f;

        // 實際判定閾值 (輕的東西需要撞更大力才會有聲音)
        float threshold = impactThreshold / Mathf.Max(weightFactor, 0.5f);

        if (impactForce > impactThreshold)
        {
            float range = impactForce * weightFactor * materialFactor;
            float intensity = range * 2f; // 撞越大力越警戒

            // 限制最大範圍，避免 Bug 導致全圖警戒
            range = Mathf.Clamp(range, 0, 30f);

            // 發出聲音
            StealthManager.MakeNoise(gameObject, transform.position, range, intensity);

            lastSoundTime = Time.time;

            // ▼▼▼ [新增] 紀錄 Debug 資訊 ▼▼▼
            if (showDebugGizmos)
            {
                _lastNoiseTime = Time.time;     // 紀錄發生時間
                _lastNoiseRadius = range;       // 紀錄計算出的範圍
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }
    }

    // ▼▼▼ [新增] 繪製 Gizmos (只在 Scene 視窗可見) ▼▼▼
    void OnDrawGizmos()
    {
        if (showDebugGizmos && Application.isPlaying)
        {
            if (Time.time - _lastNoiseTime < 1.0f)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Gizmos.DrawWireSphere(transform.position, _lastNoiseRadius);
            }
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
}