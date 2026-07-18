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

        // ---- 落地动画播放阶段（仅用于上升攻击后的落地） ----
        if (isLandingAfterAttack)
        {
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;

            if (Mathf.Abs(moveX) > 0.01f)
            {
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }

            if (player.inputActions.Player.Jump.WasPressedThisFrame())
            {
                player.currentJumpCount = 0;
                player.ChangeState(player.JumpState);
                return;
            }

            var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            if (currentTrack != null && currentTrack.IsComplete)
            {
                player.ChangeState(player.IdleState);
                return;
            }
            return;
        }

        // ---- ★ 修改：落地处理 ----
        if (player.IsGrounded())
        {
            // 获取当前播放的动画名称
            var currentTrack = player.skeletonAnim.AnimationState.GetCurrent(0);
            string currentAnim = currentTrack?.Animation?.Name;
            bool isAttackDown = (currentAnim == AttackDown);

            // 如果是下落攻击，立即终止攻击，直接切换到 Idle 或 Run
            if (isAttackDown)
            {
                float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
                if (Mathf.Abs(moveX) > 0.01f)
                {
                    player.playLandingToRun = true;
                    player.ChangeState(player.RunState);
                }
                else
                {
                    player.ChangeState(player.IdleState);
                }
                return;
            }

            // 上升攻击或未明确时，依旧使用原有 Landing2 逻辑（需要可取消窗口）
            if (player.canCancelAttack)
            {
                player.skeletonAnim.AnimationState.SetAnimation(0, Landing, false);
                isLandingAfterAttack = true;
                return;
            }
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