using UnityEngine;

// 🔥 這是一個介面 (Interface)，不是 Class
public interface IInteractable
{
    // 所有可互動物件都必須實作這個功能
    void Interact();

    // (選填) 讓 UI 顯示「按下 F 開門」之類的提示
    string GetInteractionPrompt();
}