using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class AutoEnableObjects : MonoBehaviour
{
    [Header("把那些你為了方便而關掉的物件拖進來")]
    [Tooltip("遊戲開始時，這些物件會被強制開啟 (SetActive True)")]
    public GameObject[] targetObjects;

    private List<Renderer> allRenderers = new List<Renderer>();
    private List<Collider> allColliders = new List<Collider>();

    void Awake()
    {
        // 1. 徹底搜尋所有子物件
        foreach (var obj in targetObjects)
        {
            if (obj != null)
            {
                // 抓取自己 + 所有子孫物件的 Renderer
                Renderer[] rs = obj.GetComponentsInChildren<Renderer>(true); // true 代表連原本被關掉的也抓
                allRenderers.AddRange(rs);

                // 抓取自己 + 所有子孫物件的 Collider
                Collider[] cs = obj.GetComponentsInChildren<Collider>(true);
                allColliders.AddRange(cs);
            }
        }

        // Debug 檢查一下到底抓到了幾個，如果由 0 變多，就代表修好了
        Debug.Log($"[Environment] 初始化完成：抓到了 {allRenderers.Count} 個 Renderer, {allColliders.Count} 個 Collider");
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
        // 處理所有抓到的 Renderers
        foreach (var r in allRenderers)
        {
            if (r != null)
            {
                // 🔥 強制切換模式
                if (isFullVisible)
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    // 有些特殊的 Shader 可能需要強制開啟 Render
                    r.enabled = true;
                }
                else
                {
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    // 保持 enabled = true，不然連影子都不會算
                    r.enabled = true;
                }
            }
        }

        // 處理所有抓到的 Colliders
        foreach (var col in allColliders)
        {
            if (col != null)
            {
                col.enabled = isFullVisible;
            }
        }

        // Debug.Log($"[Environment] 模式切換: {(isFullVisible ? "顯示" : "隱藏(ShadowOnly)")}");
    }

    public void ReScanEnvironment()
    {
        allRenderers.Clear();
        allColliders.Clear();

        foreach (var obj in targetObjects)
        {
            if (obj != null)
            {
                // 強制開啟物件，否則 GetComponentsInChildren 有可能抓不到
                obj.SetActive(true);
                allRenderers.AddRange(obj.GetComponentsInChildren<Renderer>(true));
                allColliders.AddRange(obj.GetComponentsInChildren<Collider>(true));
            }
        }
        Debug.Log($"[Environment] 重新掃描完成：{allRenderers.Count} Renderers.");
    }

    // --- 編輯器測試用按鈕 ---
    [ContextMenu("切換為：實體顯示 (Possess)")]
    public void TestShow() => ToggleVisuals(true);

    [ContextMenu("切換為：僅陰影 (Spectator)")]
    public void TestHide() => ToggleVisuals(false);
}