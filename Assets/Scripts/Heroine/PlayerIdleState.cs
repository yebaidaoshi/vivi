// 站立状态

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        player.skeletonAnim.AnimationState.SetAnimation(0, "Idle", true);
        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;
        player.rb.velocity = new Vector2(0, player.rb.velocity.y);
        player.currentJumpCount = 0;
    }

    public override void Update()
    {
        base.Update();

        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

        // ★ 同时按下 S + 攻击 → 直接进入蹲下攻击
        if (player.inputActions.Player.Crouch.IsPressed() &&
            player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.CrouchAttackState);
            return;
        }

        if (Mathf.Abs(moveX) > 0.01f)
        {
            player.ChangeState(player.RunState);
            return;
        }

        if (player.inputActions.Player.Attack.WasPressedThisFrame())
        {
            player.ChangeState(player.AttackState);
            return;
        }

        if (player.inputActions.Player.Jump.WasPressedThisFrame() &&
            player.currentJumpCount < player.maxJumpCount &&
            player.IsGrounded())
        {
            player.facingDirectionBeforeJump = player.facingDirection;
            player.ChangeState(player.JumpState);
            return;
        }

        if (player.inputActions.Player.Crouch.WasPressedThisFrame())
        {
            player.ChangeState(player.CrouchState);
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}