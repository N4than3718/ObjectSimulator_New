using UnityEngine;
using UnityEngine.AI; // 引用 NavMesh

[RequireComponent(typeof(NavMeshAgent), typeof(Collider), typeof(Rigidbody))]
public class NpcBodyPush : MonoBehaviour
{
    [Header("推力設定")]
    [Tooltip("推開物體的力道倍率")]
    [SerializeField] private float pushPower = 2.0f;

    [Tooltip("可以推動的 Layer (避免推到地板或牆壁)")]
    [SerializeField] private LayerMask pushLayers;

    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // 當 NPC 的 Collider 碰到別人的 Collider 時觸發
    private void OnCollisionStay(Collision collision)
    {
        // 1. 檢查是否在推動名單內 (使用 LayerMask)
        // (1 << collision.gameObject.layer) 是位元運算，用來比對 Layer
        if (((1 << collision.gameObject.layer) & pushLayers) == 0) return;

        // 2. 取得對方的 Rigidbody
        Rigidbody targetRb = collision.gameObject.GetComponent<Rigidbody>();

        // 3. 如果對方有 RB 且不是 Kinematic (是會動的物體)
        if (targetRb != null && !targetRb.isKinematic)
        {
            // 4. 計算推力方向
            Vector3 pushDir = collision.transform.position - transform.position;
            pushDir.y = 0; // 只推水平方向，不要把東西推飛上天
            pushDir.Normalize();

            // 5. 施加力道
            // 力道 = 設定值 * NPC 當前速度 (走越快撞越大力)
            float currentSpeed = agent.velocity.magnitude;

            // 使用 ForceMode.VelocityChange 可以忽略物體質量差異，推起來手感比較像 "強行撥開"
            // 或者用 ForceMode.Force 比較符合物理 (重物推不動)
            targetRb.AddForce(pushDir * pushPower * currentSpeed, ForceMode.Force);
        }
    }
}