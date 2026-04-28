using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI; // 💀 記得引入 UI 命名空間

public class VolumeSettings : MonoBehaviour
{
    [Header("混音器與拉桿綁定")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        // 1. 讀取存檔 (如果沒存過，預設給 0.75 = 75% 音量)
        float savedBGM = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 0.75f);

        // 2. 更新拉桿的視覺位置
        if (bgmSlider != null) bgmSlider.value = savedBGM;
        if (sfxSlider != null) sfxSlider.value = savedSFX;

        // 3. 遊戲一開始先套用音量
        SetBGMVolume(savedBGM);
        SetSFXVolume(savedSFX);

        // 4. 💀 綁定事件：當玩家拖曳拉桿時，自動執行對應的程式
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    // 當 BGM 拉桿被拖曳時觸發
    private void SetBGMVolume(float sliderValue)
    {
        // 💀 核心數學：把拉桿的 0.0001 ~ 1 轉換成 AudioMixer 的 -80dB ~ 0dB
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;

        audioMixer.SetFloat("BGMVolume", db);
        PlayerPrefs.SetFloat("BGMVolume", sliderValue); // 存入記憶裡
    }

    // 當 SFX 拉桿被拖曳時觸發
    private void SetSFXVolume(float sliderValue)
    {
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;

        audioMixer.SetFloat("SFXVolume", db);
        PlayerPrefs.SetFloat("SFXVolume", sliderValue); // 存入記憶裡
    }

    private void OnDisable()
    {
        // 當你關閉暫停選單時，把設定確實寫入硬碟
        PlayerPrefs.Save();
    }
}