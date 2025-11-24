using UnityEngine;

public class SoundRipple : MonoBehaviour
{
    [Header("視覺設定")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AnimationCurve expansionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("預設參數 (會被 Manager 覆蓋)")]
    [SerializeField] private float duration = 1.0f;
    [SerializeField] private float maxRadius = 5.0f;

    private float timer = 0f;
    private Color startColor;

    public void Initialize(float radius, float lifeTime)
    {
        this.maxRadius = radius;
        this.duration = lifeTime;
        this.timer = 0f;

        // 記錄初始顏色
        if (spriteRenderer != null) startColor = spriteRenderer.color;

        // 初始大小設為 0
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / duration);

        // 1. 動態擴散 logic (使用 AnimationCurve 讓動態更滑順)
        float currentRadius = expansionCurve.Evaluate(progress) * maxRadius * 2; // *2 因為 Scale 是直徑
        transform.localScale = new Vector3(currentRadius, currentRadius, 1f);

        // 2. 透明度淡出 Logic
        if (spriteRenderer != null)
        {
            float alpha = fadeCurve.Evaluate(progress) * startColor.a;
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
        }

        // 3. 生命週期結束
        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}