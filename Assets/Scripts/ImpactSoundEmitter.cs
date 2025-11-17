using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // 建議加上，確保有物理屬性
public class ImpactSoundEmitter : MonoBehaviour
{
    [Header("碰撞設定")]
    [SerializeField] private float impactThreshold = 2.0f; // 撞擊力道閾值
    [SerializeField] private float noiseRangeMultiplier = 2.0f; // 撞擊力轉聲音範圍的倍率

    // ▼▼▼ [新增] Debug 可視化變數 ▼▼▼
    [Header("Debug 可視化")]
    [SerializeField]
    [Tooltip("是否在 Scene 視窗顯示噪音範圍")]
    private bool showDebugGizmos = true;

    [SerializeField]
    private Color gizmoColor = new Color(1, 0, 0, 0.5f); // 紅色半透明

    [SerializeField]
    [Tooltip("視覺效果停留的時間 (秒)")]
    private float gizmoDuration = 1.0f;

    // 用來記錄最後一次發出的聲音資訊
    private float _lastNoiseTime = -10f;
    private float _lastNoiseRadius;
    private Vector3 _lastNoisePos;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void OnCollisionEnter(Collision collision)
    {
        // 根據相對速度計算撞擊力
        float impactForce = collision.relativeVelocity.magnitude;

        if (impactForce > impactThreshold)
        {
            float range = impactForce * noiseRangeMultiplier;
            float intensity = impactForce * 2f; // 撞越大力越警戒

            // 發出聲音
            StealthManager.MakeNoise(transform.position, range, intensity);
            Debug.Log($"{name} 撞擊發出噪音! 範圍: {range}");

            // ▼▼▼ [新增] 紀錄 Debug 資訊 ▼▼▼
            if (showDebugGizmos)
            {
                _lastNoiseTime = Time.time;     // 紀錄發生時間
                _lastNoiseRadius = range;       // 紀錄計算出的範圍
                _lastNoisePos = transform.position; // 紀錄位置
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }
    }

    // ▼▼▼ [新增] 繪製 Gizmos (只在 Scene 視窗可見) ▼▼▼
    void OnDrawGizmos()
    {
        // 只在開啟開關且在 Play 模式下繪製
        if (showDebugGizmos && Application.isPlaying)
        {
            float timeSinceLastNoise = Time.time - _lastNoiseTime;

            // 如果還在顯示時間內
            if (timeSinceLastNoise < gizmoDuration)
            {
                // 計算淡出透明度 (從 1 到 0)
                float alpha = 1.0f - (timeSinceLastNoise / gizmoDuration);

                // 設定顏色
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * alpha);

                // 畫出範圍圈
                Gizmos.DrawWireSphere(_lastNoisePos, _lastNoiseRadius);
                // 畫出中心點
                Gizmos.DrawSphere(_lastNoisePos, 0.1f);
            }
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
}