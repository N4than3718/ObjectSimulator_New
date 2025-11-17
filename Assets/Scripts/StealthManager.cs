using UnityEngine;
using System.Collections.Generic;

// 靜態類別，全域皆可存取
public static class StealthManager
{
    // 儲存所有場景中的 NPC，避免每次都要 FindObject 或 OverlapSphere
    private static List<NpcAI> allNpcs = new List<NpcAI>();

    // --- NPC 註冊系統 ---
    // NpcAI.cs 在 Start() 時呼叫
    public static void RegisterNpc(NpcAI npc)
    {
        if (!allNpcs.Contains(npc))
        {
            allNpcs.Add(npc);
            // Debug.Log($"StealthManager: NPC {npc.name} registered. Total: {allNpcs.Count}");
        }
    }

    // NpcAI.cs 在 OnDestroy() 時呼叫
    public static void UnregisterNpc(NpcAI npc)
    {
        if (allNpcs.Contains(npc))
        {
            allNpcs.Remove(npc);
        }
    }

    // --- 核心功能：製造噪音 (合併了 NoiseManager 的功能) ---
    /// <summary>
    /// 在指定位置製造噪音，通知所有範圍內的 NPC
    /// </summary>
    /// <param name="position">聲音來源位置</param>
    /// <param name="range">聲音傳播半徑 (聽覺範圍)</param>
    /// <param name="intensity">聲音強度 (影響警戒值增加量)</param>
    public static void MakeNoise(Vector3 position, float range, float intensity)
    {
        // (可選) 在 Editor 裡畫個圈圈 Debug 用
        // Debug.DrawRay(position, Vector3.up * 2, Color.cyan, 1f);

        // 遍歷所有已註冊的 NPC
        // 這種方法比 Physics.OverlapSphere 更快，因為我們只檢查相關的 NPC，而不是場景裡所有的 Collider
        for (int i = allNpcs.Count - 1; i >= 0; i--)
        {
            NpcAI npc = allNpcs[i];

            // 防呆：如果 NPC 被刪除了但沒取消註冊
            if (npc == null)
            {
                allNpcs.RemoveAt(i);
                continue;
            }

            // 計算距離
            float distance = Vector3.Distance(npc.transform.position, position);
            float effectiveHearingRange = range * npc.HearingSensitivity;

            // 判斷 NPC 是否在噪音半徑內
            if (distance <= effectiveHearingRange)
            {
                // NPC 聽到了！呼叫 NPC 自己的處理方法
                // 這裡可以加入更複雜的邏輯，例如根據距離衰減強度
                // float attenuatedIntensity = intensity * (1 - (distance / range)); 

                npc.HearNoise(position, range, intensity);
            }
        }
    }

    // 為了相容性，保留舊的 ReportNoise 方法，但導向 MakeNoise
    // 這樣你還沒改到的 ImpactSoundEmitter 也不會報錯
    public static void ReportNoise(Vector3 sourcePosition, float radius)
    {
        // 預設給一個強度，例如 radius * 2
        MakeNoise(sourcePosition, radius, radius * 2f);
    }
}