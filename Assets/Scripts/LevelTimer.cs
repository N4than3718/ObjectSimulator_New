using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance { get; private set; }
    public float CurrentTime { get; private set; }
    private bool isRunning = false;

    public int timesDetected { get; private set; } = 0;
    public int timesViolent { get; private set; } = 0;

    public void AddDetectionCount() => timesDetected++;
    public void AddViolenceCount() => timesViolent++;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        isRunning = true; // 遊戲開始即計時，或由 GameManager 啟動
    }

    private void Update()
    {
        if (isRunning)
        {
            CurrentTime += Time.deltaTime;
        }
    }

    public void StopTimer()
    {
        isRunning = false;
    }
}