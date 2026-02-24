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
    [SerializeField] private Transform ringingPart;
    // 💀 把 shakeAmount 改大一點，因為現在代表旋轉角度
    [SerializeField] private float shakeAngle = 15f;

    private bool isRinging = false;
    private float timer = 0f;
    // 💀 改為記錄初始旋轉
    private Quaternion originalLocalRot;

    protected override void Start()
    {
        base.Start();
        // 💀 記錄初始旋轉值
        if (ringingPart != null) originalLocalRot = ringingPart.localRotation;
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
            // 💀 關閉時復原旋轉
            if (ringingPart != null) ringingPart.localRotation = originalLocalRot;
        }
    }

    // 覆寫 Update：處理持續發聲和視覺震動
    protected override void Update()
    {
        base.Update();

        if (isRinging)
        {
            timer += Time.deltaTime;
            if (timer >= noiseInterval)
            {
                EmitNoise();
                timer = 0f;
            }

            // ✨ 視覺升級：改成旋轉震動 (Juice!)
            if (ringingPart != null)
            {
                // 產生隨機的 Z 軸搖擺角度 (鬧鐘左右晃)
                float randomZ = Random.Range(-shakeAngle, shakeAngle);
                ringingPart.localRotation = originalLocalRot * Quaternion.Euler(0, 0, randomZ);
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
        if (ringingPart != null) ringingPart.localRotation = originalLocalRot;
    }
}