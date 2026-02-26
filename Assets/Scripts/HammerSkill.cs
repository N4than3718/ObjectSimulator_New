using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HammerSkill : BaseSkill
{
    [Header("發射設定")]
    [SerializeField] private float minForce = 5f;
    [SerializeField] private float maxForce = 20f;
    [SerializeField] private float chargeTime = 1.5f; // 蓄滿所需時間
    [SerializeField] private float upwardAngle = 30f; // 預設往上拋的角度

    [Header("軌跡視覺")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private int linePoints = 30;     // 預測的點數量
    [SerializeField] private float timeBetweenPoints = 0.1f; // 點的密度

    [Header("狀態與引用")]
    [SerializeField] private PlayerMovement playerMovement;

    private Rigidbody rb;
    private bool isCharging = false;
    private float currentChargeTimer = 0f;
    private bool isFlying = false;

    protected override void Activate()
    { }

    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();

        if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);
    }

    public override void OnInput(InputAction.CallbackContext context)
    {
        if (context.started && !isFlying)
        {
            // 開始蓄力
            isCharging = true;
            currentChargeTimer = 0f;
            if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(true);
        }
        else if (context.canceled && isCharging)
        {
            // 釋放發射
            isCharging = false;
            if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);
            Launch();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isCharging)
        {
            // 累加蓄力時間
            currentChargeTimer += Time.deltaTime;
            currentChargeTimer = Mathf.Clamp(currentChargeTimer, 0, chargeTime);

            // 畫拋物線
            DrawTrajectory();
        }
    }

    private Vector3 CalculateLaunchVelocity()
    {
        // 計算當前蓄力比例
        float chargePercent = currentChargeTimer / chargeTime;
        float currentForce = Mathf.Lerp(minForce, maxForce, chargePercent);

        // 取得攝影機面向的方向 (如果沒有攝影機，就用自己前方的方向)
        Transform aimTransform = Camera.main != null ? Camera.main.transform : transform;
        Vector3 aimDirection = aimTransform.forward;

        // 稍微往上抬一個角度，形成拋物線
        Vector3 launchDirection = Quaternion.AngleAxis(-upwardAngle, aimTransform.right) * aimDirection;

        // F = ma -> V = F/m (如果我們使用 VelocityChange，質量影響會自動處理，這裡直接算出初速向量)
        return launchDirection.normalized * currentForce;
    }

    private void DrawTrajectory()
    {
        if (trajectoryLine == null) return;

        Vector3 startPos = transform.position;
        Vector3 velocity = CalculateLaunchVelocity();
        trajectoryLine.positionCount = linePoints;

        // 💀 物理公式：S = V0*t + 0.5*g*t^2
        for (int i = 0; i < linePoints; i++)
        {
            float t = i * timeBetweenPoints;
            Vector3 pointPosition = startPos + velocity * t + 0.5f * Physics.gravity * (t * t);
            trajectoryLine.SetPosition(i, pointPosition);

            // 如果預測點撞到地板或牆壁，就中斷後面的線條
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

        // 1. 關閉玩家的走路控制 (必須在 PlayerMovement 裡加一個 isFlying 判斷)
        if (playerMovement != null) playerMovement.enabled = false;

        // 2. 施加發射力道 (使用 VelocityChange 忽略質量，讓拋物線更符合預測)
        Vector3 launchVelocity = CalculateLaunchVelocity();
        rb.linearVelocity = launchVelocity;

        // 3. ✨ Juice: 加入隨機旋轉，讓槌子在空中翻滾
        rb.AddTorque(new Vector3(Random.Range(-5f, 5f), Random.Range(10f, 20f), 0), ForceMode.Impulse);

        Debug.Log($"[HammerSkill] 發射！力度: {launchVelocity.magnitude}");
    }
}