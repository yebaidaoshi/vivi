using UnityEngine;

public class PlayerAirAttackState : PlayerState
{
    private const string AttackUp = "Jump/Jump_Attack_Up";
    private const string AttackDown = "Jump/Jump_Attack_Down";
    private const string Landing = "Landing";

    private float timer;
    private float duration;
    private bool isLandingAfterAttack = false;
    private float landingTimer = 0f;

    public PlayerAirAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("进入空中攻击状态");

        isLandingAfterAttack = false;
        landingTimer = 0f;

        bool isGoingUp = player.rb.velocity.y > 0.1f;
        string animName = isGoingUp ? AttackUp : AttackDown;
        player.skeletonAnim.AnimationState.SetAnimation(0, animName, false);
        duration = player.GetAnimationDuration(animName);
        timer = duration;

        Debug.Log($"空中攻击：{animName}，时长 {duration}");
    }

    public override void Update()
    {
        base.Update();

        if (isLandingAfterAttack)
        {
            // 落地过渡阶段
            float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(moveX) > 0.01f)
            {
                // 有方向输入，切换到跑步状态，播放 Landing_to_Run
                player.playLandingToRun = true;
                player.ChangeState(player.RunState);
                return;
            }

            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f)
            {
                // 无输入，切换到 Idle
                player.ChangeState(player.IdleState);
            }
            return;
        }

        // 检测落地
        if (player.IsGrounded())
        {
            // 落地，播放落地动画
            player.skeletonAnim.AnimationState.SetAnimation(0, Landing, false);
            isLandingAfterAttack = true;
            landingTimer = player.GetAnimationDuration(Landing);
            Debug.Log($"空中攻击落地，播放落地动画，时长 {landingTimer}");
            return;
        }

        // 正常计时
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            // 攻击动画结束，回到跳跃状态
            player.JumpState.skipJump = true;
            player.ChangeState(player.JumpState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        Debug.Log("退出空中攻击状态");
    }
}