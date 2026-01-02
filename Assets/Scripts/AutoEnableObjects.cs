using UnityEngine;

public class AutoEnableObjects : MonoBehaviour
{
    [Header("把那些你為了方便而關掉的物件拖進來")]
    [Tooltip("遊戲開始時，這些物件會被強制開啟 (SetActive True)")]
    public GameObject[] visualsToEnable;

    [Header("設定")]
    public bool enableOnAwake = true;

    void Awake()
    {
        if (enableOnAwake)
        {
            ToggleVisuals(true);
        }
    }

    // 公開這個方法，讓其他腳本 (如 TeamManager) 也可以呼叫
    public void ToggleVisuals(bool isActive)
    {
        foreach (var obj in visualsToEnable)
        {
            if (obj != null)
            {
                obj.SetActive(isActive);
            }
        }

        Debug.Log($"[AutoEnable] 已將 {visualsToEnable.Length} 個環境物件設為: {isActive}");
    }

    // --- ✨ 小功能：在編輯器裡也可以按右鍵快速開關 ---
    [ContextMenu("開啟所有物件 (Show All)")]
    public void ShowAll()
    {
        foreach (var obj in visualsToEnable) if (obj != null) obj.SetActive(true);
    }

    [ContextMenu("關閉所有物件 (Hide All)")]
    public void HideAll()
    {
        foreach (var obj in visualsToEnable) if (obj != null) obj.SetActive(false);
    }
}