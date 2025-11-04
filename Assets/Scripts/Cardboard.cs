using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using System.Linq; // 這是 C# 的好東西，用來加總 (Sum)
using UnityEngine;

// 確保它跟 PlayerMovement 掛在一起，也必須有一個 Collider
[RequireComponent(typeof(PlayerMovement), typeof(Collider), typeof(ObjectStats))]
public class Cardboard : MonoBehaviour
{
    [Header("元件參考")]
    private PlayerMovement playerMovement;
    private Collider containerTrigger; // 這是專門用來偵測物品的 Trigger

    [Header("Heavy Push Settings")]
    [Tooltip("超過這個重量，移動方式變為'重推'")]
    [SerializeField] private float weightThreshold = 50f;
    [Tooltip("重推的單次爆發力")]
    [SerializeField] private float heavyPushForce = 50f;
    [Tooltip("每次重推的間隔/動畫時長 (秒)")]
    [SerializeField] private float pushInterval = 0.8f;

    [Header("庫存狀態")]
    [Tooltip("目前裝在裡面的物品清單 (除錯用)")]
    [SerializeField]
    private List<ObjectStats> itemsInside = new List<ObjectStats>();

    private ObjectStats selfObjectStats;

    void Awake()
    {
        // 找到在同一個物件上的 PlayerMovement
        playerMovement = GetComponent<PlayerMovement>();
        selfObjectStats = GetComponent<ObjectStats>();

        // 關鍵：找到身上所有的 Collider，
        // 並指定 "第一個" 設為 Is Trigger 的 Collider 當作我們的偵測範圍
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (col.isTrigger)
            {
                containerTrigger = col;
                break; // 找到就停
            }
        }

        if (containerTrigger == null)
        {
            Debug.LogError($"'{name}' 上的 BoxContainer.cs 找不到一個設為 'Is Trigger' 的 Collider！", this);
            Debug.LogWarning("請在紙箱物件上新增一個 Collider (例如 Box Collider)，並勾選 'Is Trigger'。", this);
        }

        // 遊戲一開始，先把紙箱的基礎重量設定好
        UpdateTotalWeight();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 檢查進來的東西是不是 "物品"
        // (我們假設所有 "物品" 都有 ItemData 腳本)
        ObjectStats item = other.GetComponent<ObjectStats>();

        // 確保 1. 它是物品 2. 它還沒在任何容器裡
        if (item != null && item != selfObjectStats && !item.isInsideContainer)
        {
            item.isInsideContainer = true;
            itemsInside.Add(item);

            // 重新計算總重量
            UpdateTotalWeight();

            Debug.Log($"[BoxContainer] {other.name} (Weight: {item.weight}kg) 進入。");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ObjectStats item = other.GetComponent<ObjectStats>();

        // 確保 1. 它是物品 2. 它真的在我們的清單裡
        if (item != null && itemsInside.Contains(item))
        {
            item.isInsideContainer = false;
            itemsInside.Remove(item);

            // 重新計算總重量
            UpdateTotalWeight();
            Debug.Log($"[BoxContainer] {other.name} 離開。");
        }
    }

    /// <summary>
    /// 計算總重量並更新 PlayerMovement
    /// </summary>
    private void UpdateTotalWeight()
    {
        // 總重 = 紙箱自重 + (清單中所有物品的重量)
        float totalWeight = selfObjectStats.weight;

        // 使用 Linq.Sum() 快速加總
        totalWeight += itemsInside.Sum(item => item.weight);

        bool isOver = (totalWeight > weightThreshold);

        // **這就是關鍵：**
        // 把計算好的總重，"餵" 給 PlayerMovement 腳本
        playerMovement.SetWeightAndPushStats(totalWeight, isOver, heavyPushForce, pushInterval);

        //Debug.Log($"[BoxContainer] 總重量更新為: {totalWeight}kg");
    }
}