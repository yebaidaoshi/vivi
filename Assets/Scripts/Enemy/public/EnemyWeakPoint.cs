using Player;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 弱点判定（原版 HitBox_WeakPoint）。
    /// 用法：在敌人身上新建一个子物体 → 加 Collider2D（Is Trigger 关掉也行）+ 本组件，
    /// 玩家子弹/近战命中该碰撞体时走这里，按倍率放大伤害。
    /// 没有本组件的普通碰撞体仍然走 EnemyHealth 的普通伤害。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyWeakPoint : MonoBehaviour, IDamageable
    {
        [Tooltip("留空自动向父物体找 EnemyHealth")]
        [SerializeField] private EnemyHealth health;

        [Tooltip("弱点伤害倍率")]
        [SerializeField] private float damageMultiplier = 2f;

        [Tooltip("命中弱点时的额外眩晕值")]
        [SerializeField] private float bonusStun = 20f;

        [Tooltip("命中弱点时是否额外顿帧")]
        [SerializeField] private bool extraHitStop = true;

        public bool IsDead => health == null || health.IsDead;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<EnemyHealth>();
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            if (health == null) health = GetComponentInParent<EnemyHealth>();
            if (health == null || health.IsDead) return;

            int final = Mathf.Max(1, Mathf.RoundToInt(damage * damageMultiplier));
            health.TakeDamage(final, knockback, attacker, true);   // critical

            var gauge = GetComponentInParent<EnemyStunGauge>();
            if (gauge != null) gauge.AddStun(bonusStun);

            if (extraHitStop)
            {
                var stop = GetComponentInParent<EnemyHitStop>();
                if (stop != null) stop.Play(0.09f, 0.02f);
            }
        }
    }
}
