using UnityEngine;

public class SimpleSpin : MonoBehaviour
{
    // 在 Inspector 裡設定旋轉速度 (例如 Y 設 300)
    public Vector3 spinSpeed = new Vector3(0, 300, 0);

    void Update()
    {
        // Time.deltaTime 確保在不同電腦上轉速一樣
        transform.Rotate(spinSpeed * Time.deltaTime);
    }
}