using UnityEngine;
using UnityEngine.EventSystems; // 💀 這是讀取滑鼠事件的魔法套件

// 繼承 IPointerDownHandler 就等於在程式裡內建了 Event Trigger 的 PointerDown！
public class UIButtonSound : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // 💀 按鈕按下時，自動透過 Instance (單例) 呼叫總管！完全不用拖曳綁定！
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickDown();
            AudioManager.Instance.PlayClickRelease();
        }
    }
}