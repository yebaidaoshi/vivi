using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D.Animation;

public class PlayerController_NewInput : MonoBehaviour
{
    [Header("动画控制")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isRunningParam = "isRunning";

    [Header("模型切换（Sprite Swap）")]
    [SerializeField] private SpriteLibraryAsset idleLibrary;
    [SerializeField] private SpriteLibraryAsset runLibrary;

    private InputAction moveAction;
    private SpriteLibrary spriteLibrary;
    private SpriteResolver[] allResolvers;

    private void Awake()
    {
        // 1. 创建一个 D 键的输入动作
        moveAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/d");
        // 也可以绑定多个键，例如：moveAction.AddBinding("<Keyboard>/rightArrow");

        if (animator == null)
            animator = GetComponent<Animator>();

        spriteLibrary = GetComponent<SpriteLibrary>();
        allResolvers = GetComponentsInChildren<SpriteResolver>();
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
    }

    private void Update()
    {
        // 2. 读取 D 键状态
        bool isPressed = moveAction.IsPressed();   // 按住时为 true
        bool wasReleasedThisFrame = moveAction.WasReleasedThisFrame(); // 本帧松开

        // 3. 根据状态控制动画
        if (isPressed)
        {
            animator.SetBool(isRunningParam, true);
        }
        else if (wasReleasedThisFrame)
        {
            animator.SetBool(isRunningParam, false);
        }

        // 4. 根据状态切换模型（Sprite Swap）
        if (isPressed)
        {
            SwapModelLibrary(runLibrary);
        }
        else if (wasReleasedThisFrame)
        {
            SwapModelLibrary(idleLibrary);
        }
    }

    // 切换整个精灵库
    private void SwapModelLibrary(SpriteLibraryAsset targetLibrary)
    {
        if (spriteLibrary == null || targetLibrary == null)
            return;

        spriteLibrary.spriteLibraryAsset = targetLibrary;

        foreach (var resolver in allResolvers)
        {
            resolver.SetCategoryAndLabel(resolver.GetCategory(), resolver.GetLabel());
        }
    }
}