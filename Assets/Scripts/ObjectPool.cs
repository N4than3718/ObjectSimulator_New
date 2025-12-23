using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [Header("池子設定")]
    public GameObject prefabToPool; // 要生成的東西 (例如 Ripple)
    public int initialPoolSize = 20; // 一開始要準備幾個 (例如 20 個)
    public bool shouldExpand = true; // 如果用光了，要不要自動加開？

    // 真正的池子 (使用 Queue 效能最好)
    private Queue<GameObject> poolQueue = new Queue<GameObject>();

    void Awake()
    {
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewObject();
        }
    }

    // 建立一個新物件，並把它關掉塞進池子
    private GameObject CreateNewObject()
    {
        GameObject obj = Instantiate(prefabToPool, transform); // 生成在自己底下，保持整潔
        obj.SetActive(false); // 先關掉 (休眠)
        poolQueue.Enqueue(obj); // 排隊
        return obj;
    }

    /// <summary>
    /// 【借書】從池子裡拿出一個物件
    /// </summary>
    public GameObject GetObject(Vector3 position, Quaternion rotation)
    {
        if (poolQueue.Count == 0)
        {
            if (shouldExpand)
            {
                CreateNewObject(); // 池子空了，臨時加印一本
            }
            else
            {
                return null; // 沒庫存了，也不准加印
            }
        }

        // 從隊列頭拿出一個
        GameObject obj = poolQueue.Dequeue();

        // 設定它的狀態 (重置位置、角度)
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true); // 【關鍵】喚醒它！

        return obj;
    }

    /// <summary>
    /// 【還書】把物件還回池子
    /// </summary>
    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false); // 【關鍵】讓它睡覺
        poolQueue.Enqueue(obj); // 重新排隊
    }
}