using UnityEngine;
using DG.Tweening;

public class MainMenuShowcase : MonoBehaviour
{
    [Header("展示物件清單 (把紙箱、鬧鐘等模型拖進來)")]
    [SerializeField] private GameObject[] itemModels;

    [Header("旋轉與漂浮設定")]
    [SerializeField] private float rotationDuration = 10f; // 轉一圈所需時間
    [SerializeField] private float floatHeight = 0.5f;     // 上下漂浮的幅度
    [SerializeField] private float floatDuration = 2f;     // 漂浮一次的時間

    private void Start()
    {
        if (itemModels.Length == 0)
        {
            Debug.LogWarning("Showcase models array is empty!");
            return;
        }

        // 1. 初始化：關閉所有模型
        foreach (var item in itemModels)
        {
            item.SetActive(false);
        }

        // 2. 隨機選取一個模型並啟用
        int randomIndex = Random.Range(0, itemModels.Length);
        itemModels[randomIndex].SetActive(true);

        // 3. 啟動 DOTween 動效 (完全不佔用 Update)
        StartShowcaseAnimation();
    }

    private void StartShowcaseAnimation()
    {
        // Y軸無限勻速旋轉 (360度)
        transform.DORotate(new Vector3(0, 360, 0), rotationDuration, RotateMode.FastBeyond360)
                 .SetEase(Ease.Linear)
                 .SetLoops(-1, LoopType.Incremental);

        // Y軸平滑上下漂浮
        float startY = transform.position.y;
        transform.DOMoveY(startY + floatHeight, floatDuration)
                 .SetEase(Ease.InOutSine)
                 .SetLoops(-1, LoopType.Yoyo);
    }

    private void OnDestroy()
    {
        // 釋放 Tween，避免切換場景時產生 Memory Leak
        transform.DOKill();
    }
}