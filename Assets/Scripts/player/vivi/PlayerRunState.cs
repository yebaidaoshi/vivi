using UnityEngine;

public class PlayerRunState : PlayerState
{
    public PlayerRunState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Update()
    {
        base.Update();
        if (player.onGround && player.inputActions.Player.Move.ReadValue<Vector2>().x == 0)
        {
            player.ChangeState(player.IdleState);
        }
    }

    public override void Exit() 
    { 
        base.Exit();
    }
}
