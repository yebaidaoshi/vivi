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

        [Header("打击特效（拖 BulletHit / BulletHitCritical）")]
        [SerializeField] private GameObject hitVfx;
        [SerializeField] private GameObject criticalVfx;
        [SerializeField] private float vfxLifetime = 1f;

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
        private Vector2 _lastHitPoint;
        private float _lastHitPointTime = -10f;
        private static bool _warmedUp;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public float NormalizedHealth => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>血条用的平滑值（0~1），UI 每帧读这个做插值。</summary>
        public float DisplayNormalized => displayNormalized;

        /// <summary>由子弹/近战在碰撞点调用，让打击特效出现在真正命中的位置。</summary>
        public void SetHitPoint(Vector2 worldPoint)
        {
            _lastHitPoint = worldPoint;
            _lastHitPointTime = Time.time;
        }

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

            // 启动预热：字体 + 打击特效，避免第一次射击时一次性卡住
            if (!_warmedUp)
            {
                _warmedUp = true;
                UI.DamageNumber.Warmup();
                VfxPool.Warmup(hitVfx, 2);
                VfxPool.Warmup(criticalVfx, 1);
            }
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

                // 逃跑动画循环播放，避免播完一遍后变成纯位移
                if (anim != null && !string.IsNullOrEmpty(fleeAnim) && !anim.IsPlaying(fleeAnim))
                    anim.PlayState(fleeAnim);
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

            var flasher = GetComponent<EnemyColorFlasher>();
            if (flasher != null) { if (critical) flasher.FlashCritical(); else flasher.FlashHurt(); }

            // ---- 打击感：震屏 + 音效（顿帧已全部移除，避免"卡顿"）----
            var camera = SceneCamera.CameraDirector.Instance;
            if (camera != null) camera.Shake(critical ? 0.16f : 0.09f, critical ? critShakeStrength : shakeStrength);

            var sfx = GetComponent<EnemyAudio>();
            if (sfx != null)
            {
                if (critical) sfx.PlayHurtCritical();
                else sfx.PlayHurt();
            }

            // 打击特效
            var vfx = critical ? criticalVfx : hitVfx;
            if (vfx != null)
            {
                Vector3 spawnPos = Time.time - _lastHitPointTime < 0.15f
                    ? (Vector3)_lastHitPoint
                    : transform.position + Vector3.up * 1.2f;

                VfxPool.Spawn(vfx, spawnPos, vfxLifetime);
            }

            // 血量跌破分段阈值：追加一次重反馈
            if (crossedStage)
            {
                // 只做震屏，不做顿帧：分段跌血的慢动作会让人以为卡顿
                if (camera != null) camera.Shake(0.2f, 0.45f);            }

            // ---- 伤害数字（白色）----
            Vector3 popupPos = Time.time - _lastHitPointTime < 0.15f
                ? (Vector3)_lastHitPoint + Vector3.up * 0.4f
                : transform.position + Vector3.up * 1.6f;
            UI.DamageNumber.ShowEnemy(popupPos, damage);

            // ---- 眩晕累积：这一下会死就不加眩晕 ----
            var gauge = GetComponent<EnemyStunGauge>();
            if (gauge != null && currentHealth > 0) gauge.AddHitStun(damage);

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

            // 死亡时取消眩晕（避免"死了还在眩晕循环"）
            var stunGauge = GetComponent<EnemyStunGauge>();
            if (stunGauge != null) stunGauge.Cancel();

            combat?.OnOwnerDeath();

            if (anim != null)
            {
                anim.ResumeAnimator();
                anim.PlayState(hitBlow1Anim, 0f);
            }

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
