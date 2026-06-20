using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public Moren inputActions;
    public PhysicsCheck physicsCheck;
    public Rigidbody2D rb;
    public Animator animator;

    // states
    private PlayerState currentState;
    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }

    public void Awake()
    {
        inputActions = new Moren();
        physicsCheck = GetComponent<PhysicsCheck>();
        rb = GetComponent<Rigidbody2D>();

        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
    }

    private void Start()
    {
        ChangeState(IdleState);
    }

    private void Update()
    {
        currentState?.Update();
    }

    public void ChangeState(PlayerState newState)
    {
        if (currentState == newState)
            return;

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }
}
