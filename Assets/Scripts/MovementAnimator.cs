using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class MovementAnimator : MonoBehaviour
{
    [Header("元件參考")]
    [Tooltip("要晃動的模型 Transform")]
    public Transform modelTransform;
    private PlayerMovement playerMovement;

    [Header("晃動設定")]
    [Tooltip("將移動速度轉換為晃動頻率的基礎倍率")]
    [SerializeField] private float frequencyMultiplier = 1.5f;
    [Tooltip("總體晃動強度")]
    [SerializeField][Range(0f, 2f)] private float overallIntensity = 1f;

    [Header("X/Y/Z 晃動參數 (基礎頻率)")]
    [Tooltip("X 軸的基礎晃動頻率")]
    [SerializeField] private float bobFrequencyX = 1.0f;
    [Tooltip("Y 軸的基礎晃動頻率")]
    [SerializeField] private float bobFrequencyY = 1.2f; // 讓頻率錯開
    [Tooltip("Z 軸的基礎晃動頻率")]
    [SerializeField] private float bobFrequencyZ = 0.9f;

    [Header("X/Y/Z 晃動幅度")]
    [SerializeField] private float bobAmplitudeX = 0.02f;
    [SerializeField] private float bobAmplitudeY = 0.03f;
    [SerializeField] private float bobAmplitudeZ = 0.02f;


    private float timer = 0f;
    private Vector3 initialModelPosition;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        if (modelTransform == null)
        {
            Debug.LogError("MovementAnimator 的 'Model Transform' 欄位是空的！", this);
            return;
        }
        initialModelPosition = modelTransform.localPosition;
    }

    void OnEnable()
    {
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
    }

    void FixedUpdate()
    {
        if (modelTransform == null || playerMovement == null) return;

        float moveSpeed = playerMovement.CurrentHorizontalSpeed;

        if (moveSpeed > 0.1f && playerMovement.IsGrounded)
        {
            timer += Time.fixedDeltaTime * moveSpeed * frequencyMultiplier;

            // 基礎頻率現在與速度決定的計時器相乘，產生最終的晃動頻率
            float offsetX = Mathf.Sin(timer * bobFrequencyX) * bobAmplitudeX;
            float offsetY = Mathf.Cos(timer * bobFrequencyY) * bobAmplitudeY;
            float offsetZ = Mathf.Sin(timer * bobFrequencyZ) * bobAmplitudeZ;

            modelTransform.localPosition = initialModelPosition + new Vector3(offsetX, offsetY, offsetZ) * overallIntensity;
        }
        else
        {
            timer = 0;
            modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, initialModelPosition, Time.fixedDeltaTime * 10f);
        }
    }
}