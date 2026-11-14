using System;
using System.Collections;
using Player;
using Spine.Unity;
using UnityEngine;

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

        [Header("分段受伤（血量跌破这些比例时给重反馈）")]
        [SerializeField] private float[] stageThresholds = { 0.66f, 0.33f };

        [Header("受击反馈")]
        [Tooltip("普通命中震屏强度")]
        [SerializeField] private float shakeStrength = 0.18f;
        [Tooltip("暴击命中震屏强度")]
        [SerializeField] private float critShakeStrength = 0.35f;
        [Tooltip("血条插值速度")]
        [SerializeField] private float barLerpSpeed = 6f;

        private int currentHealth;
        private bool isDead;
        private EnemyMotor motor;
        private EnemyAnimDriver anim;
        private EnemyBrainBase ai;
        private EnemyAttackModule combat;
        private EnemyFlinching flinching;
        private int fleeDirection = 1;
        private SkeletonMecanim skeletonMecanim;
        private Rigidbody2D rb;
        private bool isFleeing = false;
        private GameObject lastAttacker;

        private float displayNormalized = 1f;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public float NormalizedHealth => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>血条用的平滑值（0~1），UI 每帧读这个做插值。</summary>
        public float DisplayNormalized => displayNormalized;

        /// <summary>(current, max) 血量变化时触发，用于刷新血条。</summary>
        public event Action<int, int> HealthChanged;

        public void Init(EnemyMotor motor, EnemyAnimDriver anim)
        {
            this.motor = motor;
            this.anim = anim;
            currentHealth = maxHealth;
            isDead = false;
            displayNormalized = 1f;
            ai = GetComponent<EnemyBrainBase>();
            combat = GetComponent<EnemyAttackModule>();
            flinching = GetComponent<EnemyFlinching>();
            skeletonMecanim = GetComponent<SkeletonMecanim>();
            rb = GetComponent<Rigidbody2D>();

            if (flinching != null) flinching.Init(motor, anim, ai);
        }

        private void Update()
        {
            // 血条插值
            float target = NormalizedHealth;
            if (Mathf.Abs(displayNormalized - target) > 0.0005f)
                displayNormalized = Mathf.MoveTowards(displayNormalized, target, barLerpSpeed * Time.deltaTime);

            if (isFleeing && motor != null)
            {
                if (motor.IsFrozen) motor.UnfreezePhysics();
                motor.MoveTo(new Vector2(fleeDirection, 0), fleeSpeed);
            }
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            TakeDamage(damage, knockback, attacker, false);
        }

        /// <summary>critical = 命中弱点（原版 HitBox_WeakPoint）。</summary>
        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker, bool critical)
        {
            if (isDead) return;

            lastAttacker = attacker;

            float prevRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            currentHealth -= damage;
            float newRatio = maxHealth > 0 ? (float)Mathf.Max(0, currentHealth) / maxHealth : 0f;

            bool crossedStage = false;
            if (stageThresholds != null)
            {
                for (int i = 0; i < stageThresholds.Length; i++)
                {
                    float th = stageThresholds[i];
                    if (prevRatio > th && newRatio <= th) { crossedStage = true; break; }
                }
            }

            HealthChanged?.Invoke(Mathf.Max(0, currentHealth), maxHealth);

            StartCoroutine(FlashColor(critical ? Color.red : Color.yellow, critical ? 0.25f : 0.15f));

            // ---- 打击感：顿帧 + 震屏 + 音效 ----
            var hitStop = GetComponent<EnemyHitStop>();
            if (hitStop != null) hitStop.Play(critical ? 0.1f : 0.055f, critical ? 0.02f : 0.05f);

            var camera = SceneCamera.CameraDirector.Instance;
            if (camera != null) camera.Shake(critical ? 0.16f : 0.09f, critical ? critShakeStrength : shakeStrength);

            var sfx = GetComponent<EnemyAudio>();
            if (sfx != null) sfx.PlayHurt();

            // 血量跌破分段阈值：追加一次重反馈
            if (crossedStage)
            {
                if (hitStop != null) hitStop.Play(0.12f, 0.02f);
                if (camera != null) camera.Shake(0.2f, 0.45f);
                Debug.Log("[EnemyHealth] 血量跌破分段阈值 " + newRatio.ToString("F2"), this);
            }

            // ---- 眩晕累积 ----
            var gauge = GetComponent<EnemyStunGauge>();
            if (gauge != null) gauge.AddHitStun(damage);

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                HealthChanged?.Invoke(0, maxHealth);
                Die();
                return;
            }

            // ---- 硬直/护甲判定（原版 Flinching）----
            bool absorbed = flinching != null && flinching.OnHit(damage, attacker, critical);
            if (!absorbed) ai?.NotifyHit();
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
                if (motor.IsFrozen) motor.UnfreezePhysics();
                motor.SetIgnorePhysicsUpdate(true);
            }

            if (ai != null)
            {
                ai.OnDeath();
                ai.enabled = false;
            }

            var sfx = GetComponent<EnemyAudio>();
            if (sfx != null) sfx.PlayDeath();

            var camLink = GetComponent<EnemyCameraLink>();
            if (camLink != null) camLink.Release();

            combat?.OnOwnerDeath();

            anim.PlayState(hitBlow1Anim);

            Vector2 knockDir = Vector2.right;
            if (lastAttacker != null)
            {
                Vector2 dir = (transform.position - lastAttacker.transform.position).normalized;
                knockDir = Mathf.Abs(dir.x) < 0.01f ? Vector2.right : (dir.x > 0 ? Vector2.right : Vector2.left);
            }
            else
            {
                int f = motor != null ? motor.Facing : 1;
                knockDir = f > 0 ? Vector2.left : Vector2.right;
            }

            Vector2 force = knockDir * knockbackForce;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.AddForce(force, ForceMode2D.Impulse);
            }

            StartCoroutine(PlayFleeAfterHitBlow((int)knockDir.x));
        }

        private IEnumerator PlayFleeAfterHitBlow(int hitDir)
        {
            yield return new WaitForSeconds(knockbackDuration);

            if (rb != null) rb.velocity = Vector2.zero;
            if (motor != null) motor.SetIgnorePhysicsUpdate(false);

            fleeDirection = hitDir;
            if (motor != null) motor.SetFacing(fleeDirection);

            anim.PlayState(fleeAnim);
            var sfx = GetComponent<EnemyAudio>();
            if (sfx != null) sfx.PlayFlee();
            isFleeing = true;

            yield return new WaitForSeconds(destroyDelay - knockbackDuration);
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
