using System.Collections;
using UnityEngine;

public class ShowDoorAngle : MonoBehaviour
{
    private HingeJoint hinge;
    private Rigidbody rb;

    void Start()
    {
        hinge = GetComponent<HingeJoint>();
        rb = GetComponent<Rigidbody>();

        // 確保一開始是凍結的 (雙重保險)
        if (rb != null) rb.isKinematic = true;

        // 啟動解凍程序
        StartCoroutine(UnfreezeDoor());
    }

    IEnumerator UnfreezeDoor()
    {
        // 1. 讓門休息 0.2 秒
        // 這段時間足夠讓 Unity 載入所有物件，並消除初始化的震盪
        yield return new WaitForSeconds(0.2f);

        if (rb != null)
        {
            // 2. 解除封印！
            rb.isKinematic = false;

            // 3. 再次喚醒，並給一個微小的力確保數據刷新
            rb.WakeUp();
            rb.AddRelativeTorque(new Vector3(0, 0.0001f, 0));

            Debug.Log("門已解凍，物理運算啟動！");
        }
    }

    void OnGUI()
    {
        if (hinge != null)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 40;
            // 如果還是 NaN，顯示紅色警告；正常則顯示綠色
            style.normal.textColor = float.IsNaN(hinge.angle) ? Color.red : Color.green;
            GUI.Label(new Rect(50, 50, 400, 100), $"Door Angle: {hinge.angle:F1}", style);
        }
    }
}