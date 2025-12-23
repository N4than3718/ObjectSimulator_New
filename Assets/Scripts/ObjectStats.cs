using UnityEngine;

public class ObjectStats : MonoBehaviour
{
    [Header("物件基礎屬性")]
    [Tooltip("物件重量 (單位kg)。主要用於計算碰撞聲響和物理反饋。")]
    public float weight = 1.0f; // 範例：紙箱 2kg, 鞋子 0.5kg, 槌子 5kg 

    [Tooltip("這個物體是否容易發出聲音？(例如金屬 1.5, 布料 0.2)")]
    public float noiseMultiplier = 1.0f;

    [HideInInspector]
    public bool isInsideContainer = false;
}