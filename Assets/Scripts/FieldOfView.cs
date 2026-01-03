using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    [Header("視野參數")]
    public float viewRadius = 10f;
    [Range(0, 360)]
    public float viewAngle = 90f;
    [Tooltip("眼睛的高度偏移量 (避免從腳底發射射線)")]
    public Vector3 eyeOffset = new Vector3(0, 1.6f, 0); // 預設 1.6米高度

    [Header("圖層遮罩")]
    public LayerMask targetMask;
    public LayerMask obstacleMask;

    [HideInInspector]
    public List<Transform> visibleTargets = new List<Transform>();

    private Collider[] _targetBuffer = new Collider[10]; // 最多偵測 10 個目標，夠用了

    void Start()
    {
        StartCoroutine(FindTargetsWithDelay(0.2f)); // 每 0.2 秒檢查一次
    }

    IEnumerator FindTargetsWithDelay(float delay)
    {
        while (true)
        {
            FindVisibleTargets();
            yield return new WaitForSeconds(delay);
        }
    }

    void FindVisibleTargets()
    {
        visibleTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, viewRadius, _targetBuffer, targetMask);

        Vector3 eyePos = transform.position + transform.rotation * eyeOffset; // 考慮旋轉 (如果 NPC 會趴下)

        for (int i = 0; i < count; i++)
        {
            Transform target = _targetBuffer[i].transform;

            if (target.root == transform.root) continue;

            Vector3 targetCenter = target.position + Vector3.up * 0.5f; // 假設目標中心高 0.5
            Vector3 dirToTarget = (targetCenter - eyePos).normalized;

            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
            {
                float distToTarget = Vector3.Distance(transform.position, target.position);
                if (!Physics.Raycast(eyePos, dirToTarget, distToTarget, obstacleMask))
                {
                    visibleTargets.Add(target);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 eyePos = transform.position + eyeOffset; // 簡單視覺化

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(eyePos, eyePos + viewAngleA * viewRadius);
        Gizmos.DrawLine(eyePos, eyePos + viewAngleB * viewRadius);

        Gizmos.color = Color.red;
        foreach (Transform visibleTarget in visibleTargets)
        {
            Gizmos.DrawLine(eyePos, visibleTarget.position);
        }
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}