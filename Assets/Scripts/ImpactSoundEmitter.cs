using UnityEngine;

// 2. 碰撞聲音觸發器 (ImpactSoundEmitter.cs)
// 同樣掛在可操控物件上。它依賴 ObjectStats 和 Rigidbody。
[RequireComponent(typeof(ObjectStats))]
[RequireComponent(typeof(Rigidbody))]
public class ImpactSoundEmitter : MonoBehaviour
{
    private ObjectStats stats;
    private Rigidbody rb;

    [Header("碰撞聲音閾值")]
    [SerializeField]
    [Tooltip("產生可聽見聲音所需的最小相對速度")]
    private float minImpactVelocity = 1.5f;

    [SerializeField]
    [Tooltip("基礎聲音乘數，用來將『力道』轉換為『聽覺半徑』")]
    private float baseSoundRadiusMultiplier = 0.5f;

    void Start()
    {
        stats = GetComponent<ObjectStats>();
        rb = GetComponent<Rigidbody>(); // Rigidbody 是計算物理所必需的
    }

    void OnCollisionEnter(Collision collision)
    {
        // 1. 獲取碰撞的相對速度
        float impactVelocity = collision.relativeVelocity.magnitude;

        // 2. 判斷 (Judgment) - 速度太慢就沒聲音
        if (impactVelocity < minImpactVelocity)
        {
            return;
        }

        // 3. 核心計算：噪音半徑
        // 噪音 = (速度 - 閾值) * 重量 * 材質 * 基礎乘數
        float calculatedNoiseRadius = (impactVelocity - minImpactVelocity) * stats.weight * stats.noiseMultiplier * baseSoundRadiusMultiplier;

        // 確保半徑大於 0
        if (calculatedNoiseRadius > 0.1f)
        {
            // 4. 廣播這個聲音事件
            // 我們不直接通知 AI，而是呼叫一個中央管理器
            // Debug.Log($"Impact Noise: {calculatedNoiseRadius}m radius at {transform.position}");
            StealthManager.ReportNoise(transform.position, calculatedNoiseRadius);
        }
    }
}