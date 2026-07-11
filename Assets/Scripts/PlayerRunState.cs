using UnityEngine;

public class PlayerRunState : PlayerState
{
    public PlayerRunState(PlayerStateMachine player) : base(player) { }

    // --- 常量 ---
    private const string RunStartAnim = "Run_Start";
    private const string RunLoopAnim = "Run";
    private const string RunTurningAnim = "Run_Turning";
    private const string RunToIdleAnim = "Run_to_Idle";
    private const string LandingToRunAnim = "Landing_to_Run";

    // --- 过渡状态 ---
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private float transitionDuration = 1.250f;   // Run_to_Idle 时长

    private bool isLandingToRun = false;
    private float landingToRunTimer = 0f;

    // --- 转向状态 ---
    private bool isTurning = false;
    private float turningTimer = 0f;

    // --- 辅助方法 ---
    private void PlayRunWithStart()
    {
        player.skeletonAnim.AnimationState.SetAnimation(0, RunStartAnim, false);
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
            // ★ 瞬时切换，消除扭曲
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


        // 立即应用速度
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

        isTransitioning = false;
        transitionTimer = 0f;
        isTurning = false;
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

        // ----- 2. 速度设置（每帧执行，不受任何状态影响）-----
        player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);

        // ----- 3. 方向翻转 + 转向检测（仅在地面跑步时触发）-----
        if (Mathf.Abs(moveX) > 0.01f && isGrounded)
        {
            int newDirection = (moveX > 0) ? 1 : -1;

            // 检测方向改变且未在转向中、未在过渡中、未在落地过渡中
            if (newDirection != player.facingDirection && !isTurning && !isTransitioning && !isLandingToRun)
            {
                // 立即翻转朝向
                player.skeletonAnim.Skeleton.ScaleX = newDirection;
                player.facingDirection = newDirection;

                // 播放转向动画
                player.skeletonAnim.AnimationState.SetAnimation(0, RunTurningAnim, false);
                isTurning = true;
                turningTimer = player.GetAnimationDuration(RunTurningAnim);
            }
            else
            {
                // 如果方向变了但未触发转向，确保朝向正确
                if (newDirection != player.facingDirection)
                {
                    player.skeletonAnim.Skeleton.ScaleX = newDirection;
                    player.facingDirection = newDirection;
                }
            }
        }

        // ----- 4. 处理转向动画（如果正在播放）-----
        if (isTurning)
        {
            turningTimer -= Time.deltaTime;
            if (turningTimer <= 0f)
            {
                // 转向结束，恢复跑步（用 Run_Start 衔接）
                PlayRunWithStart();
                isTurning = false;
            }
            // 转向期间跳过其他过渡
            return;
        }

        // ----- 5. 处理 Landing_to_Run 过渡（修改：松开方向键时播放 Run_to_Idle）-----
        if (isLandingToRun)
        {
            // ★ 修改：如果松开了方向键，播放 Run_to_Idle 过渡，而不是直接切 Idle
            if (Mathf.Approximately(moveX, 0f))
            {
                player.rb.velocity = new Vector2(0, player.rb.velocity.y);
                // 播放 Run_to_Idle 过渡，并进入 isTransitioning 状态，让计时器切换 Idle
                player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
                isLandingToRun = false; // 清除落地过渡标志
                isTransitioning = true;
                transitionTimer = transitionDuration; // 设置时长
                return;
            }

            landingToRunTimer -= Time.deltaTime;
            if (landingToRunTimer <= 0f)
            {
                PlayRunWithStart();
                isLandingToRun = false;
            }
            return;
        }

        // ----- 6. 处理 Run_to_Idle 过渡（添加取消逻辑）-----
        if (isTransitioning)
        {
            // 如果按了方向键，取消过渡，回到跑步
            if (Mathf.Abs(moveX) > 0.01f)
            {
                isTransitioning = false;
                PlayRunWithStart();
                return;
            }

            transitionTimer -= Time.deltaTime;
            if (transitionTimer <= 0f)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        // ----- 7. 检测是否进入 Run_to_Idle（松方向键）-----
        // ★ 关键：如果正在播放 Run_Start，跳过检测
        var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
        if (currentTrack != null && currentTrack.Animation.Name == RunStartAnim)
        {
            return;
        }

        if (Mathf.Approximately(moveX, 0f))
        {
            isTransitioning = true;
            transitionTimer = transitionDuration;
            player.rb.velocity = new Vector2(0, player.rb.velocity.y);
            player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
            return;
        }

        // 如果到这里，说明正常跑步且没有其他状态，无需额外操作
    }

    public override void Exit()
    {
        isTransitioning = false;
        isLandingToRun = false;
        isTurning = false;
        base.Exit();
    }
}