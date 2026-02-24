using UnityEngine;
using UnityEngine.UI;

public class NpcStatusUI : MonoBehaviour
{
    [Header("UI 參考")]
    [Tooltip("只需要這一個 Image 來切換所有狀態圖")]
    [SerializeField] private Image statusIcon;

    [Header("圖示序列 (Sprite Sequences)")]
    [Tooltip("放入警戒值 0~99 的連續圖檔 (例如：填滿中的問號)")]
    [SerializeField] private Sprite[] questionSprites;

    [Tooltip("放入警戒值 100~200 的連續圖檔 (例如：填滿中的驚嘆號)")]
    [SerializeField] private Sprite[] exclamationSprites;

    // 💀 新增這行：專門放 200 (最高警戒) 時要瘋狂閃爍的幾張圖！
    [Tooltip("放入最高警戒時要循環閃爍的圖檔 (例如：紅、白交替的全滿驚嘆號)")]
    [SerializeField] private Sprite[] alertLoopSprites;

    [Header("設定")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);
    // 💀 新增這行：控制驚嘆號循環播放的幀率 (FPS)
    [SerializeField] private float alertAnimationFPS = 12f;

    private NpcAI linkedNPC;
    private Camera mainCam;

    public void Initialize(NpcAI npc)
    {
        linkedNPC = npc;
        mainCam = Camera.main;
        UpdateUI(0, NpcAI.NpcState.Searching);
    }

    void LateUpdate()
    {
        if (linkedNPC == null) { Destroy(gameObject); return; }

        transform.position = linkedNPC.transform.position + offset;

        Transform currentCamTransform = GetActiveCameraTransform();
        if (currentCamTransform != null)
        {
            transform.LookAt(transform.position + currentCamTransform.rotation * Vector3.forward,
                             currentCamTransform.rotation * Vector3.up);
        }
        else if (mainCam != null)
        {
            currentCamTransform = mainCam.transform;
            transform.LookAt(transform.position + currentCamTransform.rotation * Vector3.forward,
                             currentCamTransform.rotation * Vector3.up);
        }

        UpdateUI(linkedNPC.CurrentAlertLevel, linkedNPC.CurrentState);
    }

    private Transform GetActiveCameraTransform()
    {
        if (TeamManager.Instance != null && TeamManager.Instance.physicalCam != null)
        {
            return TeamManager.Instance.physicalCam.transform;
        }
        return null;
    }

    private void UpdateUI(float alertLevel, NpcAI.NpcState state)
    {
        // 1. 最高警戒狀態：循環播放驚嘆號動畫 (Loop)
        if (state == NpcAI.NpcState.Alerted)
        {
            statusIcon.gameObject.SetActive(true);
            if (alertLoopSprites != null && alertLoopSprites.Length > 0)
            {
                // 💀 核心魔法：利用時間計算當前應該播放哪一張圖
                // Time.time * FPS 會得到總經過的幀數，再 % 陣列長度就能無限循環
                int loopIndex = (int)(Time.time * alertAnimationFPS) % exclamationSprites.Length;
                statusIcon.sprite = exclamationSprites[loopIndex];
            }
            return;
        }

        // 2. 有警戒值時：根據數值播放動畫幀
        if (alertLevel > 0)
        {
            statusIcon.gameObject.SetActive(true);

            if (alertLevel < 100f)
            {
                // [0~99] 階段：播放問號序列
                if (questionSprites != null && questionSprites.Length > 0)
                {
                    float progress = alertLevel / 100f; // 計算 0~1 比例
                    // 💀 Mathf.Clamp 確保索引不會因為剛好 100f 而超出陣列範圍 (IndexOutOfRangeException)
                    int index = Mathf.Clamp(Mathf.FloorToInt(progress * questionSprites.Length), 0, questionSprites.Length - 1);
                    statusIcon.sprite = questionSprites[index];
                }
            }
            else
            {
                // [100~200] 階段：播放驚嘆號序列
                if (exclamationSprites != null && exclamationSprites.Length > 0)
                {
                    float progress = (alertLevel - 100f) / 100f; // 計算 0~1 比例
                    int index = Mathf.Clamp(Mathf.FloorToInt(progress * exclamationSprites.Length), 0, exclamationSprites.Length - 1);
                    statusIcon.sprite = exclamationSprites[index];
                }
            }
        }
        else
        {
            // 3. 警戒值歸零：隱藏 UI
            statusIcon.gameObject.SetActive(false);
        }
    }
}