using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerModelSwitcher : MonoBehaviour
{
    [Header("模型引用")]
    [SerializeField] private GameObject idleModel;   // 站立模型
    [SerializeField] private GameObject runModel;    // 跑步模型

    [Header("输入动作")]
    [SerializeField] private InputActionReference moveAction;

    [Header("停止动画设置（跑步模型）")]
    [SerializeField] private string stopTriggerName = "Stop";      // Animator 中触发停止动画的 Trigger 名称
    [SerializeField] private float stopAnimationDuration = 0.5f;   // 停止动画的时长（秒）

    [Header("站立模型显示后的行为")]
    [Tooltip("切换到站立模型后触发的事件，可用于播放站立模型的入场动画")]
    public UnityEvent OnIdleModelShown;

    private bool isMoving = false;
    private Coroutine stopTransitionCoroutine = null;
    private Animator runAnimator;

    private void Awake()
    {
        if (runModel != null)
            runAnimator = runModel.GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (moveAction == null)
        {
            Debug.LogError("moveAction 未绑定！");
            return;
        }

        moveAction.action.Enable();
        moveAction.action.performed += OnMove;
        moveAction.action.canceled += OnMoveCanceled;

        Vector2 initialMove = moveAction.action.ReadValue<Vector2>();
        UpdateMovementState(initialMove);
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.action.performed -= OnMove;
            moveAction.action.canceled -= OnMoveCanceled;
            moveAction.action.Disable();
        }
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 moveValue = ctx.ReadValue<Vector2>();
        UpdateMovementState(moveValue);
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        UpdateMovementState(Vector2.zero);
    }

    private void UpdateMovementState(Vector2 moveValue)
    {
        bool nowMoving = moveValue.sqrMagnitude > 0.01f;

        // 静止 → 移动
        if (nowMoving && !isMoving)
        {
            // 如果正在播放停止动画，立即中断并切换到跑步模型
            if (stopTransitionCoroutine != null)
            {
                StopCoroutine(stopTransitionCoroutine);
                stopTransitionCoroutine = null;
            }
            SwitchToRunModel();
            isMoving = true;
        }
        // 移动 → 静止
        else if (!nowMoving && isMoving)
        {
            StartStopTransition();
            isMoving = false;
        }
    }

    private void SwitchToRunModel()
    {
        if (idleModel != null) idleModel.SetActive(false);
        if (runModel != null) runModel.SetActive(true);
    }

    private void SwitchToIdleModel()
    {
        if (idleModel != null) idleModel.SetActive(true);
        if (runModel != null) runModel.SetActive(false);

        // 触发站立模型显示后的事件（例如播放入场动画）
        OnIdleModelShown?.Invoke();
    }

    private void StartStopTransition()
    {
        // 触发跑步模型的停止动画
        if (runAnimator != null && !string.IsNullOrEmpty(stopTriggerName))
        {
            runAnimator.SetTrigger(stopTriggerName);
        }

        if (stopTransitionCoroutine != null)
            StopCoroutine(stopTransitionCoroutine);
        stopTransitionCoroutine = StartCoroutine(WaitForStopAnimationAndSwitch());
    }

    private IEnumerator WaitForStopAnimationAndSwitch()
    {
        yield return new WaitForSeconds(stopAnimationDuration);
        FinishStopTransition();
    }

    /// <summary>
    /// 停止动画结束，切换到站立模型
    /// 也可以由动画事件调用以获得更精确的时机
    /// </summary>
    public void FinishStopTransition()
    {
        if (stopTransitionCoroutine != null)
        {
            StopCoroutine(stopTransitionCoroutine);
            stopTransitionCoroutine = null;
        }
        SwitchToIdleModel();
    }

    private void Start()
    {
        // 初始状态：站立模型激活，跑步模型隐藏
        if (idleModel != null) idleModel.SetActive(true);
        if (runModel != null) runModel.SetActive(false);
        isMoving = false;
    }  
}