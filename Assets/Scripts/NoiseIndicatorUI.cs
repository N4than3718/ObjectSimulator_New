using UnityEngine;
using UnityEngine.UI;

public class NoiseIndicatorUI : MonoBehaviour
{
    [Header("UI 參考")]
    [SerializeField] private Transform rotator; // 旋轉軸心 (根物件)
    [SerializeField] private CanvasGroup canvasGroup; // 控制透明度
    [SerializeField] private Image iconImage; // 實際顯示的圖示

    [Header("設定")]
    [SerializeField] private float displayDuration = 1.0f; // 顯示多久
    [SerializeField] private float fadeSpeed = 2.0f;

    private Transform targetNPC; // 指向誰
    private Transform playerCamera;
    private float timer = 0f;

    public void Initialize(Transform npc, Transform camera, float intensity)
    {
        targetNPC = npc;
        playerCamera = camera;
        timer = displayDuration;

        // 根據強度改變顏色或大小 (可選)
        iconImage.color = Color.Lerp(Color.yellow, Color.red, intensity / 100f);

        canvasGroup.alpha = 1f; // 顯示
    }

    void Update()
    {
        // 1. 計時與淡出
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
            if (canvasGroup.alpha <= 0)
            {
                Destroy(gameObject); // 消失後銷毀
                return;
            }
        }

        // 2. 更新方向 (核心邏輯)
        if (targetNPC != null && playerCamera != null)
        {
            UpdateDirection();
        }
    }

    private void UpdateDirection()
    {
        // 取得從 攝影機 到 NPC 的方向向量
        Vector3 directionToNPC = targetNPC.position - playerCamera.position;

        // 只要水平方向 (忽略高度差)
        directionToNPC.y = 0;
        directionToNPC.Normalize();

        // 取得攝影機的前方向量 (也忽略高度)
        Vector3 cameraForward = playerCamera.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        // 計算 NPC 相對於攝影機的角度
        // 使用 SignedAngle 算出帶有正負號的角度 (左負右正)
        float angle = Vector3.SignedAngle(cameraForward, directionToNPC, Vector3.up);

        // 旋轉 UI (注意：Canvas 的旋轉是 Z 軸，且方向可能相反，視 Canvas 設定而定)
        // 通常 screen space 的 Z 旋轉：正值是逆時針。
        // 如果 NPC 在右邊 (角度為正)，我們希望指示器轉向右邊 (負 Z 旋轉)
        rotator.localEulerAngles = new Vector3(0, 0, -angle);
    }
}