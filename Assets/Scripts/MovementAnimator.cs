using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class MovementAnimator : MonoBehaviour
{
    // ... (�Ҧ��ѼƩM�ѦҫO������) ...
    [Header("����Ѧ�")]
    public Transform modelTransform;
    private PlayerMovement playerMovement;
    private CamControl camControl;
    [Header("�̰ʳ]�w")]
    [Tooltip("�ѱ���������ͪ��ݰʱj�׭��v")]
    [SerializeField] private float rotationShakeMultiplier = 50f;
    [Header("�`��̰ʱj��")]
    [SerializeField][Range(0f, 2f)] private float overallIntensity = 1f;
    [Header("X/Y/Z �̰ʰѼ�")]
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
            Debug.LogError("MovementAnimator �� 'Model Transform' ���O�Ū��I", this);
            return;
        }
        initialModelPosition = modelTransform.localPosition;
    }

    void OnEnable()
    {
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null && playerMovement.cameraTransform != null)
        {
            camControl = playerMovement.cameraTransform.GetComponent<CamControl>();
        }
        if (camControl == null)
        {
            Debug.LogError("MovementAnimator �b�ҥήɧ䤣�� CamControl �}���I", this);
        }
    }

    void Update()
    {
        if (modelTransform == null || playerMovement == null) return;

        float moveSpeed = playerMovement.CurrentHorizontalSpeed;
        float rotationSpeed = 0f;
        if (camControl != null)
        {
            rotationSpeed = camControl.RotationInput.magnitude * rotationShakeMultiplier;
        }

        float totalSpeed = moveSpeed + rotationSpeed;

        // ������ ����ק�G�s�W playerMovement.IsGrounded �P�_ ������
        if (totalSpeed > 0.1f && playerMovement.IsGrounded)
        {
            timer += Time.deltaTime * totalSpeed * 0.5f;
            float offsetX = Mathf.Sin(timer * bobFrequencyX) * bobAmplitudeX;
            float offsetY = Mathf.Cos(timer * bobFrequencyY) * bobAmplitudeY;
            float offsetZ = Mathf.Sin(timer * bobFrequencyZ) * bobAmplitudeZ;
            modelTransform.localPosition = initialModelPosition + new Vector3(offsetX, offsetY, offsetZ) * overallIntensity;
        }
        else
        {
            timer = 0;
            modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, initialModelPosition, Time.deltaTime * 10f);
        }
        // ������������������������������������������������
    }
}