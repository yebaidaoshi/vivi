using UnityEngine;

namespace Enemy
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Modules")]
        [SerializeField] private EnemyMotor motor;
        [SerializeField] private EnemyAnimDriver anim;
        [SerializeField] private EnemyAI ai;
        [SerializeField] private EnemyCombat combat;
        [SerializeField] private EnemyHealth health;

        private void Awake()
        {
            EnsureModules();
            WireModules();
        }

        private void EnsureModules()
        {
            motor = motor ?? GetComponent<EnemyMotor>() ?? gameObject.AddComponent<EnemyMotor>();
            anim = anim ?? GetComponent<EnemyAnimDriver>() ?? gameObject.AddComponent<EnemyAnimDriver>();
            ai = ai ?? GetComponent<EnemyAI>() ?? gameObject.AddComponent<EnemyAI>();
            combat = combat ?? GetComponent<EnemyCombat>() ?? gameObject.AddComponent<EnemyCombat>();
            health = health ?? GetComponent<EnemyHealth>() ?? gameObject.AddComponent<EnemyHealth>();
        }

        private void WireModules()
        {
            ai.Init(motor, anim, combat, health);
            combat.Init(motor, anim);
            health.Init(motor, anim);
        }

        private void Update()
        {
            ai.Tick();
        }

        private void FixedUpdate()
        {
            motor.PhysicsUpdate();
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            health.TakeDamage(damage, knockback, attacker);
        }
    }
}