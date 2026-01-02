using UnityEngine;
using UnityEngine.Rendering;

public class AutoEnableObjects : MonoBehaviour
{
    [Header("把那些你為了方便而關掉的物件拖進來")]
    [Tooltip("遊戲開始時，這些物件會被強制開啟 (SetActive True)")]
    public GameObject[] targetObjects;

    // 內部快取 Renderers，效能比較好
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders; // 選用：如果你也想順便關掉碰撞

    void Awake()
    {
        // 1. 預先抓取所有的 Renderer 和 Collider
        // 這樣切換時就不用一直 GetComponent，效能比較好
        int count = targetObjects.Length;
        cachedRenderers = new Renderer[count];
        cachedColliders = new Collider[count];

        for (int i = 0; i < count; i++)
        {
            if (targetObjects[i] != null)
            {
                cachedRenderers[i] = targetObjects[i].GetComponent<Renderer>();
                cachedColliders[i] = targetObjects[i].GetComponent<Collider>();
            }
        }
    }

    /// <summary>
    /// 切換視覺模式
    /// </summary>
    /// <param name="isFullVisible">
    /// true (附身模式) = 看得見實體 + 影子
    /// false (觀察者模式) = 看不見實體 + 但有影子 (Shadow Only)
    /// </param>
    public void ToggleVisuals(bool isFullVisible)
    {
        // 處理 Renderer (視覺)
        if (cachedRenderers != null)
        {
            foreach (var r in cachedRenderers)
            {
                if (r != null)
                {
                    // 🔥 核心魔法在這裡：切換陰影模式
                    if (isFullVisible)
                    {
                        // 附身時：完全顯示 (實體 + 陰影)
                        r.shadowCastingMode = ShadowCastingMode.On;
                    }
                    else
                    {
                        // 觀察者時：只渲染陰影，本體隱形
                        r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    }
                }
            }
        }

        // 處理 Collider (物理)
        // 💡 根據你的需求：
        // 如果你是觀察者模式，通常也希望滑鼠射線能穿過屋頂點到地板，所以要把 Collider 關掉
        if (cachedColliders != null)
        {
            foreach (var col in cachedColliders)
            {
                if (col != null)
                {
                    col.enabled = isFullVisible;
                }
            }
        }

        Debug.Log($"[Environment] 屋頂模式切換: {(isFullVisible ? "實體顯示" : "僅陰影")}");
    }

    // --- 編輯器測試用按鈕 ---
    [ContextMenu("切換為：實體顯示 (Possess)")]
    public void TestShow() => ToggleVisuals(true);

    [ContextMenu("切換為：僅陰影 (Spectator)")]
    public void TestHide() => ToggleVisuals(false);
}