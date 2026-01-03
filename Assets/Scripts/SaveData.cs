using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitData
{
    public string unitName;
    public Vector3 position;
    public Quaternion rotation;
    public bool isAvailable;
}

[System.Serializable]
public class SaveData
{
    public int activeUnitIndex; // 目前控制哪一個
    public List<UnitData> teamUnits = new List<UnitData>();
    public string saveTime;
}