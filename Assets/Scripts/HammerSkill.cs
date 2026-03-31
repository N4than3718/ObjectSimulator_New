using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HammerSkill : BaseSkill
{
    [Header("發射設定")]
    [Tooltip("按下按鍵後，延遲幾秒才開始蓄力？")]
    [SerializeField] private float chargeDelay = 1.5f;
    [SerializeField] private float minForce = 5f;
    [SerializeField] private float maxForce = 20f;
    [SerializeField] private float chargeTime = 5f;

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
    [SerializeField] private Transform aimCamera;

    private Rigidbody rb;
    private bool isCharging = false;
    private bool isFlying = false;

    // 計時器統整
    private float holdTimer = 0f;
    private float currentForce = 0f;

    protected override void Activate() { }

    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();

        if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);
    }

    // 💀 刪除了原本衝突的 OnInput 方法，統一將輸入邏輯交給 Update 處理

    protected override void Update()
    {
        base.Update();

        // 統一在這裡處理輸入與蓄力
        HandleChargeInput();
    }

    private void HandleChargeInput()
    {
        if (isFlying || Mouse.current == null) return;

        // 1. 剛按下左鍵：初始化所有狀態
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isCharging = true;
            holdTimer = 0f;
            currentForce = minForce; // 鎖定在基礎力道

            if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(true);
        }

        // 2. 按住左鍵不放：跑計時器與畫線
        if (isCharging && Mouse.current.leftButton.isPressed)
        {
            holdTimer += Time.deltaTime;

            // 超過 1.5 秒延遲後，開始增加力道
            if (holdTimer >= chargeDelay)
            {
                // 計算扣除延遲後，真正用來蓄力的時間
                float actualChargeTime = holdTimer - chargeDelay;
                float chargePercent = actualChargeTime / chargeTime;

                // 力道逐漸增加，最大不超過 maxForce
                currentForce = Mathf.Lerp(minForce, maxForce, chargePercent);
            }

            // 每幀更新預覽線
            DrawTrajectory();
        }

        // 3. 放開左鍵：發射
        if (isCharging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isCharging = false;
            if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);

            Launch();
            holdTimer = 0f; // 重置
        }
    }

    private Vector3 CalculateLaunchVelocity()
    {
        Vector3 horizontalDirection = aimCamera != null ? aimCamera.forward : transform.forward;
        horizontalDirection.y = 0;
        horizontalDirection.Normalize();

        float pitchAngle = aimCamera != null ? aimCamera.eulerAngles.x : transform.eulerAngles.x;
        if (pitchAngle > 180f) pitchAngle -= 360f;

        float launchAngle = -pitchAngle;
        launchAngle = Mathf.Clamp(launchAngle, -80f, 85f);

        float radianAngle = launchAngle * Mathf.Deg2Rad;

        float verticalSpeed = currentForce * Mathf.Sin(radianAngle);
        float horizontalSpeed = currentForce * Mathf.Cos(radianAngle);

        Vector3 finalVelocity = (horizontalDirection * horizontalSpeed) + (Vector3.up * verticalSpeed);
        return finalVelocity;
    }

    private void DrawTrajectory()
    {
        if (trajectoryLine == null) return;

        // 💀 1. 起點偏移：不要從肚子裡發射！
        // 順著攝影機方向，把畫線的起點稍微往前推 0.1 個單位，避開自己的碰撞體
        Vector3 forwardOffset = aimCamera != null ? aimCamera.forward : transform.forward;
        Vector3 startPos = transform.position + (forwardOffset * 0.1f);

        Vector3 velocity = CalculateLaunchVelocity();
        trajectoryLine.positionCount = linePoints;

        // 💀 2. 設定 LayerMask (碰撞遮罩)
        // 這行程式碼的意思是：「除了 Layer 是 Player 的東西之外，其他全撞」
        // 這樣射線就不會被槌子自己的 BoxCollider 擋住了！
        int layerMask = ~LayerMask.GetMask("Player");

        for (int i = 0; i < linePoints; i++)
        {
            float t = i * timeBetweenPoints;
            Vector3 pointPosition = startPos + velocity * t + 0.5f * Physics.gravity * (t * t);
            trajectoryLine.SetPosition(i, pointPosition);

            if (i > 0)
            {
                Vector3 lastPos = trajectoryLine.GetPosition(i - 1);

                // 💀 3. 把 layerMask 加進 Raycast 裡面！
                if (Physics.Raycast(lastPos, (pointPosition - lastPos).normalized, out RaycastHit hit, Vector3.Distance(lastPos, pointPosition), layerMask))
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

        if (playerMovement != null) playerMovement.isFlying = true;

        Vector3 launchVelocity = CalculateLaunchVelocity();

        // 💀 改回 AddForce (VelocityChange)，這在 Unity 物理引擎中通常比直接設定 linearVelocity 更穩定
        rb.AddForce(launchVelocity, ForceMode.VelocityChange);

        rb.AddTorque(new Vector3(Random.Range(-5f, 5f), Random.Range(10f, 20f), 0), ForceMode.Impulse);

        Debug.Log($"[HammerSkill] 發射！力度: {launchVelocity.magnitude}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isFlying) return;

        isFlying = false;
        if (playerMovement != null) playerMovement.isFlying = false;

        if (impactEffect != null) Instantiate(impactEffect, collision.contacts[0].point, Quaternion.identity);
        if (impactSound != null && GetComponent<AudioSource>() != null) GetComponent<AudioSource>().PlayOneShot(impactSound);

        NpcAI hitNpc = collision.collider.GetComponentInParent<NpcAI>();
        if (hitNpc != null)
        {
            Debug.Log($"[HammerSkill] 爆頭！砸暈了 {hitNpc.name}");
            hitNpc.GetStunned(stunDuration);
        }
        else
        {
            Debug.Log("[HammerSkill] 沒砸中目標，發出巨大噪音！");
        }
    }
}