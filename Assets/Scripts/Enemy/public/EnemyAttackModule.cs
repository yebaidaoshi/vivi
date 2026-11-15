using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人「攻击模块」基类：把攻击行为从大脑里拆出来，方便不同敌人复用/替换。
    ///
    /// 例子：
    ///   - 近战爪击（单一动画 + 判定盒）
    ///   - 飞扑束缚（鬣狗 EnemyCombat）
    ///   - 远程投射（炮台）
    ///
    /// 约定：判定盒开关、伤害结算、冷却都在模块内部处理；
    /// 大脑只负责「什么时候调用 PerformAttack()」。
    /// </summary>
    public abstract class EnemyAttackModule : MonoBehaviour
    {
        /// <summary>是否正在攻击（含一切占用动作的攻击）。</summary>
        public abstract bool IsAttacking { get; }

        /// <summary>是否正在执行束缚/擒抱类攻击（没有就保持 false）。</summary>
        public virtual bool IsMountAttacking => false;

        /// <summary>击杀后是否冻结（鬣狗击杀演算用，没有就保持 false）。</summary>
        public virtual bool IsPostKillFrozen => false;

        /// <summary>现在能不能起手（冷却、忙碌状态）。</summary>
        public virtual bool CanAttack => !IsAttacking;

        /// <summary>由 EnemyController 注入依赖。</summary>
        public virtual void Init(EnemyMotor motor, EnemyAnimDriver anim) { }

        /// <summary>开始一次普通攻击。</summary>
        public abstract void PerformAttack();

        /// <summary>打断当前攻击（受击、玩家死亡等）。</summary>
        public virtual void CancelAttack() { }

        /// <summary>动画事件转发，例如 "Mount_Hit"。</summary>
        public virtual void OnAnimEvent(string eventName) { }

        /// <summary>
        /// 玩家用闪避无敌帧躲过了本次攻击判定（原版 HitBox FSM 的 "Dodged" 事件）。
        /// 一般用来给攻击落空加额外冷却 / 播空挥反馈。
        /// </summary>
        public virtual void OnPlayerDodged() { }

        /// <summary>本体死亡时回调，用于强制结束协程 / 关闭判定。</summary>
        public virtual void OnOwnerDeath() { }
    }
}
