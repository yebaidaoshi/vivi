namespace Enemy
{
    /// <summary>
    /// 敌人难度档位（原版 FSM 里 Easy / Normal / Hard 三个分支）。
    /// 用来缩放移动速度、攻击冷却、警戒/受击时长，不改判定框和动画。
    /// </summary>
    public enum EnemyDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    public static class EnemyDifficultyUtil
    {
        /// <summary>移动速度倍率。</summary>
        public static float SpeedMultiplier(this EnemyDifficulty d)
        {
            switch (d)
            {
                case EnemyDifficulty.Easy: return 0.85f;
                case EnemyDifficulty.Hard: return 1.15f;
                default: return 1f;
            }
        }

        /// <summary>攻击冷却倍率（越大越慢）。</summary>
        public static float CooldownMultiplier(this EnemyDifficulty d)
        {
            switch (d)
            {
                case EnemyDifficulty.Easy: return 1.35f;
                case EnemyDifficulty.Hard: return 0.75f;
                default: return 1f;
            }
        }
    }
}
