using UnityEngine;
using UnityEngine.InputSystem;

public class SkillManager : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private TeamManager teamManager;

    private InputSystem_Actions playerActions;

    private void Awake()
    {
        if (teamManager == null) teamManager = FindAnyObjectByType<TeamManager>();
        playerActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        // 綁定 F 鍵 (假設你的 Action 叫 Interact 或 Skill)
        playerActions.Player.Interact.started += OnSkillInput;
        playerActions.Player.Interact.performed += OnSkillInput;
        playerActions.Player.Interact.canceled += OnSkillInput;
    }

    private void OnDisable()
    {
        playerActions.Player.Interact.started -= OnSkillInput;
        playerActions.Player.Interact.performed -= OnSkillInput;
        playerActions.Player.Interact.canceled -= OnSkillInput;
        playerActions.Player.Disable();
    }

    private void OnSkillInput(InputAction.CallbackContext context)
    {
        // 1. 確認目前是否在附身狀態
        if (teamManager == null || teamManager.CurrentGameState != TeamManager.GameState.Possessing)
        {
            return;
        }

        // 2. 取得當前操控的物件 (ActiveCharacter)
        GameObject activeObj = teamManager.ActiveCharacterGameObject;

        if (activeObj != null)
        {
            // 3. 自動搜尋該物件身上是否有任何 "BaseSkill" 的子類別 (FlashlightSkill, NoiseSkill...)
            BaseSkill skill = activeObj.GetComponent<BaseSkill>();

            if (skill != null)
            {
                // 4. 觸發技能
                Debug.Log($"[SkillManager] 觸發了 {activeObj.name} 的 {skill.GetType().Name}");
                skill.OnInput(context);
            }
            else
            {
                Debug.LogWarning($"[lSkillManager] 當前物件 {activeObj.name} 身上沒有掛載任何技能腳本！");
            }
        }
    }
}