using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Update()
    {
        base.Update();
        if (player.physicsCheck.onGround && player.inputActions.Player.Jump.triggered)  // 跳跃条件
        {
            player.ChangeState(new PlayerJumpState(player));
            return;
        }

        if (player.inputActions.Player.Move.ReadValue<Vector2>().x != 0)  // 移动条件
        {
            player.ChangeState(new PlayerRunState(player));
            return;
        }
    }

    public override void Exit() 
    { 
        base.Exit();
    }
}
