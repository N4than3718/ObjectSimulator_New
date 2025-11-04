using UnityEngine;
using System.Collections.Generic; // 需要這個來儲存 NPC 列表

// 3. 中央潛行管理器 (StealthManager.cs) - 靜態類別 (Static Class)
public static class StealthManager
{
    private static List<NpcAI> allNpcs = new List<NpcAI>();

    // (你的 NpcAI.cs 腳本需要在 Start() 時呼叫這個來註冊自己)
    public static void RegisterNpc(NpcAI npc)
    {
        if (!allNpcs.Contains(npc))
            allNpcs.Add(npc);
    }

    // (NpcAI.cs 在 OnDestroy() 時呼叫這個來取消註冊)
    public static void UnregisterNpc(NpcAI npc)
    {
        if (allNpcs.Contains(npc))
            allNpcs.Remove(npc);
    }

    // 核心功能：接收並處理噪音事件
    public static void ReportNoise(Vector3 sourcePosition, float radius)
    {
        // 遍歷所有已註冊的 NPC
        foreach (NpcAI npc in allNpcs)
        {
            float distance = Vector3.Distance(npc.transform.position, sourcePosition);

            // 判斷 NPC 是否在噪音半徑內
            if (distance <= radius)
            {
                // NPC 聽到了！
                // 根據距離和噪音強度 (radius)，增加 NPC 的警戒值

                // 簡易計算：離越近，加越多
                float calculatedAlertness = (radius - distance) * 10.0f; // (10.0f 是可調整的靈敏度)

                // 呼叫 NpcAI 上的公共方法 (我們假設你 NpcAI 上有這個方法)
                // npc.HearNoise(calculatedAlertness, sourcePosition);
            }
        }
    }
}