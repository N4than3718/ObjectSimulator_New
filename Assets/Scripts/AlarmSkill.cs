using UnityEngine;
using UnityEngine.InputSystem;

public class AlarmSkill : BaseSkill, ISaveable
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

            // 💀 音效啟動：指定音檔並播放 (因為有開 Loop，播一次就好)
            if (audioSource != null && ringSound != null)
            {
                audioSource.clip = ringSound;
                audioSource.Play();
            }

            EmitNoise();
            timer = 0f;
        }
        else
        {
            Debug.Log($"{skillName}: 鬧鐘關閉。");

            // 💀 音效關閉：手動把聲音掐斷
            if (audioSource != null) audioSource.Stop();

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
    }

    // 💀 2. 建立專屬的存檔資料結構
    [System.Serializable]
    private class AlarmSaveState
    {
        public bool isRinging;
        public float currentTimer;
    }

    // 💀 3. 實作 GetSaveData
    public string GetSaveData()
    {
        AlarmSaveState state = new AlarmSaveState
        {
            isRinging = this.isRinging,
            currentTimer = this.timer
        };
        return JsonUtility.ToJson(state);
    }

    private void OnDisable()
    {
        isRinging = false;
        if (ringingPart != null) ringingPart.localRotation = originalLocalRot;

        // 💀 防呆：物件消失或關閉時，聲音必須立刻停止！
        if (audioSource != null) audioSource.Stop();
    }

    public void RestoreSaveData(string jsonState)
    {
        AlarmSaveState state = JsonUtility.FromJson<AlarmSaveState>(jsonState);

        this.isRinging = state.isRinging;
        this.timer = state.currentTimer;

        if (!this.isRinging)
        {
            if (ringingPart != null) ringingPart.localRotation = originalLocalRot;
            if (audioSource != null) audioSource.Stop(); // 讀檔發現是關的，確保靜音
        }
        else
        {
            // 💀 讀檔發現鬧鐘是響的，趕快幫它把聲音播回來！
            if (audioSource != null && ringSound != null && !audioSource.isPlaying)
            {
                audioSource.clip = ringSound;
                audioSource.Play();
            }
        }
    }
}