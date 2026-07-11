using UnityEngine;

public class PlayerRunState : PlayerState
{
    public PlayerRunState(PlayerStateMachine player) : base(player) { }

    // --- 动画名称常量 ---
    private const string RunStartAnim = "Run_Start";
    private const string RunLoopAnim = "Run";
    private const string RunTurningAnim = "Run_Turning";
    private const string RunToIdleAnim = "Run_to_Idle";
    private const string LandingToRunAnim = "Landing_to_Run";

    // --- 过渡状态标志 ---
    private bool isTransitioning = false;          // 正在播放 Run_to_Idle 过渡
    private float transitionTimer = 0f;            // 过渡倒计时（用于切 Idle）

    private bool isLandingToRun = false;           // 正在播放 Landing_to_Run 过渡
    private float landingToRunTimer = 0f;

    // --- 停止延迟机制（新增）---
    private float idleDelayTimer = 0f;             // 停止延迟计时器
    private const float IdleDelayThreshold = 0.05f; // 延迟时间（秒），可调

    // --- 辅助方法 ---
    private void PlayRunWithStart()
    {
        player.skeletonAnim.AnimationState.SetAnimation(0, RunStartAnim, false);
        player.skeletonAnim.AnimationState.AddAnimation(0, RunLoopAnim, true, 0f);
    }

    private void PlayRunLoopDirectly()
    {
        player.skeletonAnim.AnimationState.SetAnimation(0, RunLoopAnim, true);
    }

    private void PlayTurningThenRun()
    {
        // 播放转向动画，并自动衔接跑步循环
        player.skeletonAnim.AnimationState.SetAnimation(0, RunTurningAnim, false);
        player.skeletonAnim.AnimationState.AddAnimation(0, RunLoopAnim, true, 0f);
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入Run状态");
        player.currentJumpCount = 0;

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

        if (player.playLandingToRun)
        {
            var track = player.skeletonAnim.AnimationState.SetAnimation(0, LandingToRunAnim, false);
            track.MixDuration = 0f;
            isLandingToRun = true;
            landingToRunTimer = player.GetAnimationDuration(LandingToRunAnim);
            player.playLandingToRun = false;
        }
        else
        {
            PlayRunWithStart();
            isLandingToRun = false;
        }

        // 立即应用速度与朝向
        player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
        if (moveX > 0.01f)
        {
            player.skeletonAnim.Skeleton.ScaleX = 1f;
            player.facingDirection = 1;
        }
        else if (moveX < -0.01f)
        {
            player.skeletonAnim.Skeleton.ScaleX = -1f;
            player.facingDirection = -1;
        }

        // 重置所有标志
        isTransitioning = false;
        transitionTimer = 0f;
        idleDelayTimer = 0f; // 重置延迟计时器
    }

    public override void Update()
    {
        base.Update();

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        bool isGrounded = player.IsGrounded();

        // ----- 1. 攻击 / 跳跃（始终优先）-----
        if (player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.AttackState);
            return;
        }
        if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && isGrounded)
        {
            player.isRunningJump = true;
            player.ChangeState(player.JumpState);
            return;
        }

        // ----- 2. 速度设置（每帧执行）-----
        player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);

        // ----- 3. 方向翻转 + 转向处理（仅在地面跑步时触发）-----
        if (Mathf.Abs(moveX) > 0.01f && isGrounded)
        {
            int newDirection = (moveX > 0) ? 1 : -1;

            // 获取当前正在播放的动画
            var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            bool isPlayingTurning = (currentTrack != null && currentTrack.Animation.Name == RunTurningAnim);
            bool isPlayingStart = (currentTrack != null && currentTrack.Animation.Name == RunStartAnim);

            // 方向改变且不在过渡中（isTransitioning、isLandingToRun）、不在转向或起步动画中
            if (newDirection != player.facingDirection && !isTransitioning && !isLandingToRun && !isPlayingStart && !isPlayingTurning)
            {
                // 翻转朝向
                player.skeletonAnim.Skeleton.ScaleX = newDirection;
                player.facingDirection = newDirection;

                // 播放转向动画并衔接跑步循环
                PlayTurningThenRun();
            }
            else
            {
                // 如果方向改变了但未触发转向（比如正在播放转向或起步），确保朝向正确
                if (newDirection != player.facingDirection)
                {
                    player.skeletonAnim.Skeleton.ScaleX = newDirection;
                    player.facingDirection = newDirection;
                }
            }
        }

        // ----- 4. 处理 Landing_to_Run 过渡 -----
        if (isLandingToRun)
        {
            // 如果在落地过渡中松开方向键，我们也延迟触发停止（但为了简单，仍然立即进入Run_to_Idle，
            // 因为落地过渡本身很短，且用户可能希望立刻停下）
            // 但为了统一体验，我们也加入延迟判断（可选），这里为了简化，保持原逻辑。
            if (Mathf.Approximately(moveX, 0f))
            {
                player.rb.velocity = new Vector2(0, player.rb.velocity.y);
                // 立即播放 Run_to_Idle（因为落地过渡已结束，可视为停止）
                player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
                isLandingToRun = false;
                isTransitioning = true;
                transitionTimer = player.GetAnimationDuration(RunToIdleAnim); // 动态获取时长
                return;
            }

            landingToRunTimer -= Time.deltaTime;
            if (landingToRunTimer <= 0f)
            {
                // 落地过渡结束，正常进入跑步
                PlayRunWithStart();
                isLandingToRun = false;
            }
            return;
        }

        // ----- 5. 处理 Run_to_Idle 过渡（取消逻辑与延迟触发）-----
        if (isTransitioning)
        {
            // 如果按了方向键，取消过渡
            if (Mathf.Abs(moveX) > 0.01f)
            {
                isTransitioning = false;
                // 根据方向决定是否转向或直接跑步
                int newDir = (moveX > 0) ? 1 : -1;
                if (newDir != player.facingDirection)
                {
                    // 转向：翻转朝向，播放转向动画
                    player.skeletonAnim.Skeleton.ScaleX = newDir;
                    player.facingDirection = newDir;
                    PlayTurningThenRun();
                }
                else
                {
                    // 同向，直接进入跑步循环（不播放 Run_Start）
                    PlayRunLoopDirectly();
                }
                return;
            }

            // 否则继续倒计时，结束后切换到 Idle
            transitionTimer -= Time.deltaTime;
            if (transitionTimer <= 0f)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        // ----- 6. 停止延迟检测（核心新增逻辑）-----
        // 条件：处于跑步状态，并且不在转向、起步、过渡中，且在地面
        var currentTrack2 = player.skeletonAnim.AnimationState.GetCurrent(0);
        bool isPlayingTurning2 = (currentTrack2 != null && currentTrack2.Animation.Name == RunTurningAnim);
        bool isPlayingStart2 = (currentTrack2 != null && currentTrack2.Animation.Name == RunStartAnim);
        bool isPlayingLandingToRun = (currentTrack2 != null && currentTrack2.Animation.Name == LandingToRunAnim);

        // 如果正在播放转向、起步、落地过渡，则忽略停止检测
        if (isPlayingTurning2 || isPlayingStart2 || isPlayingLandingToRun)
        {
            // 重置延迟计时器，以免误触发
            idleDelayTimer = 0f;
            return;
        }

        // 检查水平输入是否为 0
        if (Mathf.Approximately(moveX, 0f))
        {
            // 开始计时
            idleDelayTimer += Time.deltaTime;
            if (idleDelayTimer >= IdleDelayThreshold)
            {
                // 延迟结束，触发 Run_to_Idle
                isTransitioning = true;
                transitionTimer = player.GetAnimationDuration(RunToIdleAnim); // 动态获取时长
                player.rb.velocity = new Vector2(0, player.rb.velocity.y);
                player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
                // 重置计时器，避免重复触发
                idleDelayTimer = 0f;
            }
        }
        else
        {
            // 有输入，重置延迟计时器
            idleDelayTimer = 0f;
        }

        // ----- 正常跑步状态（无额外操作）-----
    }

    public override void Exit()
    {
        isTransitioning = false;
        isLandingToRun = false;
        idleDelayTimer = 0f;
        base.Exit();
    }
}