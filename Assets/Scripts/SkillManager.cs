using UnityEngine;
using UnityEngine.InputSystem;

public class SkillManager : MonoBehaviour
{
    [Header("技能插槽")]
    [SerializeField] private BaseSkill currentSkill; // 拖曳該物件身上的技能腳本

    private InputSystem_Actions playerActions;

    private void Awake()
    {
        // 自動抓取同一物件上的技能 (如果沒拖的話)
        if (currentSkill == null) currentSkill = GetComponent<BaseSkill>();

        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // 假設你在 Input System 裡設定了一個叫 "Interact" 或 "Skill" 的動作 (綁定 F 鍵)
        // 如果還沒設，請去 InputSystem_Actions.inputactions 新增一個 Action
        playerActions.Player.Interact.performed += OnSkillInput;
    }

    private void OnDisable()
    {
        playerActions.Player.Interact.performed -= OnSkillInput;
        playerActions.Player.Disable();
    }

    private void OnSkillInput(InputAction.CallbackContext context)
    {
        // 只有當這個物件被附身 (Active) 時才反應
        // 簡單判斷：如果這個腳本啟用了，通常代表被附身了 (由 TeamManager 控制)
        if (currentSkill != null && this.enabled)
        {
            currentSkill.TryActivate();
        }
    }

    // (可選) 如果你想動態切換技能
    public void SetSkill(BaseSkill newSkill)
    {
        currentSkill = newSkill;
    }
}