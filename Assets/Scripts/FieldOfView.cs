using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    [Header("�����Ѽ�")]
    [Tooltip("�������b�|")]
    public float viewRadius = 10f;
    [Tooltip("���������� (0-360)")]
    [Range(0, 360)]
    public float viewAngle = 90f;

    [Header("�ϼh�B�n")]
    [Tooltip("���w�n�������ؼЩҦb���ϼh (�Ҧp���a����)")]
    public LayerMask targetMask;
    [Tooltip("���w�|���׵�������ê���Ҧb���ϼh (�Ҧp����B�a��)")]
    public LayerMask obstacleMask;

    // --- ���}���������G ---
    [HideInInspector]
    public List<Transform> visibleTargets = new List<Transform>();

    void Start()
    {
        // ���F�į�A�ڭ̥i�H�Τ@�Ө�{�өT�w�W�v�����A�Ӥ��O�b�C�@�V����
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
        // ���M�ŤW�@�V�����G
        visibleTargets.Clear();

        // �B�J 1: ��X�b�����b�|�����Ҧ��ؼ�
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            Vector3 dirToTarget = (target.position - transform.position).normalized;

            // �B�J 2: �ˬd�ؼЬO�_�b�������פ�
            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
            {
                float distToTarget = Vector3.Distance(transform.position, target.position);

                // �B�J 3: �o�g�g�u�A�ˬd�O�_����ê��
                // �p�G�g�u�S����������ê���A�N�N��ؼЬO�i����
                if (!Physics.Raycast(transform.position, dirToTarget, distToTarget, obstacleMask))
                {
                    visibleTargets.Add(target);
                }
            }
        }
    }

    // �b Scene �������e�X�����d��A��K���d�]�p�M����
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

    // �ھڨ��׭p���V�V�q�����U�禡
    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}