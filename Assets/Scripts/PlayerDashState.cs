using UnityEngine;

public class PlayerDashState : PlayerState
{
    private const string StepForward = "Step_Forward";
    private const string StepBackward = "BackStep";
    private const string RunToIdle = "Run_to_Idle";

    [SerializeField] private float forwardDistance = 10f;
    [SerializeField] private float backwardDistance = 15f;
    [SerializeField] private float forwardEffectiveRatio = 1f;   // 前进有效移动占比
    [SerializeField] private float backwardEffectiveRatio = 0.6f;  // 后撤步有效移动占比

    private float dashDuration;
    private float timer;
    private int direction;
    private string currentAnim;
    private bool isForward;
    private bool isTransitioning;
    private float effectiveDuration;   // 有效移动时长
    private float initialSpeed;        // 初始速度（峰值）

    public PlayerDashState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入Dash状态");

        player.rb.velocity = Vector2.zero;

        isForward = (direction == player.facingDirection);
        currentAnim = isForward ? StepForward : StepBackward;
        player.skeletonAnim.AnimationState.SetAnimation(0, currentAnim, false);

        dashDuration = player.GetAnimationDuration(currentAnim);
        timer = dashDuration;
        isTransitioning = false;

        // 根据前进/后撤选择参数
        float ratio = isForward ? forwardEffectiveRatio : backwardEffectiveRatio;
        float targetDistance = isForward ? forwardDistance : backwardDistance;

        effectiveDuration = dashDuration * ratio;
        // 速度线性衰减，位移 = 0.5 * initialSpeed * effectiveDuration
        initialSpeed = 2f * targetDistance / effectiveDuration;

        // 朝向处理：只有前进才改变朝向
        if (isForward)
        {
            player.skeletonAnim.Skeleton.ScaleX = (direction > 0) ? 1 : -1;
            player.facingDirection = direction;
        }

        Debug.Log($"Dash: 动画={currentAnim}, 时长={dashDuration}, 有效时长={effectiveDuration}, 初速={initialSpeed}");
    }

    public override void Update()
    {
        base.Update();

        if (isTransitioning)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
                player.ChangeState(player.IdleState);
            return;
        }

        // 计算当前速度（线性衰减）
        float currentSpeed = 0f;
        float remaining = timer; // 剩余总时间
        float decelerationStart = dashDuration - effectiveDuration; // 开始减速的时间点

        if (remaining > decelerationStart) // 在有效移动阶段
        {
            float effectiveRemaining = remaining - decelerationStart;
            // 速度 = 初始速度 * (有效剩余时间 / 有效总时长) 线性递减
            currentSpeed = initialSpeed * (effectiveRemaining / effectiveDuration);
        }
        else
        {
            currentSpeed = 0f; // 收尾阶段不移动
        }

        // 应用速度
        player.rb.velocity = new Vector2(direction * currentSpeed, 0f);

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            if (isForward)
            {
                float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                if (Mathf.Abs(moveX) > 0.1f)
                    player.ChangeState(player.RunState);
                else
                    PlayRunToIdleTransition();
            }
            else
            {
                player.ChangeState(player.IdleState);
            }
        }
    }

    private void PlayRunToIdleTransition()
    {
        player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdle, false);
        float duration = player.GetAnimationDuration(RunToIdle);
        timer = duration;
        isTransitioning = true;
        player.rb.velocity = new Vector2(0f, player.rb.velocity.y);
        Debug.Log("冲刺后无移动，播放 Run_to_Idle 过渡");
    }

    public override void Exit()
    {
        base.Exit();
        Debug.Log("退出Dash状态");
    }

    public void SetDirection(int dir)
    {
        direction = dir;
    }
}