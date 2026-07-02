// 站立状态

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        Debug.Log("进入Idle状态");
        // 进入这个状态时，需要做的事
        base.Enter();
        // 动画切换到Idle
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
        // 按下跳跃键，切换到跳跃状态
    }

    public override void Exit() 
    { 
        base.Exit();
    }
}
