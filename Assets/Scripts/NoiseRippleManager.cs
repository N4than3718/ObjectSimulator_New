using UnityEngine;

public class NoiseRippleManager : MonoBehaviour
{
    [Header("資源設定")]
    [SerializeField] private SoundRipple ripplePrefab; // 拖曳剛才做的 Ripple Prefab
    [SerializeField] private float rippleLifeTime = 0.8f; // 聲紋顯示多久

    [Header("微調")]
    [SerializeField] private float heightOffset = 0.1f; // 稍微浮起，避免 Z-Fighting

    private void OnEnable()
    {
        // 訂閱 StealthManager 的事件
        // 注意：如果你的 StealthManager 沒有這個事件，請參考上面的說明加入
        StealthManager.OnNoiseEmitted += SpawnRipple;
    }

    private void OnDisable()
    {
        StealthManager.OnNoiseEmitted -= SpawnRipple;
    }

    private void SpawnRipple(Vector3 position, float range)
    {
        if (ripplePrefab == null) return;

        // 在噪音發生點生成 Ripple
        // 這裡假設你的地板是水平的，所以讓 Ripple 旋轉 90 度平躺
        Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
        Vector3 spawnPos = position + Vector3.up * heightOffset;

        SoundRipple ripple = Instantiate(ripplePrefab, spawnPos, rotation);

        // 初始化：傳入聲音的範圍 (Range)
        ripple.Initialize(range, rippleLifeTime);
    }
}