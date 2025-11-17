using UnityEngine;

public class ImpactSoundEmitter : MonoBehaviour
{
    [SerializeField] private float impactThreshold = 2.0f; // 撞擊力道閾值
    [SerializeField] private float noiseRangeMultiplier = 2.0f; // 撞擊力轉聲音範圍的倍率

    private void OnCollisionEnter(Collision collision)
    {
        // 根據相對速度計算撞擊力
        float impactForce = collision.relativeVelocity.magnitude;

        if (impactForce > impactThreshold)
        {
            float range = impactForce * noiseRangeMultiplier;
            float intensity = impactForce * 2f; // 撞越大力越警戒

            // 發出聲音
            NoiseManager.MakeNoise(transform.position, range, intensity);
            Debug.Log($"{name} 撞擊發出噪音! 範圍: {range}");
        }
    }
}