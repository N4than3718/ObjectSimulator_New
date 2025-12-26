using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("你的遊戲關卡場景名稱")]
    [SerializeField] private string gameLevelName = "Kitchen_Level_1"; // 請確認你的場景名稱

    public void OnStartGame()
    {
        // 這裡我們直接載入，或者呼叫你的 GameSceneManager (如果你有做轉場)
        GameSceneManager.Instance.LoadScene(gameLevelName);
    }

    public void OnQuitGame()
    {
        Debug.Log("Quit Game!");
        Application.Quit();
    }
}