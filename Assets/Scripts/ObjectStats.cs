using UnityEngine;

public enum SoundMaterial
{
    Generic,    // 通用
    Metal,      // 金屬 (響亮)
    Wood,       // 木頭 (沉悶)
    Glass,      // 玻璃 (清脆、易碎)
    Plastic,    // 塑膠 (輕)
    Soft        // 布料/枕頭 (幾乎無聲)
}

public class ObjectStats : MonoBehaviour
{
    [Header("物理與材質")]
    public float weight = 1.0f;
    public SoundMaterial materialType = SoundMaterial.Generic;

    [Header("進階屬性")]
    [Tooltip("聲音倍率修正 (基於材質自動調整，也可手動覆蓋)")]
    public float noiseMultiplier = 1.0f;

    [Tooltip("是否容易碎裂 (配合 ImpactSoundEmitter 使用)")]
    public bool isFragile = false;

    [HideInInspector]
    public bool isInsideContainer = false;

    // 自動根據材質設定預設值
    private void OnValidate()
    {
        switch (materialType)
        {
            case SoundMaterial.Metal: noiseMultiplier = 1.5f; break;
            case SoundMaterial.Glass: noiseMultiplier = 1.2f; break;
            case SoundMaterial.Soft: noiseMultiplier = 0.2f; break;
            default: noiseMultiplier = 1.0f; break;
        }
    }
}