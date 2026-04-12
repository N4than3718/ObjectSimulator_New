using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerObjectAudio : MonoBehaviour
{
    [Header("移動發聲設定")]
    [Tooltip("移動多遠發出一次碰撞聲？(數字越小頻率越高)")]
    [SerializeField] private float stepDistance = 1.0f;
    [Tooltip("最低觸發速度，低於此速度(例如只在原地稍微晃動)不發聲")]
    [SerializeField] private float minVelocity = 0.5f;

    [Header("材質音效庫 (對應地板 Tag)")]
    [SerializeField] private AudioClip[] woodSounds;   // Tag: "Wood"
    [SerializeField] private AudioClip[] carpetSounds; // Tag: "Carpet"
    [SerializeField] private AudioClip[] tileSounds;   // Tag: "Tile"
    [SerializeField] private AudioClip[] defaultSounds;// 如果沒偵測到，或沒設定 Tag 時播這個

    private AudioSource audioSource;
    private Vector3 lastStepPosition;
    private Rigidbody rb;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // 抓取物件身上的剛體 (如果腳本掛在子物件，用 GetComponentInParent)
        rb = GetComponentInParent<Rigidbody>();
        lastStepPosition = transform.position;
    }

    private void Update()
    {
        if (rb == null) return;

        // 只有在移動速度大於設定值時，才計算距離
        if (rb.linearVelocity.magnitude > minVelocity)
        {
            // 如果目前的座標跟上次發出聲音的座標，距離超過了 stepDistance
            if (Vector3.Distance(transform.position, lastStepPosition) >= stepDistance)
            {
                PlayMaterialImpactSound();
                lastStepPosition = transform.position; // 重置計步點
            }
        }
        else
        {
            // 如果物件停下來了，隨時更新最後位置，避免稍微動一下就突然發聲
            lastStepPosition = transform.position;
        }
    }

    private void PlayMaterialImpactSound()
    {
        AudioClip[] selectedSounds = defaultSounds;

        // 💀 核心邏輯：往下打一根 2 公尺的雷射光，偵測腳踩到什麼材質
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2.0f))
        {
            string groundTag = hit.collider.tag;

            // 根據地板的 Tag 切換音效包
            if (groundTag == "Wood") selectedSounds = woodSounds;
            else if (groundTag == "Carpet") selectedSounds = carpetSounds;
            else if (groundTag == "Tile") selectedSounds = tileSounds;
        }

        // 播放選中的音效
        if (selectedSounds != null && selectedSounds.Length > 0)
        {
            AudioClip clipToPlay = selectedSounds[Random.Range(0, selectedSounds.Length)];

            // 💎 Game Juice: 速度越快，撞擊聲越大！
            float speedVolume = Mathf.Clamp(rb.linearVelocity.magnitude / 5f, 0.2f, 1.0f);

            // 💎 Game Juice: 隨機微調音調 (Pitch)，讓連續滾動的聲音聽起來不會像機關槍一樣死板
            audioSource.pitch = Random.Range(0.85f, 1.15f);

            audioSource.PlayOneShot(clipToPlay, speedVolume);
        }
    }
}