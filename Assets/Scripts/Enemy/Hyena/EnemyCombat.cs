using UnityEngine;
using Player;
using System.Collections;

namespace Enemy
{
    public class EnemyCombat : MonoBehaviour
    {
        [Header("攻击参数")]
        [SerializeField] private int damage = 20;
        [SerializeField] private float attackCooldown = 0.8f;
        [SerializeField] private Vector2 knockbackForce = new Vector2(5f, 3f);

        [Header("攻击碰撞体")]
        [SerializeField] private Collider2D attackCollider;

        private float cooldownTimer;
        private bool isAttacking;
        private Coroutine attackCoroutine;

        private const float ATTACK_START_DELAY = 39f / 60f;   // 0.65s
        private const float ATTACK_DURATION = (42f - 39f) / 60f; // 0.05s
        private const float TOTAL_ATTACK_DURATION = 93f / 60f; // 1.55s

        public bool CanAttack => cooldownTimer <= 0f && !isAttacking;
        public bool IsAttacking => isAttacking;
        public Collider2D AttackCollider => attackCollider;

        public void Init(EnemyMotor motor, EnemyAnimDriver anim)
        {
            if (attackCollider != null)
                attackCollider.enabled = false;
            else
                Debug.LogError("EnemyCombat: 未设置 attackCollider！");
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;
        }

        public void PerformAttack()
        {
            if (!CanAttack) return;
            cooldownTimer = attackCooldown;
            isAttacking = true;

            if (attackCoroutine != null)
                StopCoroutine(attackCoroutine);
            attackCoroutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            yield return new WaitForSeconds(ATTACK_START_DELAY);
            if (attackCollider != null)
                attackCollider.enabled = true;

            yield return new WaitForSeconds(ATTACK_DURATION);
            if (attackCollider != null)
                attackCollider.enabled = false;

            float remaining = TOTAL_ATTACK_DURATION - ATTACK_START_DELAY - ATTACK_DURATION;
            if (remaining > 0)
                yield return new WaitForSeconds(remaining);

            isAttacking = false;
            attackCoroutine = null;
        }

        public void OnClawStart() { }
        public void OnClawEnd() { }
        public void OnAttackEnd() { }
        public void SendEvent(string eventName) { }

        public Collider2D GetAttackCollider() => attackCollider;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAttacking) return;
            if (attackCollider == null || !attackCollider.enabled) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) return;
            if (damageable.IsDead) return;

            Vector2 knockback = new Vector2(
                knockbackForce.x * (other.transform.position.x > transform.position.x ? 1 : -1),
                knockbackForce.y
            );
            damageable.TakeDamage(damage, knockback, gameObject);
        }
    }
}