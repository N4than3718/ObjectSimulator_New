using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FieldOfView))]
public class NpcAI : MonoBehaviour
{
    [Header("警戒值設定")]
    [Tooltip("警戒值上升速度 (每秒)")]
    [SerializeField] private float alertIncreaseRate = 25f;
    [Tooltip("警戒值下降速度 (每秒)")]
    [SerializeField] private float alertDecreaseRate = 10f;
    [Tooltip("判定為『移動』的最小速度閾值")]
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField][Range(0, 100)] private float currentAlertLevel = 0f;

    // --- 公開屬性 ---
    public float CurrentAlertLevel => currentAlertLevel;

    // --- 私有變數 ---
    private FieldOfView fov;
    private Dictionary<Transform, Vector3> lastKnownPositions = new Dictionary<Transform, Vector3>();
    private List<Transform> targetsToForget = new List<Transform>();

    void Start()
    {
        fov = GetComponent<FieldOfView>();
    }

    void Update()
    {
        bool sawMovingTarget = CheckForMovingTargets();

        if (sawMovingTarget)
        {
            // 看到移動目標，增加警戒值
            currentAlertLevel += alertIncreaseRate * Time.deltaTime;
        }
        else
        {
            // 沒看到，降低警戒值
            currentAlertLevel -= alertDecreaseRate * Time.deltaTime;
        }

        // 將警戒值限制在 0-100 之間
        currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 100f);

        // 在這裡根據 currentAlertLevel 來觸發不同行為
        HandleAlertLevels();
    }

    private bool CheckForMovingTargets()
    {
        bool detectedMovement = false;

        // 檢查視野內是否有目標移動
        foreach (Transform target in fov.visibleTargets)
        {
            // 如果是第一次看到這個目標，先記錄它的位置
            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue; // 跳過這一幀的移動檢測
            }

            // 計算從上一幀到現在的移動距離
            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);

            // 如果移動距離超過閾值，就判定為移動中
            if (distanceMoved / Time.deltaTime > movementThreshold)
            {
                detectedMovement = true;
            }

            // 更新這一幀的位置，供下一幀比較
            lastKnownPositions[target] = target.position;
        }

        // 清理那些已經不在視野內的目標記錄，避免記憶體洩漏
        targetsToForget.Clear();
        foreach (var pair in lastKnownPositions)
        {
            if (!fov.visibleTargets.Contains(pair.Key))
            {
                targetsToForget.Add(pair.Key);
            }
        }
        foreach (Transform target in targetsToForget)
        {
            lastKnownPositions.Remove(target);
        }

        return detectedMovement;
    }

    private void HandleAlertLevels()
    {
        if (currentAlertLevel > 75)
        {
            // 高度警戒：追擊！
            Debug.Log("ALERT LEVEL HIGH: Engaging target!");
        }
        else if (currentAlertLevel > 25)
        {
            // 中度警戒：懷疑，開始搜索
            Debug.Log("ALERT LEVEL MEDIUM: Searching for target...");
        }
        else
        {
            // 低度警戒：回到巡邏
        }
    }
}