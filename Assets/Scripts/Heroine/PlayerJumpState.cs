using Spine;
using UnityEngine;

public class PlayerJumpState : PlayerState
{
    private const string JumpStart = "Jump/Jump";
    private const string JumpForward = "Jump/Jump_Forward";
    private const string JumpBackward = "Jump/Jump_Backward";
    private const string AirIdle = "Jump/Jump_OnAir";
    private const string AirForward = "Jump/Jump_OnAir_Forward";
    private const string AirBackward = "Jump/Jump_OnAir_Backward";
    private const string Landing = "Landing";
    private const string BackFlip = "Jump/Jump_BackFlip";
    private const string BackFlipLand = "Jump/Jump_BackFlip_Land";

    private const float AirMoveSpeed = 25f;
    private const float SwitchCooldown = 0.12f;

    [SerializeField] private float backflipDistance = 15f;
    [SerializeField] private float backflipEffectiveRatio = 0.7f;

    private bool isLanding = false;
    private float landingTimer = 0f;
    private float lastSwitchTime = -10f;
    private string currentDirectionAnim = AirIdle;

    private bool isBackflip = false;
    private float backflipTimer = 0f;
    private float backflipDuration = 0f;
    private float backflipEffectiveDuration = 0f;
    private float backflipInitialSpeed = 0f;
    private int backflipDirection = 0;

    private bool isBackflipLanding = false;
    private float backflipLandingTimer = 0f;

    public bool skipJump = false;

    public PlayerJumpState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
       

        isLanding = false;
        landingTimer = 0f;
        lastSwitchTime = -10f;
        isBackflip = false;
        isBackflipLanding = false;

        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;

        if (skipJump)
        {
            skipJump = false;
            float horizontal = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            UpdateAirLoop(horizontal);
            return;
        }

        float moveInput = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        bool isOppositePressed = (Mathf.Abs(moveInput) > 0.1f) && (Mathf.Sign(moveInput) != player.facingDirectionBeforeJump);

        if (isOppositePressed)
        {
            Jump();
            player.skeletonAnim.AnimationState.SetAnimation(0, BackFlip, false);

            isBackflip = true;
            backflipDuration = player.GetAnimationDuration(BackFlip);
            if (backflipDuration <= 0f)
            {
                backflipDuration = 0.5f;
                Debug.LogWarning($"后空翻动画 {BackFlip} 未找到，使用默认时长 0.5 秒");
            }
            backflipTimer = backflipDuration;

            backflipDirection = -player.facingDirectionBeforeJump;
            backflipEffectiveDuration = backflipDuration * backflipEffectiveRatio;
            backflipInitialSpeed = 2f * backflipDistance / backflipEffectiveDuration;

            currentDirectionAnim = AirIdle;
            return;
        }

        Jump();

        string start = JumpStart;
        string follow = AirIdle;
        bool isRunningJump = player.isRunningJump;

        if (isRunningJump)
        {
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            float relative = moveX * player.facingDirection;
            if (Mathf.Abs(moveX) > 0.01f)
            {
                if (relative > 0) { start = JumpForward; follow = AirForward; }
                else { start = JumpBackward; follow = AirBackward; }
                player.isRunningJump = false;
            }
            else { player.isRunningJump = false; }
        }

        var track1 = player.skeletonAnim.AnimationState.SetAnimation(0, start, false);
        var track2 = player.skeletonAnim.AnimationState.AddAnimation(0, follow, true, 0f);
        if (isRunningJump)
        {
            track1.MixDuration = 0f;
            track2.MixDuration = 0f;
        }
        currentDirectionAnim = follow;
    }

    public override void Update()
    {
        base.Update();

        float horizontalInput = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        bool isGrounded = player.IsGrounded();

        if (player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            // ★ 根据是否在地面选择攻击类型
            if (player.IsGrounded())
                player.ChangeState(player.AttackState);
            else
                player.ChangeState(player.AirAttackState);
            return;
        }


        if (isBackflip)
        {
            if (isGrounded && player.rb.velocity.y <= 0.1f)
            {
                isBackflip = false;
                player.skeletonAnim.AnimationState.SetAnimation(0, BackFlipLand, false);
                isBackflipLanding = true;
                backflipLandingTimer = player.GetAnimationDuration(BackFlipLand);
                if (backflipLandingTimer <= 0f) backflipLandingTimer = 0.8f;
                player.rb.velocity = new Vector2(0f, player.rb.velocity.y);
                return;
            }

            backflipTimer -= Time.deltaTime;

            float currentSpeed = 0f;
            float remaining = backflipTimer;
            float decelerationStart = backflipDuration - backflipEffectiveDuration;

            if (remaining > decelerationStart)
            {
                float effectiveRemaining = remaining - decelerationStart;
                currentSpeed = backflipInitialSpeed * (effectiveRemaining / backflipEffectiveDuration);
            }
            else
            {
                currentSpeed = 0f;
            }

            player.rb.velocity = new Vector2(backflipDirection * currentSpeed, player.rb.velocity.y);

            if (backflipTimer <= 0f)
            {
                isBackflip = false;
                player.skeletonAnim.AnimationState.SetAnimation(0, BackFlipLand, false);
                isBackflipLanding = true;
                backflipLandingTimer = player.GetAnimationDuration(BackFlipLand);
                if (backflipLandingTimer <= 0f) backflipLandingTimer = 0.8f;
                player.rb.velocity = new Vector2(0f, player.rb.velocity.y);
            }
            return;
        }

        if (isBackflipLanding)
        {
            if (player.inputActions.Player.Jump.WasPressedThisFrame())
            {
                player.currentJumpCount = 0;
                player.ChangeState(player.JumpState);
                return;
            }

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }

            backflipLandingTimer -= Time.deltaTime;
            if (backflipLandingTimer <= 0f)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        if (player.inputActions.Player.Jump.WasPressedThisFrame() && (isLanding || isGrounded))
        {
            player.currentJumpCount = 0;
            player.ChangeState(player.JumpState);
            return;
        }

        if (isLanding)
        {
            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        if (player.rb.velocity.y <= 0 && isGrounded)
        {
            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }
            else
            {
                player.skeletonAnim.AnimationState.SetAnimation(0, Landing, false);
                isLanding = true;
                landingTimer = player.GetAnimationDuration(Landing);
                return;
            }
        }

        if (isGrounded && Mathf.Abs(horizontalInput) > 0.01f && player.rb.velocity.y <= 0.1f)
        {
            player.ChangeState(player.RunState);
            return;
        }

        player.rb.velocity = new Vector2(horizontalInput * AirMoveSpeed, player.rb.velocity.y);
        UpdateAirLoop(horizontalInput);
    }

    private void UpdateAirLoop(float horizontal)
    {
        string targetLoop = AirIdle;
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            float relative = horizontal * player.facingDirection;
            targetLoop = (relative > 0f) ? AirForward : AirBackward;
        }

        if (targetLoop == currentDirectionAnim)
            return;

        if (Time.time - lastSwitchTime < SwitchCooldown) return;

        bool playStart = false;
        string startAnim = "";
        if ((currentDirectionAnim == AirForward && targetLoop == AirBackward) ||
            (currentDirectionAnim == AirBackward && targetLoop == AirForward))
        {
            playStart = true;
            startAnim = (targetLoop == AirForward) ? JumpForward : JumpBackward;
        }

        if (playStart)
        {
            player.skeletonAnim.AnimationState.SetAnimation(0, startAnim, false);
            player.skeletonAnim.AnimationState.AddAnimation(0, targetLoop, true, 0f);
        }
        else
        {
            player.skeletonAnim.AnimationState.SetAnimation(0, targetLoop, true);
        }

        currentDirectionAnim = targetLoop;
        lastSwitchTime = Time.time;
    }

    public override void Exit()
    {
        base.Exit();
        isLanding = false;
        landingTimer = 0f;
        isBackflip = false;
        isBackflipLanding = false;
        backflipLandingTimer = 0f;
    }

    private void Jump()
    {
        player.rb.velocity = new Vector2(player.rb.velocity.x, player.jumpForce);
    }
}