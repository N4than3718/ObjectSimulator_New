using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音軌喇叭 (Audio Sources)")]
    [Tooltip("專門用來播 BGM 的喇叭")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("專門用來播 UI 按鈕聲的喇叭")]
    [SerializeField] private AudioSource uiSource;

    [Header("預設 BGM 音效")]
    [SerializeField] private AudioClip defaultBGM;

    [Header("預設 UI 音效")]
    [Tooltip("滑鼠『按下去』瞬間的聲音")]
    [SerializeField] private AudioClip clickDownSound;
    [Tooltip("滑鼠『放開』瞬間的聲音")]
    [SerializeField] private AudioClip clickReleaseSound;

    private void Awake()
    {
        // 💀 單例模式：確保全場只有一個 AudioManager
        if (Instance == null)
        {
            Instance = this;
            // 如果你希望 BGM 跨關卡不中斷，可以取消下面這行的註解
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 遊戲一開始自動播放預設 BGM
        if (defaultBGM != null)
        {
            PlayBGM(defaultBGM);
        }
    }

    // 💡 招式一：播放或切換背景音樂
    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null || clip == null) return;

        // 如果正在播同一首歌，就不重頭播
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    // 💡 招式二：播放 UI 按鈕聲
    // 💡 招式 A：滑鼠按下時呼叫
    public void PlayClickDown()
    {
        if (uiSource != null && clickDownSound != null)
        {
            uiSource.PlayOneShot(clickDownSound);
        }
    }

    // 💡 招式 B：滑鼠放開時呼叫
    public void PlayClickRelease()
    {
        if (uiSource != null && clickReleaseSound != null)
        {
            uiSource.PlayOneShot(clickReleaseSound);
        }
    }

    // 💡 招式三：萬用 2D 音效播放 (如果有特殊的過關音效可以呼叫這個)
    public void Play2DSFX(AudioClip clip)
    {
        if (uiSource != null && clip != null)
        {
            uiSource.PlayOneShot(clip);
        }
    }
}