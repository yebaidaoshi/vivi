using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Text;

public class PlayerModelSwitcher : MonoBehaviour
{
    [Header("模型引用")]
    [SerializeField] private GameObject idleModel;
    [SerializeField] private GameObject runModel;

    [Header("输入动作")]
    [SerializeField] private InputActionReference moveAction;

    [Header("动画状态名称（必须与Animator中完全一致）")]
    [SerializeField] private string runStateName = "Run";
    [SerializeField] private string idleLoopStateName = "Idle";
    [SerializeField] private string runStopTriggerName = "Stop";
    [SerializeField] private string idleStopTriggerName = "Stop";

    [Header("动画时长")]
    [SerializeField] private float runStopDuration = 0.5f;
    [SerializeField] private float idleStopDuration = 0.3f;

    [Header("优化选项")]
    [SerializeField] private bool keepIdleAlwaysActive = true;

    private bool isMoving = false;
    private float runStopTimer = -1f;
    private Animator runAnimator;
    private Animator idleAnimator;
    private Renderer runRenderer;
    private Renderer idleRenderer;
    private RuntimeAnimatorController originalRunController;

    private void Awake()
    {
        if (runModel != null)
        {
            runAnimator = runModel.GetComponent<Animator>();
            runRenderer = runModel.GetComponent<Renderer>();
            if (runAnimator != null)
                originalRunController = runAnimator.runtimeAnimatorController;
        }
        if (idleModel != null)
        {
            idleAnimator = idleModel.GetComponent<Animator>();
            idleRenderer = idleModel.GetComponent<Renderer>();
        }
        if (keepIdleAlwaysActive && idleModel != null)
            idleModel.SetActive(true);
    }

    private void OnEnable()
    {
        if (moveAction == null) { Debug.LogError("moveAction未绑定！"); return; }
        moveAction.action.Enable();
        moveAction.action.performed += OnMove;
        moveAction.action.canceled += OnMoveCanceled;
        UpdateMovementState(moveAction.action.ReadValue<Vector2>());
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

    private void OnMove(InputAction.CallbackContext ctx) => UpdateMovementState(ctx.ReadValue<Vector2>());
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => UpdateMovementState(Vector2.zero);

    private void UpdateMovementState(Vector2 moveValue)
    {
        bool nowMoving = Mathf.Abs(moveValue.x) > 0.01f;
        if (nowMoving && !isMoving)
        {
            runStopTimer = -1f;
            ForceSwitchToRunModel();
            isMoving = true;
        }
        else if (!nowMoving && isMoving)
        {
            if (runStopTimer < 0f)
                StartRunStopTransition();
            isMoving = false;
        }
    }

    private void ForceSwitchToRunModel()
    {
        SetModelVisible(idleModel, idleRenderer, false);
        SetModelVisible(runModel, runRenderer, true);
        if (runAnimator == null) return;

        // 彻底重置Animator
        runAnimator.runtimeAnimatorController = null;
        runAnimator.Rebind();
        runAnimator.Update(0f);
        runAnimator.runtimeAnimatorController = originalRunController;

        // 重置所有参数
        foreach (var param in runAnimator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Trigger)
                runAnimator.ResetTrigger(param.name);
            else if (param.type == AnimatorControllerParameterType.Bool)
                runAnimator.SetBool(param.name, false);
            else if (param.type == AnimatorControllerParameterType.Float)
                runAnimator.SetFloat(param.name, 0f);
            else if (param.type == AnimatorControllerParameterType.Int)
                runAnimator.SetInteger(param.name, 0);
        }

        // 验证状态是否存在
        if (!StateExists(runAnimator, runStateName))
        {
            Debug.LogError($"跑步模型：找不到状态 '{runStateName}'！可用的状态有：{GetAllStateNames(runAnimator)}");
            return;
        }

        runAnimator.Play(runStateName, 0, 0f);
        runAnimator.Update(0f);
        Debug.Log($"切换到跑步模型，播放状态：{runStateName}");
    }

    private void StartRunStopTransition()
    {
        if (runAnimator != null && !string.IsNullOrEmpty(runStopTriggerName))
        {
            runAnimator.ResetTrigger(runStopTriggerName);
            runAnimator.SetTrigger(runStopTriggerName);
        }
        else
        {
            SwitchToIdleModel();
            return;
        }
        runStopTimer = 0f;
    }

    private void Update()
    {
        if (runStopTimer >= 0f)
        {
            runStopTimer += Time.deltaTime;
            if (runStopTimer >= runStopDuration)
            {
                runStopTimer = -1f;
                FinishRunStopTransition();
            }
        }
    }

    private void FinishRunStopTransition()
    {
        if (moveAction != null)
        {
            Vector2 currentMove = moveAction.action.ReadValue<Vector2>();
            if (Mathf.Abs(currentMove.x) > 0.01f)
            {
                isMoving = true;
                ForceSwitchToRunModel();
                return;
            }
        }
        SwitchToIdleModel();
    }

    private void SwitchToIdleModel()
    {
        SetModelVisible(idleModel, idleRenderer, true);
        SetModelVisible(runModel, runRenderer, false);
        if (idleAnimator == null) return;

        if (!string.IsNullOrEmpty(idleStopTriggerName))
        {
            idleAnimator.ResetTrigger(idleStopTriggerName);
            idleAnimator.SetTrigger(idleStopTriggerName);
            if (idleStopDuration > 0f)
                StartCoroutine(WaitForIdleStopAndLoop());
        }
        else if (!string.IsNullOrEmpty(idleLoopStateName))
        {
            if (!StateExists(idleAnimator, idleLoopStateName))
            {
                Debug.LogError($"站立模型：找不到状态 '{idleLoopStateName}'！可用状态：{GetAllStateNames(idleAnimator)}");
                return;
            }
            idleAnimator.Play(idleLoopStateName, 0, 0f);
        }
    }

    private IEnumerator WaitForIdleStopAndLoop()
    {
        yield return new WaitForSeconds(idleStopDuration);
        if (idleAnimator != null && !string.IsNullOrEmpty(idleLoopStateName))
        {
            if (StateExists(idleAnimator, idleLoopStateName))
                idleAnimator.Play(idleLoopStateName, 0, 0f);
        }
    }

    private bool StateExists(Animator anim, string stateName)
    {
        if (anim == null) return false;
        return anim.HasState(0, Animator.StringToHash(stateName));
    }

    private string GetAllStateNames(Animator anim)
    {
        if (anim == null) return "null";
        StringBuilder sb = new StringBuilder();
        // 简单方法：只能通过运行时信息，这里提供一个已知状态的列表提示
        sb.Append("请检查Animator窗口中的状态名。常见状态如：Idle, Run, Walk, Stop...");
        return sb.ToString();
    }

    private void SetModelVisible(GameObject model, Renderer rendererComp, bool visible)
    {
        if (model == null) return;
        if (keepIdleAlwaysActive && model == idleModel)
        {
            if (rendererComp != null)
                rendererComp.enabled = visible;
            else
                model.SetActive(visible);
        }
        else
        {
            model.SetActive(visible);
        }
    }

    private void Start()
    {
        SetModelVisible(idleModel, idleRenderer, true);
        SetModelVisible(runModel, runRenderer, false);
        isMoving = false;
        runStopTimer = -1f;
        if (idleAnimator != null && !string.IsNullOrEmpty(idleLoopStateName))
        {
            if (StateExists(idleAnimator, idleLoopStateName))
                idleAnimator.Play(idleLoopStateName, 0, 0f);
        }
    }
}