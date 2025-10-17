using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class MovementAnimator : MonoBehaviour
{
    [Header("����Ѧ�")]
    [Tooltip("�n�̰ʪ��ҫ� Transform")]
    public Transform modelTransform;
    private PlayerMovement playerMovement;

    [Header("�̰ʳ]�w")]
    [Tooltip("�N���ʳt���ഫ���̰��W�v����¦���v")]
    [SerializeField] private float frequencyMultiplier = 1.5f;
    [Tooltip("�`��̰ʱj��")]
    [SerializeField][Range(0f, 2f)] private float overallIntensity = 1f;

    [Header("X/Y/Z �̰ʰѼ� (��¦�W�v)")]
    [Tooltip("X �b����¦�̰��W�v")]
    [SerializeField] private float bobFrequencyX = 1.0f;
    [Tooltip("Y �b����¦�̰��W�v")]
    [SerializeField] private float bobFrequencyY = 1.2f; // ���W�v���}
    [Tooltip("Z �b����¦�̰��W�v")]
    [SerializeField] private float bobFrequencyZ = 0.9f;

    [Header("X/Y/Z �̰ʴT��")]
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
            Debug.LogError("MovementAnimator �� 'Model Transform' ���O�Ū��I", this);
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

            // ��¦�W�v�{�b�P�t�רM�w���p�ɾ��ۭ��A���ͳ̲ת��̰��W�v
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