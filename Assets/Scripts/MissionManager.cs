using UnityEngine;
using TMPro; // 💀 記得引入 TMP 命名空間

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Header("UI 綁定")]
    [SerializeField] private TextMeshProUGUI missionText;

    [Header("任務設定")]
    [SerializeField] private string missionDescription = "把鑰匙推到門口";
    [SerializeField] private int targetAmount = 1;

    private int currentAmount = 0;
    private bool isMissionComplete = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        UpdateUI(); // 遊戲一開始先印出 (0/1)
    }

    // 💀 開放給外部觸發區呼叫的方法
    public void AddProgress(int amount)
    {
        if (isMissionComplete) return;

        currentAmount += amount;

        // 防止超過目標數量 (例如變成 2/1)
        if (currentAmount >= targetAmount)
        {
            currentAmount = targetAmount;
            CompleteMission();
        }
        else
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (missionText != null)
        {
            missionText.text = $"{missionDescription} ({currentAmount}/{targetAmount})";
        }
    }

    private void CompleteMission()
    {
        isMissionComplete = true;

        // 💎 Game Juice: 任務完成時字體變色，並播放成功音效
        if (missionText != null)
        {
            missionText.text = $"<color=#00FF00>{missionDescription} (完成!)</color>";
        }

        Debug.Log("任務達成！準備過關！");

        // 💀 呼叫你原本寫好的 GameDirector 來過關！
        // if (GameDirector.Instance != null) GameDirector.Instance.ShowVictoryPanel(); 
    }
}