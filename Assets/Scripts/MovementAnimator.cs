using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class MovementAnimator : MonoBehaviour
{
    [Header("元件參考")]
    [Tooltip("要晃動的模型 Transform")]
    public Transform modelTransform;
    private PlayerMovement playerMovement;

    [Header("總體晃動強度")]
    [SerializeField][Range(0f, 2f)] private float overallIntensity = 1f;

    [Header("X/Y/Z 晃動參數")]
    [SerializeField] private float bobFrequencyX = 12f;
    [SerializeField] private float bobAmplitudeX = 0.02f;
    [SerializeField] private float bobFrequencyY = 15f;
    [SerializeField] private float bobAmplitudeY = 0.03f;
    [SerializeField] private float bobFrequencyZ = 9f;
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

    // OnEnable 函式現在是空的，可以移除，但我暫時保留以防未來擴充
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
            // 抖動頻率現在可以只跟一個基礎頻率關聯，或也可以跟速度掛鉤
            timer += Time.fixedDeltaTime;

            float offsetX = Mathf.Sin(timer * bobFrequencyX) * bobAmplitudeX;
            float offsetY = Mathf.Cos(timer * bobFrequencyY) * bobAmplitudeY;
            float offsetZ = Mathf.Sin(timer * bobFrequencyZ) * bobAmplitudeZ;

            modelTransform.localPosition = initialModelPosition + new Vector3(offsetX, offsetY, offsetZ) * overallIntensity;
        }
        else
        {
            // 停止時平滑回到原位
            timer = 0;
            modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, initialModelPosition, Time.fixedDeltaTime * 10f);
        }
    }
}