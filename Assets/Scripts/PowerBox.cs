using UnityEngine;
using UnityEngine.Events;

public class PowerBox : MonoBehaviour
{
    [Header("破壞設定")]
    [Tooltip("需要用什麼物品來砸爛它？(填寫 Prefab 名稱)")]
    public string requiredWeaponName = "Hammer";
    [Tooltip("需要多大的力道才能砸壞？(建議 3~5)")]
    public float breakForceThreshold = 4.0f;

    private bool isBroken = false;

    [Header("場景連動")]
    [Tooltip("破壞後要開啟的撤離區 (例如：洗衣槽通道)")]
    public GameObject extractionZone;
    [Tooltip("破壞後要關閉的燈光 (可以把 Directional Light 或房間燈拖進來)")]
    public Light[] lightsToTurnOff;

    [Header("特效與音效 (選填)")]
    public UnityEvent OnPowerBroken; // 可以在 Inspector 綁定播放火花粒子或音效

    private void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;

        // 1. 檢查是不是我們指定的武器 (槌子)
        if (collision.gameObject.name == requiredWeaponName || collision.gameObject.name == requiredWeaponName + "(Clone)")
        {
            // 2. 檢查撞擊力道！必須重重地砸！
            float impactForce = collision.relativeVelocity.magnitude;

            if (impactForce >= breakForceThreshold)
            {
                BreakPowerBox();
            }
            else
            {
                Debug.Log($"[PowerBox] 砸的力道太小了 ({impactForce:F1})！需要從高一點的地方跳下來砸！");
            }
        }
    }

    private void BreakPowerBox()
    {
        isBroken = true;
        Debug.Log("[PowerBox] 總電源箱被破壞！整棟停電！");

        // 💀 1. 寫入跨關卡事件：記錄電箱已破壞 (給第三關用)
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SetEvent("PowerBroken", true);
        }

        // 💡 2. 關閉燈光 (製造停電效果)
        foreach (Light light in lightsToTurnOff)
        {
            if (light != null) light.enabled = false;
        }

        // 🚪 3. 開啟撤離區
        if (extractionZone != null)
        {
            extractionZone.SetActive(true);
        }

        // ✨ 4. 觸發額外特效 (並把電箱本體變黑，表示烤焦了)
        OnPowerBroken.Invoke();
        if (GetComponent<Renderer>() != null)
        {
            GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f); // 變成焦黑色
        }
    }
}