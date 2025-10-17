using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    [Header("視野參數")]
    [Tooltip("視野的半徑")]
    public float viewRadius = 10f;
    [Tooltip("視野的角度 (0-360)")]
    [Range(0, 360)]
    public float viewAngle = 90f;

    [Header("圖層遮罩")]
    [Tooltip("指定要偵測的目標所在的圖層 (例如玩家物件)")]
    public LayerMask targetMask;
    [Tooltip("指定會阻擋視野的障礙物所在的圖層 (例如牆壁、家具)")]
    public LayerMask obstacleMask;

    // --- 公開的偵測結果 ---
    [HideInInspector]
    public List<Transform> visibleTargets = new List<Transform>();

    void Start()
    {
        // 為了效能，我們可以用一個協程來固定頻率偵測，而不是在每一幀都做
        StartCoroutine("FindTargetsWithDelay", .2f);
    }

    IEnumerator FindTargetsWithDelay(float delay)
    {
        while (true)
        {
            yield return new WaitForSeconds(delay);
            FindVisibleTargets();
        }
    }

    void FindVisibleTargets()
    {
        // 先清空上一幀的結果
        visibleTargets.Clear();

        // 步驟 1: 找出在視野半徑內的所有目標
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            Vector3 dirToTarget = (target.position - transform.position).normalized;

            // 步驟 2: 檢查目標是否在視野角度內
            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
            {
                float distToTarget = Vector3.Distance(transform.position, target.position);

                // 步驟 3: 發射射線，檢查是否有障礙物
                // 如果射線沒有打到任何障礙物，就代表目標是可見的
                if (!Physics.Raycast(transform.position, dirToTarget, distToTarget, obstacleMask))
                {
                    visibleTargets.Add(target);
                }
            }
        }
    }

    // 在 Scene 視窗中畫出視野範圍，方便關卡設計和除錯
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);

        Gizmos.color = Color.red;
        foreach (Transform visibleTarget in visibleTargets)
        {
            Gizmos.DrawLine(transform.position, visibleTarget.position);
        }
    }

    // 根據角度計算方向向量的輔助函式
    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}