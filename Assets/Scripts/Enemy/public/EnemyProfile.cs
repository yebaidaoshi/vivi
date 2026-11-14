using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人通用数值表（ScriptableObject）。新敌人建议一个敌人一份 Profile，
    /// 把「血量 / 速度 / 视野 / 攻击距离 / 冷却 / 动画名」都放在这里，
    /// 代码只管行为，数值改 asset 就行。
    ///
    /// 菜单：Assets → Create → Enemy → Enemy Profile
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyProfile", menuName = "Enemy/Enemy Profile")]
    public class EnemyProfile : ScriptableObject
    {
        [Header("基础")]
        public int maxHealth = 100;
        [Tooltip("死亡后尸体保留时间（秒）。")]
        public float corpseDuration = 4.7f;

        [Header("移动")]
        public float walkSpeed = 3f;
        public float runSpeed = 10f;
        public float chaseSpeed = 10f;
        [Tooltip("距离目标多近时从 Run 降为 Walk。")]
        public float walkThreshold = 4f;

        [Header("感知")]
        public float viewDistance = 8f;
        [Tooltip("视野总角度（度）。")]
        public float viewAngle = 60f;
        public float backDetectDistance = 2f;
        [Tooltip("超过这个距离就丢失目标。")]
        public float loseTargetDistance = 14f;

        [Header("战斗")]
        public int damage = 20;
        public float attackRange = 2.5f;
        public float attackCooldown = 0.8f;
        public float hitStun = 0.3f;
        public float knockbackForce = 15f;

        [Header("待机 / 巡逻")]
        public bool startPatrolling = true;
        public Vector2 patrolRangeX = new Vector2(-4f, 4f);
        public Vector2 idleDuration = new Vector2(3f, 5f);

        [Header("动画状态名")]
        public string idleAnim = "Idle";
        public string moveAnim = "Locomotion";
        public string attackAnim = "Attack";
        public string hitAnim = "Hit";
        public string deathAnim = "Death";
        public string climbAnim = "Jump_High";

        [Header("图层")]
        public LayerMask groundMask;
        public LayerMask obstacleMask;
    }
}
