using UnityEngine;
using Player;
using System.Collections;
using Spine.Unity;

namespace Enemy
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("生命值")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float destroyDelay = 4.7f;

        [Header("击飞参数")]
        [SerializeField] private float knockbackForce = 15f;
        [SerializeField] private float knockbackDuration = 2.167f;

        [Header("逃走参数")]
        [SerializeField] private float fleeSpeed = 5f;

        [Header("动画状态名")]
        [SerializeField] private string hitBlow1Anim = "Hit_Blow1";
        [SerializeField] private string fleeAnim = "Flee";

        private int currentHealth;
        private bool isDead;
        private EnemyMotor motor;
        private EnemyAnimDriver anim;
        private EnemyAI ai;
        private int facing = 1;
        private int fleeDirection = 1;
        private SkeletonMecanim skeletonMecanim;
        private Rigidbody2D rb;
        private bool isFleeing = false;
        private GameObject lastAttacker;

        public bool IsDead => isDead;

        public void Init(EnemyMotor motor, EnemyAnimDriver anim)
        {
            this.motor = motor;
            this.anim = anim;
            currentHealth = maxHealth;
            isDead = false;
            ai = GetComponent<EnemyAI>();
            skeletonMecanim = GetComponent<SkeletonMecanim>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (!isDead && motor != null && motor.Velocity.x != 0)
                facing = motor.Velocity.x > 0 ? 1 : -1;

            if (isFleeing && motor != null)
            {
                if (motor.IsFrozen)
                    motor.UnfreezePhysics();

                motor.MoveTo(new Vector2(fleeDirection, 0), fleeSpeed);
                if (fleeDirection > 0)
                    transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
                else
                    transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
            }
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            if (isDead) return;

            lastAttacker = attacker;

            currentHealth -= damage;
            StartCoroutine(FlashColor(Color.yellow, 0.15f));

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }

        private IEnumerator FlashColor(Color color, float duration)
        {
            if (skeletonMecanim == null) yield break;
            var skeleton = skeletonMecanim.skeleton;
            if (skeleton == null) yield break;

            float origR = skeleton.R, origG = skeleton.G, origB = skeleton.B, origA = skeleton.A;
            skeleton.R = color.r; skeleton.G = color.g; skeleton.B = color.b; skeleton.A = color.a;
            yield return new WaitForSeconds(duration);
            skeleton.R = origR; skeleton.G = origG; skeleton.B = origB; skeleton.A = origA;
        }

        private void Die()
        {
            isDead = true;

            if (motor != null)
            {
                motor.Stop();
                if (motor.IsFrozen)
                    motor.UnfreezePhysics();
                // ★ 关键：让 Motor 在击飞期间不要覆盖速度
                motor.SetIgnorePhysicsUpdate(true);
            }
            if (ai != null) ai.enabled = false;

            anim.PlayState(hitBlow1Anim);

            // 计算击飞方向：从攻击者指向敌人（即敌人被击飞远离攻击者）
            Vector2 knockDir = Vector2.right;
            if (lastAttacker != null)
            {
                Vector2 dir = (transform.position - lastAttacker.transform.position).normalized;
                if (Mathf.Abs(dir.x) < 0.01f)
                    knockDir = Vector2.right;
                else
                    knockDir = dir.x > 0 ? Vector2.right : Vector2.left;
            }
            else
            {
                // 降级：使用敌人朝向的反方向
                knockDir = facing > 0 ? Vector2.left : Vector2.right;
            }

            Vector2 force = knockDir * knockbackForce;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.AddForce(force, ForceMode2D.Impulse);
            }

            int hitDir = (int)knockDir.x;
            StartCoroutine(PlayFleeAfterHitBlow(hitDir));
        }

        private IEnumerator PlayFleeAfterHitBlow(int hitDir)
        {
            yield return new WaitForSeconds(knockbackDuration);

            if (rb != null)
                rb.velocity = Vector2.zero;

            // ★ 恢复 Motor 的物理更新控制
            if (motor != null)
                motor.SetIgnorePhysicsUpdate(false);

            fleeDirection = hitDir;
            if (fleeDirection > 0)
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
            else
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);

            anim.PlayState(fleeAnim);
            isFleeing = true;

            yield return new WaitForSeconds(destroyDelay - knockbackDuration);
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }
    }
}