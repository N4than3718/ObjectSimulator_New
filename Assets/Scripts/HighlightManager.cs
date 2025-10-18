using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamManager teamManager;
    [Tooltip("白色高亮材質模板")]
    [SerializeField] private Material availableHighlightTemplate; // 模板給 HighlightableObject 用

    [Header("Settings")]
    [Tooltip("掃描場景更新高亮的頻率（秒）")]
    [SerializeField] private float updateInterval = 0.5f;

    private List<HighlightableObject> allHighlightables = new List<HighlightableObject>();

    void Start()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager == null)
        {
            Debug.LogError("HighlightManager cannot find TeamManager!");
            enabled = false;
            return;
        }
        if (availableHighlightTemplate == null)
        {
            Debug.LogError("HighlightManager needs the Available Highlight Material Template!");
            enabled = false;
            return;
        }

        // 找到場景中所有可高亮的物件
        allHighlightables.AddRange(FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None));

        // 把白色模板傳給每個物件
        foreach (var obj in allHighlightables)
        {
            obj.availableHighlightTemplate = this.availableHighlightTemplate;
        }


        StartCoroutine(UpdateAvailableHighlights());
    }

    IEnumerator UpdateAvailableHighlights()
    {
        while (true)
        {
            foreach (HighlightableObject highlightable in allHighlightables)
            {
                if (highlightable != null && highlightable.enabled) // 確保物件還存在且腳本啟用
                {
                    // 檢查物件是否**不在**隊伍中
                    bool shouldBeAvailable = !highlightable.IsInTeam(teamManager);
                    highlightable.SetAvailableHighlight(shouldBeAvailable);
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }
}