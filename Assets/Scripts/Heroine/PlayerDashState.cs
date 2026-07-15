using UnityEngine;

public class PlayerDashState : PlayerState
{
    private const string StepForward = "Step_Forward";
    private const string StepBackward = "BackStep";
    private const string RunToIdle = "Run_to_Idle";

    [SerializeField] private float forwardDistance = 10f;
    [SerializeField] private float backwardDistance = 15f;
    [SerializeField] private float forwardEffectiveRatio = 1f;
    [SerializeField] private float backwardEffectiveRatio = 0.6f;

    private float dashDuration;
    private float timer;
    private int direction;
    private string currentAnim;
    private bool isForward;
    private bool isTransitioning;
    private float effectiveDuration;
    private float initialSpeed;

    public PlayerDashState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        

        player.rb.velocity = Vector2.zero;

        isForward = (direction == player.facingDirection);
        currentAnim = isForward ? StepForward : StepBackward;
        player.skeletonAnim.AnimationState.SetAnimation(0, currentAnim, false);

        dashDuration = player.GetAnimationDuration(currentAnim);
        timer = dashDuration;
        isTransitioning = false;

        float ratio = isForward ? forwardEffectiveRatio : backwardEffectiveRatio;
        float targetDistance = isForward ? forwardDistance : backwardDistance;

        effectiveDuration = dashDuration * ratio;
        initialSpeed = 2f * targetDistance / effectiveDuration;

        if (isForward)
        {
            player.skeletonAnim.Skeleton.ScaleX = (direction > 0) ? 1 : -1;
            player.facingDirection = direction;
        }

        
    }

    public override void Update()
    {
        base.Update();

        // ★ 在过渡期间检测输入（与 RunState 行为一致）
        if (isTransitioning)
        {
            // 攻击检测
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.ChangeState(player.AttackState);
                return;
            }
            // 跳跃检测
            if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && player.IsGrounded())
            {
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = true;
                player.ChangeState(player.JumpState);
                return;
            }

            // 方向键检测（取消过渡，切换到跑步）
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(moveX) > 0.01f)
            {
                // 取消过渡
                isTransitioning = false;
                // 设置速度
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                // 更新朝向（但不主动触发转向动画，由 RunState 的 Enter 处理）
                int newDir = (moveX > 0) ? 1 : -1;
                if (newDir != player.facingDirection)
                {
                    player.skeletonAnim.Skeleton.ScaleX = newDir;
                    player.facingDirection = newDir;
                }
                // 切换到 RunState（正常播放 Run_Start，符合自然过渡）
                player.forceRunLoop = false; // 确保正常播放 Run_Start
                player.ChangeState(player.RunState);
                return;
            }

            // 倒计时结束后切 Idle
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        // ---- 原始冲刺逻辑 ----
        float currentSpeed = 0f;
        float remaining = timer;
        float decelerationStart = dashDuration - effectiveDuration;

        if (remaining > decelerationStart)
        {
            float effectiveRemaining = remaining - decelerationStart;
            currentSpeed = initialSpeed * (effectiveRemaining / effectiveDuration);
        }
        else
        {
            currentSpeed = 0f;
        }

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
        
    }

    public override void Exit()
    {
        base.Exit();
        
    }

    public void SetDirection(int dir)
    {
        direction = dir;
    }
}