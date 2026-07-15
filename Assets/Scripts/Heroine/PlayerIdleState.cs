// 站立状态

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
       
        // 进入这个状态时，需要做的事
        base.Enter();
        // 动画切换到Idle
        player.skeletonAnim.AnimationState.SetAnimation(0, "Idle", true);  // 循环播放
        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;   // 保持朝向
        player.rb.velocity = new Vector2(0, player.rb.velocity.y);// 停止水平移动 保持 Y 轴速度不变（让力继续起作用）
        player.currentJumpCount = 0;// 重置跳跃计数器

        
    }

    public override void Update()
    {
        // 何时切换到其他状态
        base.Update();
        
        // 生命值为0，切换到死亡状态

        // 如果玩家按下了移动键，就切换到跑步状态
        float moveX = player.inputActions.Player.Move.ReadValue<Vector2>().x;
        if (Mathf.Abs(moveX) > 0.01f)
        {
            player.ChangeState(player.RunState);
            return;
        }
        // 玩家按下攻击键，切换到攻击状态 
        if (player.inputActions.Player.Attack.WasPressedThisFrame()) //检查按钮“按下瞬间”应该用 WasPressedThisFrame() 方法
        {
            player.ChangeState(player.AttackState);
            return;
        }
        // 按下跳跃键，切换到跳跃状态
        if (player.inputActions.Player.Jump.WasPressedThisFrame() && player.currentJumpCount < player.maxJumpCount && player.IsGrounded())
        {
            player.facingDirectionBeforeJump = player.facingDirection; // ★ 保存跳跃前朝向
            player.ChangeState(player.JumpState);
            return;
        }
    }

    public override void Exit() 
    { 
        base.Exit();
    }
}
