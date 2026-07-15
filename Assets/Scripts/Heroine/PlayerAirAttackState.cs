using UnityEngine;

public class PlayerAirAttackState : PlayerState
{
    private const string AttackUp = "Jump/Jump_Attack_Up";
    private const string AttackDown = "Jump/Jump_Attack_Down";
    private const string Landing = "Landing2";

    private float timer;
    private float duration;
    private bool isLandingAfterAttack = false;

    public PlayerAirAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        player.canCancelAttack = false;

        isLandingAfterAttack = false;

        bool isGoingUp = player.rb.velocity.y > 0.1f;
        string animName = isGoingUp ? AttackUp : AttackDown;
        player.skeletonAnim.AnimationState.SetAnimation(0, animName, false);
        duration = player.GetAnimationDuration(animName);
        timer = duration;
    }

    public override void Update()
    {
        base.Update();

        // ---- 落地动画播放阶段 ----
        if (isLandingAfterAttack)
        {
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

            // 按方向键 → 跑步（带 Landing_to_Run 过渡）
            if (Mathf.Abs(moveX) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }

            // 按跳跃键 → 重新起跳
            if (player.inputActions.Player.Jump.WasPressedThisFrame())
            {
                player.currentJumpCount = 0;
                player.ChangeState(player.JumpState);
                return;
            }

            // ★ 检查当前动画是否播放完毕
            var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (currentTrack != null && currentTrack.IsComplete)
            {
                player.ChangeState(player.IdleState);
                return;
            }

            // 未结束则继续等待
            return;
        }

        // ---- 可取消且落地 → 播放 Landing2 ----
        if (player.canCancelAttack && player.IsGrounded())
        {
            // 播放落地动画（非循环）
            player.skeletonAnim.AnimationState.SetAnimation(0, Landing, false);
            isLandingAfterAttack = true;
            return;
        }

        // ---- 空中攻击动画正常计时 ----
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            player.JumpState.skipJump = true;
            player.ChangeState(player.JumpState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        player.canCancelAttack = false;
        isLandingAfterAttack = false;
    }
}