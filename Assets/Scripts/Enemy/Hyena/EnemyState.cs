namespace Enemy
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        WalkApproach,
        RunApproach,
        RunEnd,
    }

    public interface IState
    {
        void Enter();
        void Update();
        void Exit();
    }
}