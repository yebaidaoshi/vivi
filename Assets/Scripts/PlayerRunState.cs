using UnityEngine;

public class PlayerRunState : PlayerState
{
    public PlayerRunState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        // 进入这个状态时，需要做的事
        base.Enter();
        // 动画切换到Run
    }

    public override void Update()
    {
        // 何时切换到其他状态
        base.Update();
        // 生命值为0，切换到死亡状态
        // 如果玩家没有按下移动键，就切换到站立状态
        if (player.inputActions.Player.Move.ReadValue<Vector2>().x == 0)
        {
            player.ChangeState(player.IdleState);
            return;
        }
        // 玩家按下攻击键，切换到攻击状态
        


        // 移动
        player.rb.velocity = new Vector2(player.inputActions.Player.Move.ReadValue<Vector2>().x *3, player.rb.velocity.y);
    }

    public override void Exit() 
    { 
        base.Exit();
    }
}
