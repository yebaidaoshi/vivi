using UnityEngine;

public class PlayerAttackState : PlayerState
{
    public PlayerAttackState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        // 进入这个状态时，需要做的事
        base.Enter();
        // 动画切换到Attack
        Attack();
    }

    public override void Update()
    {
        // 何时切换到其他状态
        base.Update();
        // 生命值为0，切换到死亡状态
        // 如果玩家没有按下攻击键，就切换到站立状态
        if (!player.inputActions.Player.Attack.triggered)
        {
            player.ChangeState(player.IdleState);
            return;
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
