using UnityEngine;
using UnityEngine.Events;

public class Sensor : MonoBehaviour
{
    [Header("感應設定")]
    [Tooltip("只有這個名字的物品碰到才會開門")]
    public string targetItemName = "Key"; // 確保你的鑰匙/門禁卡 Prefab 叫做這個名字

    [Header("觸發事件")]
    public UnityEvent OnSensorTriggered; // 用來在 Inspector 裡綁定開門動畫或顯示撤離區

    private bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        // 檢查碰到的是不是我們要的鑰匙
        if (other.gameObject.name == targetItemName || other.gameObject.name == targetItemName + "(Clone)")
        {
            Debug.Log($"[Sensor] 感應成功！{targetItemName} 已確認。");
            isTriggered = true;

            // 觸發開門事件
            OnSensorTriggered.Invoke();

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.AddProgress(targetItemName, 1);
            }

            // 💀 寫入跨關卡事件：記錄門禁卡已經推下來了 (給第三關用)
            if (DataManager.Instance != null)
            {
                DataManager.Instance.SetEvent("KeyDropped", true);
            }
        }
    }
}