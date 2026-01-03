using UnityEngine;

public class IntroSequence : MonoBehaviour
{
    [Header("目標物件")]
    public HighlightableObject targetMug; // 拖入你的馬克杯

    // 這個方法會在 Timeline 的 "Signal Emitter" 或 "Simple Animation Event" 中被呼叫
    public void TriggerMugAwake()
    {
        if (targetMug != null)
        {
            // 💀 Coder: 模仿玩家準星對準的效果，強制開啟黃色高亮
            targetMug.SetTargetedHighlight(true);

            // 額外微調：讓邊框稍微加粗一點，增加戲劇性
            targetMug.SetOutlineWidth(0.005f);

            Debug.Log("✨ 開場動畫：馬克杯已甦醒！");
        }
    }
}