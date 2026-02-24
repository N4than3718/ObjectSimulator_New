using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightSkill : BaseSkill
{
    [Header("手電筒設定")]
    [SerializeField] private Light spotlight; // 拖曳聚光燈元件
    [SerializeField] private Transform rotatingPart; // [新增] 指定要旋轉的部分 (如果是 null 則自動設為 spotlight 的 transform)
    [SerializeField] private AudioClip clickSound; // 開關音效
    [SerializeField] private AudioSource audioSource;

    [Header("操作設定")]
    [SerializeField] private float holdThreshold = 0.25f; // 超過 0.25秒 視為長按
    [SerializeField] private float rotateSpeed = 15f;     // 轉向準星的速度

    [Header("致盲機制")] // ▼▼▼ [新增]
    [SerializeField] private float effectiveRange = 10f; // 有效距離
    [SerializeField] private float effectiveAngle = 45f; // 有效角度 (通常比聚光燈的 Spot Angle 小一點)
    [SerializeField] private LayerMask targetLayer;      // NPC 所在的 Layer (例如 "Default" 或 "NPC")
    [SerializeField] private LayerMask obstructionLayer; // 牆壁的 Layer (用來判斷遮擋)
    [SerializeField] private float checkInterval = 0.2f; // 不要每幀檢查，省效能

    // 狀態變數
    private bool isHolding = false;
    private float holdTimer = 0f;
    private Quaternion defaultRotation; // 用來復原角度
    private TeamManager teamManager;    // 用來取得攝影機
    private float timer = 0f;
    private Collider[] _hitColliders = new Collider[10];

    public override void OnInput(InputAction.CallbackContext context)
    {
        // 1. 按下瞬間 (Started)
        if (context.started)
        {
            isHolding = true;
            holdTimer = 0f;
        }

        // 2. 放開瞬間 (Canceled)
        if (context.canceled)
        {
            isHolding = false;

            // 如果按住時間很短，視為「點擊切換」
            if (holdTimer < holdThreshold)
            {
                TryActivate(); // 觸發開關
            }

            // 放開後，復原角度 (回到正前方)
            if (rotatingPart != null)
            {
                // 這裡可以用 Coroutine 做平滑復原，這裡先直接復原
                // rotatingPart.localRotation = defaultRotation; 
            }
        }
        // 注意：我們忽略 Performed，因為我們自己處理 Started/Canceled
    }

    // 覆寫 Activate 方法，定義具體行為
    protected override void Activate()
    {
        if (spotlight != null)
        {
            // 切換開關
            spotlight.enabled = !spotlight.enabled;
            Debug.Log($"手電筒已 {(spotlight.enabled ? "開啟" : "關閉")}");

            // 播放音效
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }

            // 這裡發出一個小小的聲音訊號給 AI (開關聲)
            StealthManager.MakeNoise(gameObject, transform.position, 5f, 2f);
        }
        else
        {
            Debug.LogWarning("FlashlightSkill: 缺少 Light 元件!");
        }
    }

    protected override void Update()
    {
        base.Update(); // 保留 BaseSkill 的冷卻邏輯

        if (isHolding)
        {
            holdTimer += Time.deltaTime;

            // 如果按住超過門檻，開始瞄準
            if (holdTimer >= holdThreshold)
            {
                AimAtCrosshair();

                // (可選) 長按時強迫開燈？
                if (spotlight != null && !spotlight.enabled) spotlight.enabled = true;
            }
        }
        else
        {
            // 沒按住時，平滑復原角度
            if (rotatingPart != null)
            {
                rotatingPart.localRotation = Quaternion.Slerp(rotatingPart.localRotation, defaultRotation, Time.deltaTime * 5f);
            }
        }

        // 只有燈亮著的時候才檢測
        if (spotlight != null && spotlight.enabled)
        {
            timer += Time.deltaTime;
            if (timer >= checkInterval)
            {
                CheckForTargets();
                timer = 0f;
            }
        }
    }

    // 瞄準準星邏輯
    private void AimAtCrosshair()
    {
        if (teamManager == null || teamManager.CurrentCameraTransform == null || rotatingPart == null) return;

        Transform cam = teamManager.CurrentCameraTransform;

        // 從攝影機發射射線
        Ray ray = new Ray(cam.position, cam.forward);
        Vector3 targetPoint;

        // 如果射線打到東西 (忽略 Player 自己，這裡假設 obstructionLayer 包含地板牆壁)
        if (Physics.Raycast(ray, out RaycastHit hit, 50f, obstructionLayer))
        {
            targetPoint = hit.point;
        }
        else
        {
            // 沒打到東西，就看向遠方的一點
            targetPoint = cam.position + cam.forward * 50f;
        }

        Vector3 directionToTarget = targetPoint - transform.position;

        // 讓 rotatingPart 看向那個點
        Quaternion lookRot = Quaternion.LookRotation(directionToTarget);
        Quaternion correctedRot = lookRot * Quaternion.Euler(90f, 0f, 0f);

        transform.rotation = Quaternion.Slerp(transform.rotation, correctedRot, Time.deltaTime * rotateSpeed);
    }

    private void CheckForTargets()
    {
        if (spotlight == null) return;
        Transform origin = spotlight.transform;

        Collider[] hits = Physics.OverlapSphere(origin.position, effectiveRange, targetLayer);

        foreach (var hit in hits)
        {
            // 假設你的 NPC 腳本叫做 NpcAI (請根據你的實際檔名修改)
            NpcAI npc = hit.GetComponentInParent<NpcAI>();
            if (npc == null) continue;

            // 1. 安全抓取弱點中心 (防呆：如果沒綁定 eyeTransform，就用頭頂 1.0f 處)
            Vector3 targetCenter = npc.eyeTransform != null ? npc.eyeTransform.position : hit.transform.position + Vector3.up * 1.0f;

            // 2. 修正角度計算：使用 origin.forward 而不是 transform.forward！
            Vector3 directionToTarget = targetCenter - origin.position;
            if (Vector3.Angle(origin.forward, directionToTarget) > effectiveAngle / 2f)
            {
                continue; // 角度不對，跳過
            }

            // 3. 修正射線起點：使用 origin.position！
            if (!Physics.Linecast(origin.position, targetCenter, obstructionLayer))
            {
                // 🟢 成功：畫綠線並致盲
                Debug.DrawLine(origin.position, targetCenter, Color.green, 0.5f);
                npc.GetBlinded(origin.position);
            }
            else
            {
                // 🔴 失敗：畫紅線 (被牆壁或自己擋住)
                Debug.DrawLine(origin.position, targetCenter, Color.red, 0.5f);
            }
        }
    }

    // 覆寫初始化，確保一開始的狀態
    protected override void Start()
    {
        base.Start();

        if (spotlight == null) spotlight = GetComponentInChildren<Light>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();

        // 如果沒指定旋轉部件，就預設轉 Light 自己
        if (rotatingPart == null && spotlight != null) rotatingPart = spotlight.transform;

        // 記錄初始角度 (假設初始是朝向正前方)
        if (rotatingPart != null) defaultRotation = rotatingPart.localRotation;

        if (spotlight != null)
        {
            effectiveRange = spotlight.range;
            effectiveAngle = spotlight.spotAngle / 2f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spotlight == null) return;
        Transform origin = spotlight.transform;

        // 畫出距離
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(origin.position, effectiveRange);

        // 畫出致盲判定圓錐的邊界線
        Gizmos.color = Color.yellow;
        Vector3 forward = origin.forward * effectiveRange;

        Quaternion leftRayRotation = Quaternion.AngleAxis(-effectiveAngle / 2f, origin.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(effectiveAngle / 2f, origin.up);
        Quaternion upRayRotation = Quaternion.AngleAxis(-effectiveAngle / 2f, origin.right);
        Quaternion downRayRotation = Quaternion.AngleAxis(effectiveAngle / 2f, origin.right);

        Gizmos.DrawRay(origin.position, leftRayRotation * forward);
        Gizmos.DrawRay(origin.position, rightRayRotation * forward);
        Gizmos.DrawRay(origin.position, upRayRotation * forward);
        Gizmos.DrawRay(origin.position, downRayRotation * forward);

        // 畫出正中心的光軸
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin.position, origin.forward * effectiveRange);
    }
}