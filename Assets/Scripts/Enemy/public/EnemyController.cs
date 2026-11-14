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
        [Tooltip("屏幕左上角显示 AI 状态 / 目标 / 距离，排查用。")]
        [SerializeField] private bool debugOverlay = false;

        private void OnGUI()
        {
            if (!debugOverlay || ai == null) return;

            string state = ai != null && ai is EnemyAI hyena ? hyena.CurrentState : ai.GetType().Name;
            Transform t = ai.Target;
            float dist = t != null ? Mathf.Abs(t.position.x - transform.position.x) : -1f;

            string anim = (ai is EnemyAI hy2) ? hy2.DebugAnimInfo : "-";

            GUI.Label(new Rect(12f, 12f, 1400f, 22f),
                "[Hyena] state=" + state
                + "  target=" + (t != null ? t.name : "null")
                + "  dist=" + (dist >= 0f ? dist.ToString("F1") : "-")
                + "  anim=" + anim
                + "  dead=" + (health != null && health.IsDead));

            // 第二行：所有 Animator / SkeletonMecanim 的真实状态
            var animators = GetComponentsInChildren<Animator>(true);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < animators.Length; i++)
            {
                var an = animators[i];
                if (an == null) continue;
                string ctl = an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "null";
                string cur = an.layerCount > 0
                    ? (an.GetCurrentAnimatorStateInfo(0).IsName("Movement") ? "Movement"
                        : an.GetCurrentAnimatorStateInfo(0).IsName("Idle") ? "Idle"
                        : an.GetCurrentAnimatorStateInfo(0).IsName("Walk") ? "Walk"
                        : "other")
                    : "-";
                sb.Append(an.name).Append("(en=").Append(an.enabled).Append(",layers=").Append(an.layerCount)
                  .Append(",ctl=").Append(ctl).Append(",s0=").Append(cur).Append(") ");
            }

            var skeletons = GetComponentsInChildren<Spine.Unity.SkeletonMecanim>(true);
            for (int i = 0; i < skeletons.Length; i++)
            {
                var sk = skeletons[i];
                if (sk != null) sb.Append("SKM:").Append(sk.name).Append("(en=").Append(sk.enabled).Append(") ");
            }

            GUI.Label(new Rect(12f, 34f, 1400f, 22f), sb.ToString());
        }
        public EnemyMotor Motor => motor;
        public EnemyAnimDriver Anim => anim;
        public EnemyBrainBase Brain => ai;
        public EnemyAttackModule AttackModule => combat;
        public EnemyHealth Health => health;

        private void Awake()
        {
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
                Debug.LogWarning("[EnemyController] 没有找到 Brain，已回退到 EnemyAI（鬣狗）。新敌人请显式挂自己的 Brain。", this);
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
            if (ai == null || !ai.enabled) return;
            if (health != null && health.IsDead) return;

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
    }
}
