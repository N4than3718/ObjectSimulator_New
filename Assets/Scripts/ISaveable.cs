public interface ISaveable
{
    // 將該物件的專屬狀態轉為 JSON 字串回傳
    string GetSaveData();

    // 接收 JSON 字串並還原該物件的狀態
    void RestoreSaveData(string jsonState);
}