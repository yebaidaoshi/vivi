using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人总装：把 Brain / Motor / Anim / Attack / Health 五个模块拼起来。
    /// 不包含任何具体敌人数值或行为 —— 换成新敌人时只需要换 Brain / Attack 组件。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Modules")]
        [SerializeField] private EnemyMotor motor;
        [SerializeField] private EnemyAnimDriver anim;
        [SerializeField] private EnemyBrainBase ai;
        [SerializeField] private EnemyAttackModule combat;
        [SerializeField] private EnemyHealth health;

        [Header("调试")]
        [Tooltip("左上角显示状态 / 距离 / 动画 / 眩晕，F1 开关。")]
        [SerializeField] private bool debugOverlay = true;

        public EnemyMotor Motor => motor;
        public EnemyAnimDriver Anim => anim;
        public EnemyBrainBase Brain => ai;
        public EnemyAttackModule AttackModule => combat;
        public EnemyHealth Health => health;

        private void Awake()
        {
            debugOverlay = true;
            EnsureModules();
            WireModules();
        }

        private void EnsureModules()
        {
            motor = motor != null ? motor : GetComponent<EnemyMotor>();
            if (motor == null) motor = gameObject.AddComponent<EnemyMotor>();

            anim = anim != null ? anim : GetComponent<EnemyAnimDriver>();
            if (anim == null) anim = gameObject.AddComponent<EnemyAnimDriver>();

            health = health != null ? health : GetComponent<EnemyHealth>();
            if (health == null) health = gameObject.AddComponent<EnemyHealth>();

            ai = ai != null ? ai : GetComponent<EnemyBrainBase>();
            if (ai == null)
            {
                // 兼容旧预制体：没有显式 Brain 时回退到鬣狗的 EnemyAI。
                ai = GetComponent<EnemyAI>();
                if (ai == null) ai = gameObject.AddComponent<EnemyAI>();
            }

            combat = combat != null ? combat : GetComponent<EnemyAttackModule>();
            if (combat == null) combat = GetComponent<EnemyCombat>();

            // 公共战斗组件：没有就自动补
            if (GetComponent<EnemyHitStop>() == null) gameObject.AddComponent<EnemyHitStop>();
            if (GetComponent<EnemyStunGauge>() == null) gameObject.AddComponent<EnemyStunGauge>();
            if (GetComponent<EnemyCameraLink>() == null) gameObject.AddComponent<EnemyCameraLink>();
            if (GetComponent<EnemyFlinching>() == null) gameObject.AddComponent<EnemyFlinching>();
            if (GetComponent<EnemyAudio>() == null) gameObject.AddComponent<EnemyAudio>();
            if (GetComponent<EnemyStunStars>() == null) gameObject.AddComponent<EnemyStunStars>();
            if (GetComponent<EnemyColorFlasher>() == null) gameObject.AddComponent<EnemyColorFlasher>();
        }

        private void WireModules()
        {
            if (ai != null) ai.Init(motor, anim, combat, health);
            if (combat != null) combat.Init(motor, anim);
            if (health != null) health.Init(motor, anim);

            var gauge = GetComponent<EnemyStunGauge>();
            if (gauge != null) gauge.Init(ai, motor, anim);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) debugOverlay = !debugOverlay;

            if (ai == null || !ai.enabled) return;
            if (health != null && health.IsDead) return;

            // 安全阀：动画不允许莫名停在 speed=0（束缚击杀时的定格会残留）
            if (anim != null)
            {
                bool mountingAnim = combat != null && combat.IsMountAttacking;
                if (!mountingAnim && anim.IsFrozen) anim.ResumeAnimator();
            }

            // 安全阀：既没在束缚、也没死亡时，马达不允许保持冻结/忽略物理
            if (motor != null)
            {
                bool mounting = combat != null && combat.IsMountAttacking;
                if (!mounting)
                {
                    if (motor.IsFrozen) motor.UnfreezePhysics();
                    motor.SetIgnorePhysicsUpdate(false);
                }
            }

            // 眩晕期间 AI 停摆（原版 StunGauge 的 Break 状态）
            var gauge = GetComponent<EnemyStunGauge>();
            if (gauge != null && gauge.IsStunned) return;

            ai.Tick();
        }

        private void FixedUpdate()
        {
            if (motor != null) motor.PhysicsUpdate();
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            if (health != null) health.TakeDamage(damage, knockback, attacker);
        }

        /// <summary>动画事件桥接（Spine / Animator → 模块）。</summary>
        public void SendEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            ai?.OnAnimEvent(eventName);
            combat?.OnAnimEvent(eventName);
        }

        private void OnGUI()
        {
            if (!debugOverlay || ai == null) return;

            var brain = ai as EnemyAI;
            string state = brain != null ? brain.DebugState : ai.GetType().Name;
            string clip = brain != null ? brain.DebugAnimInfo : "-";

            Transform target = ai.Target;
            float dist = target != null ? Mathf.Abs(target.position.x - transform.position.x) : -1f;

            var gauge = GetComponent<EnemyStunGauge>();
            bool stunned = gauge != null && gauge.IsStunned;

            GUI.Label(new Rect(10f, 10f, 1200f, 22f),
                "[Hyena] " + state
                + "  dist=" + (dist >= 0f ? dist.ToString("F1") : "-")
                + "  anim=" + clip
                + (stunned ? "  STUN" : "")
                + ((health != null && health.IsDead) ? "  DEAD" : ""));
        }
    }
}
