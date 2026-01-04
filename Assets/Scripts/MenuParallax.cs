using UnityEngine;

public class MenuParallax : MonoBehaviour
{
    public float offsetMultiplier = 15f; // 移動幅度，不要太大
    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 獲取滑鼠在螢幕上的標準化位置 (-1 到 1)
        float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
        float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;

        // 讓背景往相反方向微動
        transform.position = startPos + new Vector3(mouseX * offsetMultiplier, mouseY * offsetMultiplier, 0);
    }
}