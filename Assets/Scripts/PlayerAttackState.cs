using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerAttackState : PlayerState
{   private float attackDuration = 0.5f;   // 攻击动画持续时长（与动画长度匹配）
    private float timer;
    public PlayerAttackState(PlayerStateMachine player) : base(player) { }
    
    public override void Enter()
    {
        // 进入这个状态时，需要做的事
        Debug.Log("进入Attack状态");
        base.Enter();
        player.currentJumpCount = 0;// 重置跳跃计数器
        player.rb.velocity = new Vector2(0, player.rb.velocity.y);// 停止水平移动 保持 Y 轴速度不变（让力继续起作用）

        timer = attackDuration;
        player.skeletonAnim.Skeleton.ScaleX = player.facingDirection;   // 保持攻击朝向
        // 动画切换到Attack
        player.skeletonAnim.AnimationState.SetAnimation(0, "Attack", false); // 不循环
        Attack();
    }

    public override void Update()
    {
        // 何时切换到其他状态
        base.Update();
        // 生命值为0，切换到死亡状态

        //
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            player.ChangeState(player.IdleState);
        }

    }

    public override void Exit() 
    {
        // 离开这个状态时，需要做的事
        base.Exit();
    }

    private void Attack()
    {
        // 攻击逻辑

        // 这里可以添加攻击动画、伤害计算等逻辑

        Debug.Log("Player is attacking!");
        // trigger 判断碰撞

        // 判断体力

    }
}
