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
        player.skeletonAnim.AnimationState.SetAnimation(0, RunStartAnim, false);
        player.skeletonAnim.AnimationState.AddAnimation(0, RunLoopAnim, true, 0f);
    }

    private void PlayRunLoopDirectly()
    {
        player.skeletonAnim.AnimationState.SetAnimation(0, RunLoopAnim, true);
    }

    private void PlayTurningThenRun()
    {
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
            // ★ 检查是否强制跳过 Run_Start
            if (player.forceRunLoop)
            {
                player.forceRunLoop = false; // 重置标志
                PlayRunLoopDirectly();       // 直接播放 Run 循环
            }
            else
            {
                PlayRunWithStart();          // 正常播放 Run_Start → Run
            }
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

        if (player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.AttackState);
            return;
        }
        if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && isGrounded)
        {
            player.facingDirectionBeforeJump = player.facingDirection;
            player.isRunningJump = true;
            player.ChangeState(player.JumpState);
            return;
        }

        player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);

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

        if (isLandingToRun)
        {
            if (Mathf.Approximately(moveX, 0f))
            {
                player.rb.velocity = new Vector2(0, player.rb.velocity.y);
                player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
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

        if (isTransitioning)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.ChangeState(player.AttackState);
                return;
            }
            if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && isGrounded)
            {
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = true;
                player.ChangeState(player.JumpState);
                return;
            }

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
                player.skeletonAnim.AnimationState.SetAnimation(0, RunToIdleAnim, false);
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