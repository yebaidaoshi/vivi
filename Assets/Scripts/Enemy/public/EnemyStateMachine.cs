using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 轻量状态机：给新敌人的 Brain 用，避免每个 AI 都手写 enum + switch。
    ///
    /// 用法：
    ///   enum HyenaState { Idle, Patrol, Chase, Attack }
    ///   readonly var fsm = new EnemyStateMachine&lt;HyenaState&gt;(HyenaState.Idle);
    ///
    ///   fsm.Changed += (from, to) => { /* 进入 to 时的初始化 */ };
    ///   fsm.Change(HyenaState.Chase);
    ///   fsm.Tick(Time.deltaTime);
    ///   if (fsm.Is(HyenaState.Chase)) ...
    /// </summary>
    public class EnemyStateMachine<TState> where TState : struct, Enum
    {
        private static readonly IEqualityComparer<TState> Comparer = EqualityComparer<TState>.Default;

        public TState Current { get; private set; }
        public TState Previous { get; private set; }
        public float TimeInState { get; private set; }

        /// <summary>(from, to)</summary>
        public event Action<TState, TState> Changed;

        public EnemyStateMachine(TState initial)
        {
            Current = initial;
            Previous = initial;
            TimeInState = 0f;
        }

        public bool Is(TState state)
        {
            return Comparer.Equals(Current, state);
        }

        /// <summary>切换状态；相同状态不会触发 Changed。</summary>
        public bool Change(TState next)
        {
            if (Comparer.Equals(Current, next)) return false;

            Previous = Current;
            Current = next;
            TimeInState = 0f;
            Changed?.Invoke(Previous, Current);
            return true;
        }

        /// <summary>每帧推进状态计时（在 Brain.Tick 里调用）。</summary>
        public void Tick(float deltaTime)
        {
            TimeInState += deltaTime;
        }

        public void Reset(TState state)
        {
            Current = state;
            Previous = state;
            TimeInState = 0f;
        }
    }
}
