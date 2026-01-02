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
                // if (spotlight != null && !spotlight.enabled) spotlight.enabled = true;
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
        // 1. 找出範圍內的所有 NPC
        Collider[] hits = Physics.OverlapSphere(transform.position, effectiveRange, targetLayer);

        foreach (var hit in hits)
        {
            // 嘗試取得 NpcAI 組件 (考慮到碰撞體可能在子物件或父物件)
            NpcAI npc = hit.GetComponentInParent<NpcAI>();

            if (npc != null)
            {
                // 2. 計算角度：目標是否在手電筒前方？
                Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
                // 注意：手電筒通常朝向 transform.forward，但也可能是 transform.up，視模型而定
                float angle = Vector3.Angle(transform.up, directionToTarget);

                if (angle < effectiveAngle)
                {
                    // 計算 "NPC -> 手電筒" 的方向
                    Vector3 dirToLight = (rotatingPart.position - hit.transform.position).normalized;
                    // 計算 NPC 正面 與 光源方向 的夾角
                    // 假設 NPC 眼睛視野是 120 度 (左右各 60 度)
                    float lookAngle = Vector3.Angle(npc.transform.forward, dirToLight);

                    // 如果夾角大於 80 度 (代表光源在他側面或背面)，就不算致盲
                    if (lookAngle > 80f)
                    {
                        // Debug.Log("NPC 背對光源，無效！");
                        continue; // 跳過，不執行致盲
                    }

                    // 3. 射線檢測 (Raycast) 判斷遮擋
                    // 從燈泡位置射向 NPC 的頭部或胸部 (這裡假設 pivot 在腳底，稍微往上抬一點)
                    Vector3 targetCenter = hit.transform.position + Vector3.up * 1.0f;
                    float distance = Vector3.Distance(transform.position, targetCenter);

                    if (!Physics.Linecast(transform.position, targetCenter, obstructionLayer))
                    {
                        // 通過所有檢查：致盲 NPC！
                        npc.GetBlinded(transform.position);
                    }
                }
            }
        }
    }

    // 覆寫初始化，確保一開始的狀態
    private void Start()
    {
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
}