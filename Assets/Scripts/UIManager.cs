using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI 綁定")]
    [Tooltip("顯示技能圖示的 Image (中間那個圓)")]
    [SerializeField] private Image iconImage;
    public Image saveIcon; // 存檔成功的小圖示 (例如一個小磁碟或勾勾)

    [Tooltip("顯示冷卻遮罩的 Image (外圈或覆蓋層)")]
    [SerializeField] private Image cooldownFillImage;

    [Header("預設圖示 (可選)")]
    [SerializeField] private Sprite defaultIcon; // 當沒有技能時顯示的圖示

    private BaseSkill currentSkill;
    private GameObject lastPlayerObj;
    public float fillSpeed = 2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        // 1. 隨時監控當前主角是誰
        if (PlayerMovement.Current == null)
        {
            // 沒有主角時，隱藏或顯示預設
            if (iconImage) iconImage.enabled = false;
            if (cooldownFillImage) cooldownFillImage.fillAmount = 0;
            return;
        }

        // 2. 檢查主角是否換人了 (優化效能，只在切換瞬間抓 Component)
        if (PlayerMovement.Current.gameObject != lastPlayerObj)
        {
            lastPlayerObj = PlayerMovement.Current.gameObject;
            currentSkill = PlayerMovement.Current.GetComponent<BaseSkill>();

            // 切換瞬間更新圖示
            UpdateIcon();
        }

        // 3. 每幀更新冷卻條 (如果有技能的話)
        if (currentSkill != null && currentSkill.enabled)
        {
            if (iconImage) iconImage.enabled = true;

            // 更新冷卻進度
            if (cooldownFillImage)
            {
                cooldownFillImage.fillAmount = currentSkill.GetCooldownRatio();
            }
        }
        else
        {
            // 該角色沒有技能
            if (iconImage) iconImage.enabled = false;
            if (cooldownFillImage) cooldownFillImage.fillAmount = 0;
        }
    }

    private void UpdateIcon()
    {
        if (iconImage == null) return;

        if (currentSkill != null)
        {
            // 換成新角色的技能圖示
            iconImage.sprite = currentSkill.GetSkillIcon();
            iconImage.enabled = true;
        }
        else
        {
            // 沒技能就顯示透明或預設圖
            iconImage.sprite = defaultIcon;
            iconImage.enabled = (defaultIcon != null);
        }
    }

    // 強制刷新 (給 TeamManager 切換時呼叫，以防 Update 有延遲)
    public void ForceUpdateUI()
    {
        lastPlayerObj = null; // 強制觸發下一次 Update 的偵測
    }

    public IEnumerator ShowSaveNotification()
    {
        saveIcon.gameObject.SetActive(true);
        saveIcon.fillAmount = 0f;

        float targetProgress = 0.9f;
        while (saveIcon.fillAmount < targetProgress)
        {
            saveIcon.fillAmount = Mathf.MoveTowards(saveIcon.fillAmount, targetProgress, Time.unscaledDeltaTime * fillSpeed);
            yield return null;
        }

        saveIcon.fillAmount = 1f;

        yield return new WaitForSecondsRealtime(0.5f);
        saveIcon.gameObject.SetActive(false);
        Debug.Log("[System] 存檔成功！");
    }
}