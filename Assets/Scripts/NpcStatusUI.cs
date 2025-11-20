using UnityEngine;
using UnityEngine.UI;

public class NpcStatusUI : MonoBehaviour
{
    [Header("UI 參考")]
    [SerializeField] private Image questionBar;    // 拖曳「問號」Bar
    [SerializeField] private Image exclamationBar; // 拖曳「驚嘆號」Bar
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
        // 狀態邏輯
        if (state == NpcAI.NpcState.Alerted)
        {
            // --- 狀態：警戒 (追擊中) ---
            // 顯示：驚嘆號
            // 隱藏：問號

            if (questionBar != null) questionBar.gameObject.SetActive(false);

            if (exclamationBar != null)
            {
                exclamationBar.gameObject.SetActive(true);
                exclamationBar.color = Color.red; // 紅色 (危險)
                exclamationBar.fillAmount = 1.0f; // 追擊時通常是全滿，或者你可以顯示剩餘耐力?
            }
            return; // 追擊狀態下直接結束，不跑下面的邏輯
        }
        else if (alertLevel > 0) // Searching 但有警戒值
        {
            if (exclamationBar != null) exclamationBar.gameObject.SetActive(false);
            statusIcon.gameObject.SetActive(true);

            if (questionBar != null)
            {
                statusIcon.sprite = questionMarkIcon;
                questionBar.gameObject.SetActive(true);
                questionBar.fillAmount = alertLevel / 100f;
            }

            if (alertLevel >= 100) // 高於 100 顯示 "!"
            {
                questionBar.gameObject.SetActive(false);
                statusIcon.sprite = exclamationMarkIcon;
                exclamationBar.gameObject.SetActive(true);
                exclamationBar.color = new Color(1f, 0.5f, 0f); // 橘色 (高度警戒)
                exclamationBar.fillAmount = (alertLevel -100f) / 100f;

            }
        }
        else
        {
            // --- 狀態：沒事 ---
            // 全部隱藏
            statusIcon.gameObject.SetActive(false);
            if (questionBar != null) questionBar.gameObject.SetActive(false);
            if (exclamationBar != null) exclamationBar.gameObject.SetActive(false);
        }
    }
}