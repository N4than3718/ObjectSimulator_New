using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TensionBuilder : MonoBehaviour
{
    [Header("偵測設定")]
    [Tooltip("開始感覺到緊張的最遠距離")]
    [SerializeField] private float maxDistance = 15f;
    [Tooltip("心跳最大聲、最急促的極限距離 (快被抓到了！)")]
    [SerializeField] private float minDistance = 3f;

    [Header("音效果汁 (Game Juice)")]
    [Tooltip("最遠距離時的音量 (通常設為 0)")]
    [SerializeField] private float minVolume = 0f;
    [Tooltip("最近距離時的最大音量")]
    [SerializeField] private float maxVolume = 1f;

    [Tooltip("最遠距離時的播放速度 (Pitch)")]
    [SerializeField] private float minPitch = 0.8f;
    [Tooltip("最近距離時的心跳速度 (越快越緊張！)")]
    [SerializeField] private float maxPitch = 1.6f;

    private AudioSource heartSource;
    private NpcAI[] allNpcs; // 儲存場景內所有守衛

    private void Start()
    {
        heartSource = GetComponent<AudioSource>();

        // 確保心跳聲是無限循環的，並且一開始是靜音
        heartSource.loop = true;
        heartSource.volume = 0f;
        heartSource.Play(); // 遊戲一開始就默默在背景播，只是音量是 0

        // 💀 抓取場景內所有守衛
        allNpcs = FindObjectsByType<NpcAI>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        if (allNpcs == null || allNpcs.Length == 0) return;

        // 💀 核心修改：取得「目前被附身物件」的真實位置
        Vector3 currentPlayerPosition;

        // 利用你原本就有的 PlayerMovement.Current 來定位！
        if (PlayerMovement.Current != null)
        {
            currentPlayerPosition = PlayerMovement.Current.transform.position;
        }
        else
        {
            // 如果目前沒有附身任何東西 (例如正在切換、或主角死亡)，心跳聲迅速淡出
            heartSource.volume = Mathf.MoveTowards(heartSource.volume, 0f, Time.deltaTime * 5f);
            heartSource.pitch = Mathf.MoveTowards(heartSource.pitch, minPitch, Time.deltaTime);
            return;
        }

        float closestDistance = Mathf.Infinity;

        // 找出離「當前附身物件」最近的守衛
        foreach (var npc in allNpcs)
        {
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

            // 💀 把原本的 transform.position 改成 currentPlayerPosition
            float dist = Vector3.Distance(currentPlayerPosition, npc.transform.position);

            // 如果 NPC 被砸暈或閃瞎，無視他！(根據你的 NpcAI 狀態解開註解)
            if (npc.CurrentState == NpcAI.NpcState.Stunned || npc.CurrentState == NpcAI.NpcState.Blinded) continue;

            if (dist < closestDistance)
            {
                closestDistance = dist;
            }
        }

        // --- 核心運算：動態調整心跳聲 ---
        if (closestDistance < maxDistance)
        {
            float tension = 1f - Mathf.InverseLerp(minDistance, maxDistance, closestDistance);
            float targetVolume = Mathf.Lerp(minVolume, maxVolume, tension);
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, tension);

            heartSource.volume = Mathf.MoveTowards(heartSource.volume, targetVolume, Time.deltaTime * 2f);
            heartSource.pitch = Mathf.MoveTowards(heartSource.pitch, targetPitch, Time.deltaTime);
        }
        else
        {
            heartSource.volume = Mathf.MoveTowards(heartSource.volume, 0f, Time.deltaTime);
            heartSource.pitch = Mathf.MoveTowards(heartSource.pitch, minPitch, Time.deltaTime);
        }
    }
}