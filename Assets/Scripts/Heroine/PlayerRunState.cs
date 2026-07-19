using UnityEngine;

public class PlayerRunState : PlayerState
{
    public PlayerRunState(PlayerStateMachine player) : base(player) { }

    private const string RunStartAnim = "Run_Start";
    private const string RunLoopAnim = "Run";
    private const string RunTurningAnim = "Run_Turning";
    private const string RunToIdleAnim = "Run_to_Idle";
    private const string LandingToRunAnim = "Landing_to_Run";

    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private bool isLandingToRun = false;
    private float landingToRunTimer = 0f;
    private float idleDelayTimer = 0f;
    private const float IdleDelayThreshold = 0.05f;

    private void PlayRunWithStart()
    {
        var track = player.skeletonAnim.AnimationState.SetAnimation(0, RunStartAnim, false);
        track.MixDuration = 0f;
        track.AttachmentThreshold = 0f;
        player.skeletonAnim.AnimationState.AddAnimation(0, RunLoopAnim, true, 0f);
    }

    private void PlayRunLoopDirectly()
    {
        var track = player.skeletonAnim.AnimationState.SetAnimation(0, RunLoopAnim, true);
        track.MixDuration = 0f;
        track.AttachmentThreshold = 0f;
    }

    private void PlayTurningThenRun()
    {
        var track = player.skeletonAnim.AnimationState.SetAnimation(0, RunTurningAnim, false);
        track.MixDuration = 0f;
        track.AttachmentThreshold = 0f;
        player.skeletonAnim.AnimationState.AddAnimation(0, RunLoopAnim, true, 0f);
    }

    public override void Enter()
    {
        base.Enter();
        player.currentJumpCount = 0;


        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

        var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
        bool isRunLoopPlaying = (currentTrack != null && currentTrack.Animation.Name == RunLoopAnim && currentTrack.Loop);

        if (!isRunLoopPlaying)
        {
            if (player.playLandingToRun)
            {
                var track = player.skeletonAnim.AnimationState.SetAnimation(0, LandingToRunAnim, false);
                track.MixDuration = 0f;
                track.AttachmentThreshold = 0f;
                isLandingToRun = true;
                landingToRunTimer = player.GetAnimationDuration(LandingToRunAnim);
                player.playLandingToRun = false;
            }
            else
            {
                if (player.forceRunLoop)
                {
                    player.forceRunLoop = false;
                    PlayRunLoopDirectly();
                }
                else
                {
                    PlayRunWithStart();
                }
                isLandingToRun = false;
            }
        }
        else
        {
            player.forceRunLoop = false;
            isLandingToRun = false;
        }

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
        idleDelayTimer = 0f;
    }

    public override void Update()
    {
        base.Update();

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        bool isGrounded = player.IsGrounded();

        // 同时按下 S + 攻击 → 直接进入蹲下攻击（优先级最高）
        if (player.inputActions.Player.Crouch.IsPressed() &&
            player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.CrouchAttackState);
            return;
        }

        // 攻击键 → 进入攻击状态（连击计数已在 AttackState 中处理）
        if (player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.AttackState);
            return;
        }

        // 跳跃
        if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && isGrounded)
        {
            player.facingDirectionBeforeJump = player.facingDirection;
            player.isRunningJump = true;
            player.ChangeState(player.JumpState);
            return;
        }

        // 蹲下 → 根据当前状态进入滑铲或直接蹲下
        if (player.inputActions.Player.Crouch.WasPressedThisFrame())
        {
            if (isTransitioning || isLandingToRun)
            {
                player.ChangeState(player.CrouchState);
                return;
            }
            int dir = (Mathf.Abs(moveX) > 0.1f) ? (moveX > 0 ? 1 : -1) : player.facingDirection;
            player.SlideState.SetDirection(dir);
            player.ChangeState(player.SlideState);
            return;
        }

        player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);

        // 处理转向
        if (Mathf.Abs(moveX) > 0.01f && isGrounded)
        {
            int newDirection = (moveX > 0) ? 1 : -1;
            var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            bool isPlayingTurning = (currentTrack != null && currentTrack.Animation.Name == RunTurningAnim);
            bool isPlayingStart = (currentTrack != null && currentTrack.Animation.Name == RunStartAnim);

            if (newDirection != player.facingDirection && !isTransitioning && !isLandingToRun && !isPlayingStart && !isPlayingTurning)
            {
                player.skeletonAnim.Skeleton.ScaleX = newDirection;
                player.facingDirection = newDirection;
                PlayTurningThenRun();
            }
            else if (newDirection != player.facingDirection)
            {
                player.skeletonAnim.Skeleton.ScaleX = newDirection;
                player.facingDirection = newDirection;
            }
        }

        // Landing_to_Run 过渡处理
        if (isLandingToRun)
        {
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                player.ChangeState(player.CrouchState);
                return;
            }

            if (Mathf.Approximately(moveX, 0f))
            {
                player.rb.velocity = new Vector2(0, player.rb.velocity.y);
                var track = player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
                track.MixDuration = 0f;
                track.AttachmentThreshold = 0f;
                isLandingToRun = false;
                isTransitioning = true;
                transitionTimer = player.GetAnimationDuration(RunToIdleAnim);
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

        // Run_to_Idle 过渡处理
        if (isTransitioning)
        {
            if (Mathf.Abs(moveX) > 0.01f)
            {
                isTransitioning = false;
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                int newDir = (moveX > 0) ? 1 : -1;
                if (newDir != player.facingDirection)
                {
                    player.skeletonAnim.Skeleton.ScaleX = newDir;
                    player.facingDirection = newDir;
                    PlayTurningThenRun();
                }
                else
                {
                    PlayRunLoopDirectly();
                }
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

        // 静止一小段时间后播放 Run_to_Idle
        var currentTrack2 = player.skeletonAnim.AnimationState.GetCurrent(0);
        bool isPlayingTurning2 = (currentTrack2 != null && currentTrack2.Animation.Name == RunTurningAnim);
        bool isPlayingStart2 = (currentTrack2 != null && currentTrack2.Animation.Name == RunStartAnim);
        bool isPlayingLandingToRun = (currentTrack2 != null && currentTrack2.Animation.Name == LandingToRunAnim);

        if (isPlayingTurning2 || isPlayingStart2 || isPlayingLandingToRun)
        {
            idleDelayTimer = 0f;
            return;
        }

        if (Mathf.Approximately(moveX, 0f))
        {
            idleDelayTimer += Time.deltaTime;
            if (idleDelayTimer >= IdleDelayThreshold)
            {
                isTransitioning = true;
                transitionTimer = player.GetAnimationDuration(RunToIdleAnim);
                player.rb.velocity = new Vector2(0, player.rb.velocity.y);
                var track = player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
                track.MixDuration = 0f;
                track.AttachmentThreshold = 0f;
                idleDelayTimer = 0f;
            }
        }
        else
        {
            idleDelayTimer = 0f;
        }
    }

    public override void Exit()
    {
        isTransitioning = false;
        isLandingToRun = false;
        idleDelayTimer = 0f;
        base.Exit();
    }
}