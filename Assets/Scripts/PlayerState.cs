using UnityEngine;

public abstract class PlayerState
{
    public PlayerState(PlayerStateMachine player)
    {
        this.player = player;
    }

    protected PlayerStateMachine player;

    public virtual void Enter() { }

    public virtual void Update() { }

    public virtual void Exit() { }
}
