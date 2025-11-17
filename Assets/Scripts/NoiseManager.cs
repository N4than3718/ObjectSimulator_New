using UnityEngine;

public static class NoiseManager
{
    /// <summary>
    /// 在指定位置製造噪音
    /// </summary>
    /// <param name="position">聲音來源位置</param>
    /// <param name="range">聲音傳播半徑 (越大越吵)</param>
    /// <param name="intensity">聲音強度 (影響警戒值增加量)</param>
    public static void MakeNoise(Vector3 position, float range, float intensity)
    {
        // 1. 找出範圍內所有的 Collider
        // 使用 NonAlloc 版本效能更好，但在這裡為了方便先用簡單的 OverlapSphere
        Collider[] colliders = Physics.OverlapSphere(position, range);

        foreach (var collider in colliders)
        {
            // 2. 檢查是否有 NpcAI 元件
            NpcAI npc = collider.GetComponent<NpcAI>();

            // 如果 collider 是身體部位，嘗試找 root 或 parent
            if (npc == null) npc = collider.GetComponentInParent<NpcAI>();

            if (npc != null)
            {
                // 3. 通知 NPC 聽到聲音了
                npc.HearNoise(position, range, intensity);
            }
        }

        // (可選) 在 Editor 裡畫個圈圈 Debug 用
        // Debug.DrawRay(position, Vector3.up, Color.cyan, 1f);
    }
}