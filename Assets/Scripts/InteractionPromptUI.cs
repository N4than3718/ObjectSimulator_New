using UnityEngine;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private GameObject promptPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        HidePrompt();
    }

    // 💀 開放給外部呼叫的方法
    public void ShowPrompt(string text)
    {
        if (promptText != null) promptText.text = text;
        if (promptPanel != null) promptPanel.SetActive(true);
    }

    public void HidePrompt()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
    }
}