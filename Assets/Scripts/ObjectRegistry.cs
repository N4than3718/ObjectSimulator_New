using System.Collections.Generic;
using UnityEngine;

public class ObjectRegistry : MonoBehaviour
{
    public static ObjectRegistry Instance { get; private set; }

    // 字典：用物件的名字 (string) 來快速尋找物件實體 (GameObject)
    private Dictionary<string, GameObject> registry = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 物品報到用
    public void Register(GameObject obj)
    {
        if (obj == null) return;

        // 💀 警告：為了確保字典 Key 不重複，請確保場景中物品的名字都是獨一無二的 (例如 Cup_1, Cup_2)
        if (!registry.ContainsKey(obj.name))
        {
            registry.Add(obj.name, obj);
        }
    }

    // 紙箱讀檔取件用
    public GameObject GetObjectByName(string objName)
    {
        if (registry.TryGetValue(objName, out GameObject obj))
        {
            return obj;
        }
        Debug.LogWarning($"[ObjectRegistry] 找不到名為 {objName} 的物件！");
        return null;
    }

    // 💀 加入這個方法：當場景被銷毀時，清空字典並解除單例綁定
    private void OnDestroy()
    {
        if (Instance == this)
        {
            registry.Clear(); // 清空所有參考，釋放記憶體
            Instance = null;  // 解除綁定，讓下一個場景的 Registry 能夠順利接管
        }
    }
}