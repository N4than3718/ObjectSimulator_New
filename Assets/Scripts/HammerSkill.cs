using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HammerSkill : BaseSkill
{
    [Header("憤怒鳥彈弓設定")]
    [Tooltip("拉滿橡皮筋時的最大發射力道")]
    [SerializeField] private float maxForce = 25f;
    [Tooltip("滑鼠要拉多遠才能達到最大力道？")]
    [SerializeField] private float maxDragDistance = 300f;
    [Tooltip("力道加乘係數")]
    [SerializeField] private float forceMultiplier = 3f;

    [Header("軌跡視覺")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private int linePoints = 30;
    [SerializeField] private float timeBetweenPoints = 0.1f;

    [Header("擊暈設定")]
    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private AudioClip impactSound;

    [Header("狀態與引用")]
    [SerializeField] private PlayerMovement playerMovement;

    private Rigidbody rb;
    private Camera mainCam;

    // 拖曳狀態追蹤
    private bool isDragging = false;
    private Vector2 accumulatedDrag;
    private Vector2 dragStartPos;
    private Vector2 currentDragPos;
    private bool isFlying = false;

    protected override void Activate()
    {   }

    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();

        if (trajectoryLine != null) trajectoryLine.enabled = false;
    }

    protected override void Update()
    {
        base.Update();

        // 💀 核心邏輯：偵測滑鼠拖曳 (拉彈弓)
        HandleDragInput();
    }

    private void HandleDragInput()
    {
        if (isFlying || Mouse.current == null) return;

        // 1. 按下左鍵：開始拉弓
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isDragging = true;
            accumulatedDrag = Vector2.zero; // 💀 每次拉弓前，把拖曳量歸零

            if (trajectoryLine != null) trajectoryLine.enabled = true;
            if (playerMovement != null) playerMovement.enabled = false;
        }

        // 2. 按住左鍵拖曳：更新拋物線預覽
        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            // 💀 關鍵修改：用 Delta (位移量) 不斷累加，而不是讀取死板的 Position！
            accumulatedDrag += Mouse.current.delta.ReadValue();
            DrawTrajectory();
        }

        // 3. 放開左鍵：發射！
        if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);
            Launch();
        }
    }

    // 💀 計算發射向量 (滑鼠往後拉，力量往前推)
    private Vector3 CalculateLaunchVelocity()
    {
        // 💀 關鍵修改：在憤怒鳥裡，滑鼠往「下」拉 (負數)，槌子要往「前」飛。
        // 所以我們直接把累積的拖曳量加上負號反轉！
        Vector2 dragVector = -accumulatedDrag;

        // --- 下面的都不用動，維持原樣 ---
        float dragDistance = Mathf.Clamp(dragVector.magnitude, 0, maxDragDistance);
        float forcePercent = dragDistance / maxDragDistance;
        float finalForce = forcePercent * maxForce * forceMultiplier;

        Vector3 aimDirection = mainCam.transform.forward * dragVector.y + mainCam.transform.right * dragVector.x;
        if (aimDirection.magnitude < 0.1f) aimDirection = mainCam.transform.forward;

        Vector3 launchDirection = Quaternion.AngleAxis(-30f, mainCam.transform.right) * aimDirection.normalized;

        return launchDirection * finalForce;
    }

    private void DrawTrajectory()
    {
        if (trajectoryLine == null) return;

        Vector3 startPos = transform.position;
        Vector3 velocity = CalculateLaunchVelocity();
        trajectoryLine.positionCount = linePoints;

        // 物理預測公式：S = V0*t + 0.5*g*t^2
        for (int i = 0; i < linePoints; i++)
        {
            float t = i * timeBetweenPoints;
            Vector3 pointPosition = startPos + velocity * t + 0.5f * Physics.gravity * (t * t);
            trajectoryLine.SetPosition(i, pointPosition);

            // 預測撞擊點中斷線條
            if (i > 0)
            {
                Vector3 lastPos = trajectoryLine.GetPosition(i - 1);
                if (Physics.Raycast(lastPos, (pointPosition - lastPos).normalized, out RaycastHit hit, Vector3.Distance(lastPos, pointPosition)))
                {
                    trajectoryLine.positionCount = i + 1;
                    trajectoryLine.SetPosition(i, hit.point);
                    break;
                }
            }
        }
    }

    private void Launch()
    {
        isFlying = true;

        Vector3 launchVelocity = CalculateLaunchVelocity();

        // 使用 VelocityChange 忽略質量，讓拋物線更精準
        rb.AddForce(launchVelocity, ForceMode.VelocityChange);

        // 翻滾特效
        rb.AddTorque(new Vector3(Random.Range(-5f, 5f), Random.Range(10f, 20f), 0), ForceMode.Impulse);

        Debug.Log($"[HammerSkill] 憤怒彈射！初速: {launchVelocity.magnitude}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isFlying) return;

        isFlying = false;

        // 落地後恢復玩家移動能力
        if (playerMovement != null) playerMovement.enabled = true;

        // --- 以下是你原本寫的擊暈與特效邏輯 (保持不變) ---
        if (impactEffect != null) Instantiate(impactEffect, collision.contacts[0].point, Quaternion.identity);
        if (impactSound != null && GetComponent<AudioSource>() != null) GetComponent<AudioSource>().PlayOneShot(impactSound);

        NpcAI hitNpc = collision.collider.GetComponentInParent<NpcAI>();
        if (hitNpc != null)
        {
            Debug.Log($"[HammerSkill] 爆頭！砸暈了 {hitNpc.name}");
            // hitNpc.GetStunned(stunDuration); // 等你的 NPC 寫好再解開
        }
        else
        {
            Debug.Log("[HammerSkill] 沒砸中目標，發出巨大噪音！");
            // StealthManager.MakeNoise(gameObject, transform.position, 20f, 1f);
        }
    }
}