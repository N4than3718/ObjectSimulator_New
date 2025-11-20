using UnityEngine;
using UnityEngine.UI;

public class NpcStatusUI : MonoBehaviour
{
    [Header("UI 參考")]
    [SerializeField] private Image alertBarFill; // 拖曳警戒條的填充 Image
    [SerializeField] private Image statusIcon;   // 拖曳顯示圖示的 Image
    [SerializeField] private Canvas canvasGroup; // 控制整體顯示/隱藏

    [Header("圖示資源")]
    [SerializeField] private Sprite questionMarkIcon; // ? 圖示
    [SerializeField] private Sprite exclamationMarkIcon; // ! 圖示

    [Header("設定")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0); // 頭頂高度偏移

    private NpcAI linkedNPC;
    private Camera mainCam;

    // 初始化：由 NpcAI 呼叫
    public void Initialize(NpcAI npc)
    {
        linkedNPC = npc;
        mainCam = Camera.main; // 或者是 TeamManager.CurrentCameraTransform
        // 預設隱藏
        UpdateUI(0, NpcAI.NpcState.Searching);
    }

    void LateUpdate()
    {
        if (linkedNPC == null) { Destroy(gameObject); return; }

        // 1. 跟隨 NPC 位置
        transform.position = linkedNPC.transform.position + offset;

        // 2. 永遠面向攝影機 (Billboard 效果)
        if (mainCam != null)
        {
            transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward,
                             mainCam.transform.rotation * Vector3.up);
        }

        // 3. 更新 UI 顯示
        UpdateUI(linkedNPC.CurrentAlertLevel, linkedNPC.CurrentState);
    }

    private void UpdateUI(float alertLevel, NpcAI.NpcState state)
    {
        // 根據警戒值更新進度條 (假設最大值 200)
        float fillAmount = alertLevel / 200f;
        alertBarFill.fillAmount = fillAmount;

        // 狀態邏輯
        if (state == NpcAI.NpcState.Alerted)
        {
            // 高度警戒：顯示 "!"，隱藏條 (或全滿)
            statusIcon.gameObject.SetActive(true);
            statusIcon.sprite = exclamationMarkIcon;
            alertBarFill.transform.parent.gameObject.SetActive(false); // 追擊時可能不需要條
        }
        else if (alertLevel > 0) // Searching 但有警戒值
        {
            // 懷疑中：顯示"?"
            alertBarFill.transform.parent.gameObject.SetActive(true);
            statusIcon.gameObject.SetActive(true);
            statusIcon.sprite = questionMarkIcon;

            if (alertLevel >= 100) // 高於 100 顯示 "!"
            {
                statusIcon.sprite = exclamationMarkIcon;
            }
        }
        else
        {
            // 完全沒事：全部隱藏
            statusIcon.gameObject.SetActive(false);
            alertBarFill.transform.parent.gameObject.SetActive(false);
        }
    }
}