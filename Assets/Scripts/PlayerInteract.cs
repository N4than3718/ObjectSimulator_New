using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : MonoBehaviour
{
    private InputSystem_Actions inputActions;
    private PlayerMovement playerMovement; // 🔥 引用 PlayerMovement

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        playerMovement = GetComponent<PlayerMovement>(); // 抓取自己身上的移動腳本
    }

    void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Interact.performed += OnInteractPerformed;
    }

    void OnDisable()
    {
        inputActions.Player.Interact.performed -= OnInteractPerformed;
        inputActions.Player.Disable();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        // 1. 直接問 PlayerMovement：我們現在高亮誰？
        GameObject target = playerMovement.CurrentTargetedObject;

        if (target != null)
        {
            // 2. 檢查這個目標是否有 IInteractable (門、抽屜、開關)
            IInteractable interactable = target.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                // 3. 觸發互動！
                interactable.Interact();
                Debug.Log($"[PlayerInteract] 與 {target.name} 互動成功");
            }
        }
        else
        {
            // 沒高亮任何東西，按 F 無效
            // Debug.Log("沒對準任何互動物件");
        }
    }

    // 移除 OnDrawGizmos，因為現在依賴 PlayerMovement 的視覺化，不需要這裡畫線了
}