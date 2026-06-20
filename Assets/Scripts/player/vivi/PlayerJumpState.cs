using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class PlayerJumpState : PlayerState
{
    public float jumpForce;

    public PlayerJumpState(PlayerStateMachine player) : base(player) { }

    public override void Enter()
    {
        base.Enter();
        Jump();
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

    public void Jump()
    {
        player.rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }
}