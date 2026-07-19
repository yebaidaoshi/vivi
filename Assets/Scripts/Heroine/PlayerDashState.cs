using Spine;
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

    // 缓冲攻击标志（仅用于向前冲刺）
    private bool pendingAttack = false;

    // 等待窗口（仅用于向前冲刺）
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private const float WAIT_WINDOW = 0.5f;

    public PlayerDashState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();

        // ★ 如果距离第三段攻击结束小于 0.2 秒，不允许冲刺，转为跑步
        if (Time.time - player.lastThirdAttackExitTime < 0.2f)
        {
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(moveX) > 0.01f)
            {
                player.skeletonAnim.Skeleton.ScaleX = (moveX > 0) ? 1 : -1;
                player.facingDirection = (moveX > 0) ? 1 : -1;
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                player.ChangeState(player.RunState);
            }
            else
            {
                player.ChangeState(player.IdleState);
            }
            return;
        }

        player.rb.velocity = Vector2.zero;

        isForward = (direction == player.facingDirection);
        currentAnim = isForward ? StepForward : StepBackward;
        player.skeletonAnim.AnimationState.SetAnimation(0, currentAnim, false);

        dashDuration = player.GetAnimationDuration(currentAnim);
        timer = dashDuration;
        isTransitioning = false;
        pendingAttack = false;
        isWaiting = false;

        float ratio = isForward ? forwardEffectiveRatio : backwardEffectiveRatio;
        float targetDistance = isForward ? forwardDistance : backwardDistance;

        effectiveDuration = dashDuration * ratio;
        initialSpeed = 2f * targetDistance / effectiveDuration;

        if (isForward)
        {
            player.skeletonAnim.Skeleton.ScaleX = (direction > 0) ? 1 : -1;
            player.facingDirection = direction;
        }

        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event += OnSpineEvent;

        Debug.Log($"[DashState] Enter: {currentAnim}");
    }

    public override void Update()
    {
        base.Update();

        // ---- 冲刺过程中检测攻击键（仅向前冲刺缓冲，后撤步立即打断） ----
        if (!isTransitioning && !isWaiting)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                if (isForward)
                {
                    // 向前冲刺：缓冲攻击
                    pendingAttack = true;
                    Debug.Log($"[DashState] 向前冲刺中按攻击，缓冲，当前连击 = {player.currentComboCount}");
                }
                else
                {
                    // 后撤步：立即打断，进入攻击状态（连击推进）
                    player.currentComboCount = Mathf.Min(player.currentComboCount + 1, 3);
                    Debug.Log($"[DashState] 后撤步中按攻击，立即打断，连击变为 {player.currentComboCount}");
                    player.ChangeState(player.AttackState);
                    return;
                }
            }
        }

        // ---- 可取消窗口内检测打断（蹲下、跳跃立即生效；方向键不打断） ----
        if (player.canCancelAttack)
        {
            // 蹲下打断（重置连击）
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                player.currentComboCount = 0;
                pendingAttack = false;
                isWaiting = false;
                player.rb.velocity = Vector2.zero;
                player.ChangeState(player.CrouchState);
                return;
            }

            // 跳跃打断（重置连击）
            if (player.inputActions.Player.Jump.WasPressedThisFrame() &&
                player.currentJumpCount < player.maxJumpCount &&
                player.IsGrounded())
            {
                player.currentComboCount = 0;
                pendingAttack = false;
                isWaiting = false;
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = true;
                player.ChangeState(player.JumpState);
                return;
            }
            // ★ 方向键打断已移除，避免冲刺期间被方向键中断
        }

        // ---- 等待窗口（仅向前冲刺） ----
        if (isWaiting)
        {
            // 1. 检测攻击键（推进连击，进入攻击）
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.currentComboCount = Mathf.Min(player.currentComboCount + 1, 3);
                Debug.Log($"[DashState] 等待窗口按攻击，连击变为 {player.currentComboCount}");
                pendingAttack = false;
                isWaiting = false;
                if (isTransitioning)
                {
                    isTransitioning = false;
                }
                player.ChangeState(player.AttackState);
                return;
            }

            // 2. 方向键（立即跑步，连击保留）
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(moveX) > 0.1f)
            {
                Debug.Log($"[DashState] 等待窗口按方向，进入跑步，连击 = {player.currentComboCount}");
                pendingAttack = false;
                isWaiting = false;
                if (isTransitioning)
                {
                    isTransitioning = false;
                }
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                int newDir = (moveX > 0) ? 1 : -1;
                if (newDir != player.facingDirection)
                {
                    player.skeletonAnim.Skeleton.ScaleX = newDir;
                    player.facingDirection = newDir;
                }
                player.forceRunLoop = false;
                player.ChangeState(player.RunState);
                return;
            }

            // 3. 蹲下/跳跃打断（重置连击）
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                player.currentComboCount = 0;
                pendingAttack = false;
                isWaiting = false;
                player.rb.velocity = Vector2.zero;
                player.ChangeState(player.CrouchState);
                return;
            }
            if (player.inputActions.Player.Jump.WasPressedThisFrame() &&
                player.currentJumpCount < player.maxJumpCount &&
                player.IsGrounded())
            {
                player.currentComboCount = 0;
                pendingAttack = false;
                isWaiting = false;
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = true;
                player.ChangeState(player.JumpState);
                return;
            }

            // 4. 更新等待计时器
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                // 超时，重置连击
                player.currentComboCount = 0;
                pendingAttack = false;
                isWaiting = false;
                Debug.Log($"[DashState] 等待窗口超时，连击重置为0");

                if (isTransitioning)
                {
                    // 过渡动画还在播放，不立即切换
                }
                else
                {
                    player.ChangeState(player.IdleState);
                }
            }
            return;
        }

        // ---- 过渡（Run_to_Idle）期间 ----
        if (isTransitioning)
        {
            // 检测攻击键（推进连击，进入攻击）
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.currentComboCount = Mathf.Min(player.currentComboCount + 1, 3);
                Debug.Log($"[DashState] 过渡期间按攻击，连击变为 {player.currentComboCount}");
                pendingAttack = false;
                isTransitioning = false;
                player.ChangeState(player.AttackState);
                return;
            }

            // 方向键（立即跑步，连击保留）
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(moveX) > 0.1f)
            {
                Debug.Log($"[DashState] 过渡期间按方向，进入跑步，连击 = {player.currentComboCount}");
                pendingAttack = false;
                isTransitioning = false;
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                int newDir = (moveX > 0) ? 1 : -1;
                if (newDir != player.facingDirection)
                {
                    player.skeletonAnim.Skeleton.ScaleX = newDir;
                    player.facingDirection = newDir;
                }
                player.forceRunLoop = false;
                player.ChangeState(player.RunState);
                return;
            }

            // 蹲下/跳跃打断（重置连击）
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                player.currentComboCount = 0;
                pendingAttack = false;
                isTransitioning = false;
                player.rb.velocity = Vector2.zero;
                player.ChangeState(player.CrouchState);
                return;
            }
            if (player.inputActions.Player.Jump.WasPressedThisFrame() &&
                player.currentJumpCount < player.maxJumpCount &&
                player.IsGrounded())
            {
                player.currentComboCount = 0;
                pendingAttack = false;
                isTransitioning = false;
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = true;
                player.ChangeState(player.JumpState);
                return;
            }

            // 过渡计时
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                if (!isWaiting)
                {
                    player.ChangeState(player.IdleState);
                }
                else
                {
                    // 等待窗口还在，过渡结束，播放 Idle 循环
                    isTransitioning = false;
                    var idleAnim = player.skeletonAnim.Skeleton.Data.FindAnimation("Idle");
                    if (idleAnim != null)
                        player.skeletonAnim.AnimationState.SetAnimation(0, "Idle", true);
                }
            }
            return;
        }

        // ---- 冲刺物理（向前和向后共用） ----
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

        // ---- 冲刺结束 ----
        if (timer <= 0f)
        {
            if (isForward)
            {
                // 向前冲刺：检查缓冲攻击或进入等待窗口
                if (pendingAttack)
                {
                    player.currentComboCount = Mathf.Min(player.currentComboCount + 1, 3);
                    Debug.Log($"[DashState] 缓冲攻击触发，连击变为 {player.currentComboCount}");
                    pendingAttack = false;
                    player.ChangeState(player.AttackState);
                    return;
                }

                // 无缓冲攻击，进入等待窗口并播放 Run_to_Idle 过渡
                isWaiting = true;
                waitTimer = WAIT_WINDOW;
                Debug.Log($"[DashState] 向前冲刺结束，进入等待窗口，连击 = {player.currentComboCount}");
                PlayRunToIdleTransition();
            }
            else
            {
                // 后撤步：动画结束，直接进入 Idle（连击重置）
                player.currentComboCount = 0;
                pendingAttack = false;
                Debug.Log($"[DashState] 后撤步结束，进入 Idle");
                player.ChangeState(player.IdleState);
            }
        }
    }

    private void PlayRunToIdleTransition()
    {
        var anim = player.skeletonAnim.Skeleton.Data.FindAnimation(RunToIdle);
        if (anim == null)
        {
            Debug.LogWarning($"[DashState] 动画 {RunToIdle} 不存在，直接进入 Idle");
            var idleAnim = player.skeletonAnim.Skeleton.Data.FindAnimation("Idle");
            if (idleAnim != null)
                player.skeletonAnim.AnimationState.SetAnimation(0, "Idle", true);
            // 注意：不设置 isTransitioning，等待窗口会处理后续
            return;
        }

        player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdle, false);
        float duration = player.GetAnimationDuration(RunToIdle);
        timer = duration;
        isTransitioning = true;
        player.rb.velocity = Vector2.zero;
    }

    private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "SendEvent")
        {
            string str = e.String;
            if (str == "Cancelable" || str == "Movable" || str == "Stepped_Forward")
            {
                player.canCancelAttack = true;
                Debug.Log($"[DashState] 事件 {str} 开启取消窗口");
            }
            else if (str == "End")
            {
                player.canCancelAttack = false;
                Debug.Log($"[DashState] 事件 End 关闭取消窗口");
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (player.skeletonAnim != null)
            player.skeletonAnim.AnimationState.Event -= OnSpineEvent;
        player.canCancelAttack = false;
        isTransitioning = false;
        isWaiting = false;
        pendingAttack = false;
        Debug.Log("[DashState] Exit");
    }

    public void SetDirection(int dir)
    {
        direction = dir;
    }
}