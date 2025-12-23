using System.Collections.Generic;
using UnityEngine;

public class NoiseRippleManager : MonoBehaviour
{
    public static NoiseRippleManager Instance { get; private set; }

    [Header("資源設定")]
    [SerializeField] private SoundRipple ripplePrefab; // 拖曳剛才做的 Ripple Prefab
    [SerializeField] private float rippleLifeTime = 0.8f; // 聲紋顯示多久

    [Header("物件池設定")] // [新增]
    [SerializeField] private int initialPoolSize = 20; // 預先準備 20 個
    private Queue<SoundRipple> _pool = new Queue<SoundRipple>();

    [Header("微調")]
    [SerializeField] private float heightOffset = 0.05f; // 稍微浮起，避免 Z-Fighting

    [Header("效能與視覺優化")]
    [Tooltip("單一物件產生聲紋的最小間隔時間 (秒)")]
    [SerializeField] private float objectSpawnCooldown = 0.3f;

    private Dictionary<GameObject, float> _cooldownDict = new Dictionary<GameObject, float>();
    private float _globalFallbackTime = -10f; // 給沒有 source 的聲音用的後備冷卻

    private void Awake()
    {
        // 設定單例
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // [新增] 遊戲開始時，先「印好」20 個波紋備用
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            SoundRipple ripple = CreateRawRipple(); // 只生成
            _pool.Enqueue(ripple); // 放進倉庫
        }
    }

private SoundRipple CreateRawRipple()
    {
        SoundRipple ripple = Instantiate(ripplePrefab, transform);
        ripple.gameObject.SetActive(false);
        return ripple;
    }

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

    private void SpawnRipple(GameObject source, Vector3 position, float range)
    {
        if (ripplePrefab == null) return;
        float currentTime = Time.time;

        // --- 冷卻邏輯檢查 ---
        if (source != null)
        {
            // 1. 檢查該物件是否在冷卻表中
            if (_cooldownDict.TryGetValue(source, out float lastTime))
            {
                // 如果還在冷卻中，直接跳過
                if (currentTime < lastTime + objectSpawnCooldown) return;
            }

            // 更新/新增冷卻時間
            _cooldownDict[source] = currentTime;
        }
        else
        {
            // 如果沒有 source (例如某些系統音效)，使用全域冷卻
            if (currentTime < _globalFallbackTime + objectSpawnCooldown) return;
            _globalFallbackTime = currentTime;
        }
        // --------------------

        // 在噪音發生點生成 Ripple
        // 這裡假設你的地板是水平的，所以讓 Ripple 旋轉 90 度平躺
        Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
        Vector3 spawnPos = position + Vector3.up * heightOffset;

        SoundRipple ripple = GetRippleFromPool(spawnPos, rotation);

        // 初始化：傳入聲音的範圍 (Range)
        if (ripple != null)
        {
            ripple.Initialize(range, rippleLifeTime);
        }
    }

    private SoundRipple GetRippleFromPool(Vector3 pos, Quaternion rot)
    {
        SoundRipple ripple = null;

        if (_pool.Count > 0)
        {
            ripple = _pool.Dequeue();
        }
        else
        {
            // 如果池子空了，臨時加印一個 (Expand Pool)
            ripple = CreateRawRipple();
        }

        // 設定狀態
        ripple.transform.position = pos;
        ripple.transform.rotation = rot;
        ripple.gameObject.SetActive(true);

        return ripple;
    }

    public void ReturnRipple(SoundRipple ripple)
    {
        if (!ripple.gameObject.activeSelf) return;

        ripple.gameObject.SetActive(false); // 睡覺
        _pool.Enqueue(ripple); // 排隊
    }

    // (可選) 定期清理被銷毀的物件，防止記憶體洩漏
    private void Update()
    {
        if (Time.frameCount % 300 == 0) // 每 300 幀清理一次
        {
            CleanUpDictionary();
        }
    }

    private void CleanUpDictionary()
    {
        List<GameObject> keysToRemove = new List<GameObject>();
        foreach (var kvp in _cooldownDict)
        {
            if (kvp.Key == null) keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
        {
            _cooldownDict.Remove(key);
        }
    }
}