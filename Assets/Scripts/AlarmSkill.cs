using UnityEngine;
using UnityEngine.InputSystem;

public class AlarmSkill : BaseSkill
{
    [Header("噪音設定")]
    [SerializeField] private float noiseRadius = 15f;    // 聲音傳多遠
    [SerializeField] private float noiseIntensity = 20f; // 聲音多強 (增加多少警戒值)
    [SerializeField] private float noiseInterval = 1.0f; // 每隔幾秒發出一次
    [SerializeField] private AudioClip ringSound; // [新增] 鬧鐘音效
    [SerializeField] private AudioSource audioSource;

    [Header("視覺回饋 (可選)")]
    [SerializeField] private Transform ringingPart; // 例如鬧鐘頂部的鈴鐺，讓它震動
    [SerializeField] private float shakeAmount = 0.1f;

    private bool isRinging = false;
    private float timer = 0f;
    private Vector3 originalLocalPos; // 用於震動復原

    protected override void Start()
    {
        base.Start();

        if (ringingPart != null) originalLocalPos = ringingPart.localPosition;
    }

    public override void OnInput(InputAction.CallbackContext context)
    {
        // 處理按下瞬間
        if (context.started)
        {
            TryActivate();
        }
    }

    // 覆寫 Activate：處理開關邏輯
    protected override void Activate()
    {
        isRinging = !isRinging; // 切換狀態

        if (isRinging)
        {
            Debug.Log($"{skillName}: 鬧鐘響了！(半徑: {noiseRadius})");
            // 立即發出第一次聲音
            EmitNoise();
            timer = 0f;
        }
        else
        {
            Debug.Log($"{skillName}: 鬧鐘關閉。");
            // 復原視覺位置
            if (ringingPart != null) ringingPart.localPosition = originalLocalPos;
        }
    }

    // 覆寫 Update：處理持續發聲和視覺震動
    protected override void Update()
    {
        base.Update(); // 這一行很重要！要保留 BaseSkill 的冷卻計算邏輯

        if (isRinging)
        {
            // 1. 定時發出噪音訊號
            timer += Time.deltaTime;
            if (timer >= noiseInterval)
            {
                EmitNoise();
                timer = 0f;
            }

            // 2. 視覺震動效果 (讓玩家知道它在響)
            if (ringingPart != null)
            {
                Vector3 randomOffset = Random.insideUnitSphere * shakeAmount;
                ringingPart.localPosition = originalLocalPos + randomOffset;
            }
        }
    }

    private void EmitNoise()
    {
        // 呼叫我們之前做好的 StealthManager
        StealthManager.MakeNoise(gameObject, transform.position, noiseRadius, noiseIntensity);

        // (如果之後有音效，這裡加 audioSource.Play())
    }

    // 當技能被外部強制中斷或物件被回收時
    private void OnDisable()
    {
        isRinging = false;
        if (ringingPart != null) ringingPart.localPosition = originalLocalPos;
    }
}