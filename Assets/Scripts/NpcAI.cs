using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FieldOfView))]
public class NpcAI : MonoBehaviour
{
    [Header("ĵ�٭ȳ]�w")]
    [Tooltip("ĵ�٭ȤW�ɳt�� (�C��)")]
    [SerializeField] private float alertIncreaseRate = 25f;
    [Tooltip("ĵ�٭ȤU���t�� (�C��)")]
    [SerializeField] private float alertDecreaseRate = 10f;
    [Tooltip("�P�w���y���ʡz���̤p�t���H��")]
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField][Range(0, 100)] private float currentAlertLevel = 0f;

    // --- ���}�ݩ� ---
    public float CurrentAlertLevel => currentAlertLevel;

    // --- �p���ܼ� ---
    private FieldOfView fov;
    private Dictionary<Transform, Vector3> lastKnownPositions = new Dictionary<Transform, Vector3>();
    private List<Transform> targetsToForget = new List<Transform>();

    void Start()
    {
        fov = GetComponent<FieldOfView>();
    }

    void Update()
    {
        bool sawMovingTarget = CheckForMovingTargets();

        if (sawMovingTarget)
        {
            // �ݨ첾�ʥؼСA�W�[ĵ�٭�
            currentAlertLevel += alertIncreaseRate * Time.deltaTime;
        }
        else
        {
            // �S�ݨ�A���Cĵ�٭�
            currentAlertLevel -= alertDecreaseRate * Time.deltaTime;
        }

        // �Nĵ�٭ȭ���b 0-100 ����
        currentAlertLevel = Mathf.Clamp(currentAlertLevel, 0f, 100f);

        // �b�o�̮ھ� currentAlertLevel ��Ĳ�o���P�欰
        HandleAlertLevels();
    }

    private bool CheckForMovingTargets()
    {
        bool detectedMovement = false;

        // �ˬd�������O�_���ؼв���
        foreach (Transform target in fov.visibleTargets)
        {
            // �p�G�O�Ĥ@���ݨ�o�ӥؼСA���O��������m
            if (!lastKnownPositions.ContainsKey(target))
            {
                lastKnownPositions.Add(target, target.position);
                continue; // ���L�o�@�V�������˴�
            }

            // �p��q�W�@�V��{�b�����ʶZ��
            float distanceMoved = Vector3.Distance(lastKnownPositions[target], target.position);

            // �p�G���ʶZ���W�L�H�ȡA�N�P�w�����ʤ�
            if (distanceMoved / Time.deltaTime > movementThreshold)
            {
                detectedMovement = true;
            }

            // ��s�o�@�V����m�A�ѤU�@�V���
            lastKnownPositions[target] = target.position;
        }

        // �M�z���Ǥw�g���b���������ؼаO���A�קK�O���鬪�|
        targetsToForget.Clear();
        foreach (var pair in lastKnownPositions)
        {
            if (!fov.visibleTargets.Contains(pair.Key))
            {
                targetsToForget.Add(pair.Key);
            }
        }
        foreach (Transform target in targetsToForget)
        {
            lastKnownPositions.Remove(target);
        }

        return detectedMovement;
    }

    private void HandleAlertLevels()
    {
        if (currentAlertLevel > 75)
        {
            // ����ĵ�١G�l���I
            Debug.Log("ALERT LEVEL HIGH: Engaging target!");
        }
        else if (currentAlertLevel > 25)
        {
            // ����ĵ�١G�h�áA�}�l�j��
            Debug.Log("ALERT LEVEL MEDIUM: Searching for target...");
        }
        else
        {
            // �C��ĵ�١G�^�쨵��
        }
    }
}