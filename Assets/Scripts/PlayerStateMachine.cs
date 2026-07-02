using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    // 当前状态
    private PlayerState currentState;
    // 所有状态
    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }

    public void Awake()
    {
        // 游戏启动时运行一次
        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
    }

    private void Start()
    {
        // 游戏对象激活时运行一次
        ChangeState(IdleState);
    }

    private void Update()
    {
        // 每帧运行一次
        currentState?.Update();
    }

    public void ChangeState(PlayerState newState)
    {
        // 切换到状态
        if (currentState == newState)
            return;

        currentState?.Exit();  // 退出当前状态
        currentState = newState;
        currentState.Enter();  // 进入新状态
    }
}
