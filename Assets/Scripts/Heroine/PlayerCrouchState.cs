using UnityEngine;

public class PlayerCrouchState : PlayerState
{
    private const string CrouchAnim = "Crouch/Crouch";
    private const string CrouchingAnim = "Crouch/Crouching";
    private const string CrouchToIdleAnim = "Crouch/Crouch_To_Idle";

    private bool isCrouching = false;
    private bool isTransitioningToIdle = false;
    private bool isInCrouchTransition = false;
    private bool isFromSlideRoll = false;

    public PlayerCrouchState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入CrouchState");
        player.canCancelAttack = false;
        isCrouching = false;
        isTransitioningToIdle = false;
        isInCrouchTransition = false;
        isFromSlideRoll = false;

        if (player.forceCrouching)
        {
            player.forceCrouching = false;
            var crouchTrack = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchingAnim, true);
            crouchTrack.MixDuration = 0f;
            crouchTrack.AttachmentThreshold = 0f;
            isCrouching = true;
            isFromSlideRoll = true;
            Debug.Log("强制直接进入 Crouching 循环（来自滑铲翻滚）");
            return;
        }

        var enterTrack = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchAnim, false);
        enterTrack.MixDuration = 0f;
        enterTrack.AttachmentThreshold = 0f;
        isInCrouchTransition = true;
        Debug.Log("播放 Crouch 过渡动画");
    }

    public override void Update()
    {
        base.Update();

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        bool isGrounded = player.IsGrounded();

        if (isTransitioningToIdle)
        {
            if (player.inputActions.Player.Crouch.WasPressedThisFrame())
            {
                var animTrack = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchingAnim, true);
                animTrack.MixDuration = 0f;
                animTrack.AttachmentThreshold = 0f;
                isTransitioningToIdle = false;
                isCrouching = true;
                isFromSlideRoll = false;
                Debug.Log("Crouch_To_Idle 期间按 S → 回到蹲下循环");
                return;
            }

            if (Mathf.Abs(moveX) > 0.01f)
            {
                player.skeletonAnim.AnimationState.SetEmptyAnimation(0, 0f);
                var runTrack = player.skeletonAnim.AnimationState.SetAnimation(0, "Run", true);
                runTrack.MixDuration = 0f;
                runTrack.AttachmentThreshold = 0f;
                int newDir = (moveX > 0) ? 1 : -1;
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                player.skeletonAnim.Skeleton.ScaleX = newDir;
                player.facingDirection = newDir;
                player.ChangeState(player.RunState);
                return;
            }

            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.ChangeState(player.AttackState);
                return;
            }

            if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && isGrounded)
            {
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = false;
                player.ChangeState(player.JumpState);
                return;
            }

            var currentAnimTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (currentAnimTrack != null && currentAnimTrack.IsComplete)
            {
                player.ChangeState(player.IdleState);
                isTransitioningToIdle = false;
            }
            return;
        }

        if (isInCrouchTransition)
        {
            // ★ 在过渡期间按攻击键 → 直接进入蹲下攻击
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.ChangeState(player.CrouchAttackState);
                return;
            }

            if (!player.inputActions.Player.Crouch.IsPressed())
            {
                isInCrouchTransition = false;
                var idleTrack = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchToIdleAnim, false);
                idleTrack.MixDuration = 0f;
                idleTrack.AttachmentThreshold = 0f;
                isTransitioningToIdle = true;
                isFromSlideRoll = false;
                Debug.Log("Crouch 过渡期间松开 S → 播放 Crouch_To_Idle");
                return;
            }

            var transitionTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (transitionTrack != null && transitionTrack.IsComplete)
            {
                isInCrouchTransition = false;
                var loopTrack = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchingAnim, true);
                loopTrack.MixDuration = 0f;
                loopTrack.AttachmentThreshold = 0f;
                isCrouching = true;
                Debug.Log("Crouch 过渡完成，进入 Crouching 循环");
            }
            return;
        }

        if (isCrouching)
        {
            if (player.inputActions.Player.Attack.WasPressedThisFrame())
            {
                player.ChangeState(player.CrouchAttackState);
                return;
            }

            if (!player.inputActions.Player.Crouch.IsPressed())
            {
                var toIdleTrack = player.skeletonAnim.AnimationState.SetAnimation(0, CrouchToIdleAnim, false);
                toIdleTrack.MixDuration = 0f;
                toIdleTrack.AttachmentThreshold = 0f;
                isTransitioningToIdle = true;
                isCrouching = false;
                isFromSlideRoll = false;
                Debug.Log("松开 S → 播放 Crouch_To_Idle");
                return;
            }

            if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && isGrounded)
            {
                player.facingDirectionBeforeJump = player.facingDirection;
                player.isRunningJump = false;
                player.ChangeState(player.JumpState);
                return;
            }

            if (!isFromSlideRoll && Mathf.Abs(moveX) > 0.01f)
            {
                player.skeletonAnim.AnimationState.SetEmptyAnimation(0, 0f);
                var runTrack2 = player.skeletonAnim.AnimationState.SetAnimation(0, "Run", true);
                runTrack2.MixDuration = 0f;
                runTrack2.AttachmentThreshold = 0f;
                int newDir = (moveX > 0) ? 1 : -1;
                player.rb.velocity = new Vector2(moveX * player.moveSpeed, player.rb.velocity.y);
                player.skeletonAnim.Skeleton.ScaleX = newDir;
                player.facingDirection = newDir;
                player.ChangeState(player.RunState);
                return;
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        isCrouching = false;
        isTransitioningToIdle = false;
        isInCrouchTransition = false;
        isFromSlideRoll = false;
        player.canCancelAttack = false;
        player.forceCrouching = false;
        Debug.Log("退出CrouchState");
    }
}