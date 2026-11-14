using Player;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人「大脑」基类：只做决策，移动/动画/攻击/血量分别交给
    /// EnemyMotor / EnemyAnimDriver / EnemyAttackModule / EnemyHealth。
    ///
    /// 新敌人做法：
    ///   1) 新建一个类继承 EnemyBrainBase，实现 Tick()
    ///   2) 在预制体上挂 EnemyController + 自己的 Brain + 自己的 EnemyAttackModule
    ///   3) 需要数值就用 EnemyProfile（ScriptableObject）
    /// </summary>
    public abstract class EnemyBrainBase : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] protected Transform target;
        [SerializeField] protected string[] targetTags = { "PlayerPresence", "Player" };

        protected EnemyMotor Motor { get; private set; }
        protected EnemyAnimDriver Anim { get; private set; }
        protected EnemyAttackModule Attack { get; private set; }
        protected EnemyHealth Health { get; private set; }

        public Transform Target => target;
        public bool IsReady { get; private set; }

        public virtual void Init(EnemyMotor motor, EnemyAnimDriver anim, EnemyAttackModule attack, EnemyHealth health)
        {
            Motor = motor;
            Anim = anim;
            Attack = attack;
            Health = health;

            if (target == null) target = FindTarget();
            IsReady = true;
        }

        /// <summary>每帧决策（由 EnemyController.Update 调用）。</summary>
        public abstract void Tick();

        /// <summary>Spine / Animator 动画事件转发（由 EnemyController.SendEvent 调用）。</summary>
        public virtual void OnAnimEvent(string eventName) { }

        /// <summary>死亡时回调（由 EnemyHealth.Die 调用），用于停协程、清状态。</summary>
        public virtual void OnDeath() { }

        /// <summary>受击回调（由 EnemyHealth.TakeDamage 调用），用于硬直/打断。</summary>
        public virtual void NotifyHit() { }

        // ------------------------------------------------------------------
        //  通用工具（所有敌人都能用）
        // ------------------------------------------------------------------

        protected Transform FindTarget()
        {
            if (targetTags != null)
            {
                for (int i = 0; i < targetTags.Length; i++)
                {
                    if (string.IsNullOrEmpty(targetTags[i])) continue;
                    GameObject go = GameObject.FindGameObjectWithTag(targetTags[i]);
                    if (go != null) return go.transform;
                }
            }

            // 兜底：场景里没有打 tag 时，直接按玩家组件找
            var pc = FindObjectOfType<Player.PlayerController>();
            if (pc != null) return pc.transform;

            var pm = FindObjectOfType<Player.PlayerMotor>();
            if (pm != null) return pm.transform;

            return null;
        }

        /// <summary>玩家是否已死亡（兼容 IDamageable 与束缚系统）。</summary>
        protected bool IsPlayerDead()
        {
            if (target == null) return false;

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsDead) return true;

            var pb = PlayerBinding.Instance;
            return pb != null && pb.Phase == BindingPhase.Dead;
        }

        /// <summary>玩家是否正被其它敌人束缚。</summary>
        protected bool IsPlayerBound()
        {
            var pb = PlayerBinding.Instance;
            return pb != null && pb.IsActive;
        }

        /// <summary>取玩家束缚系统（可能为 null）。</summary>
        protected PlayerBinding GetPlayerBinding()
        {
            return PlayerBinding.Instance;
        }

        /// <summary>水平距离（2D ACT 常用）。</summary>
        protected float HorizontalDistanceTo(Transform other)
        {
            if (other == null) return float.PositiveInfinity;
            return Mathf.Abs(other.position.x - transform.position.x);
        }

        /// <summary>朝目标转向时的方向符号。</summary>
        protected int DirectionTo(Transform other)
        {
            if (other == null) return Motor != null ? Motor.Facing : 1;
            return other.position.x >= transform.position.x ? 1 : -1;
        }
    }
}
