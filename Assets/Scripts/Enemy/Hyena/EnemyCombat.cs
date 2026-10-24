using UnityEngine;
using Player;
using System.Collections;
using System.Collections.Generic;

namespace Enemy
{
    public class EnemyCombat : MonoBehaviour, IBindingSource
    {
        public static bool GlobalTimeFrozen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => GlobalTimeFrozen = false;

        [Header("普通攻击")]
        [SerializeField] private int damage = 20;
        [SerializeField] private float attackCooldown = 0.8f;
        [SerializeField] private Vector2 knockbackForce = new Vector2(5f, 3f);
        [SerializeField] private Collider2D attackCollider;

        [Header("束缚攻击 - 配置")]
        [SerializeField] private BindingProfile bindingProfile;
        [SerializeField] private float mountCooldown = 20f;
        [SerializeField] private bool useCooldownOnStart = false;
        [SerializeField] private int mountDamagePerTick = 10;
        [SerializeField] private Vector2 mountKnockback = new Vector2(0f, 2f);
        [SerializeField] private Collider2D mountDetectCollider;
        [SerializeField] private LayerMask mountTargetMask;

        [Header("束缚攻击 - 动画时长（秒）")]
        [SerializeField] private float standbyDuration     = 0.533f;
        [SerializeField] private float jumpAttack0Duration = 0.2f;
        [SerializeField] private float jumpAttackDuration  = 65f / 60f;
        [SerializeField] private float mountedLoopDuration = 1.267f;
        [SerializeField] private float mountKillDuration   = 2.4f;
        [SerializeField] private float mountBreakDuration  = 2.167f;

        [Header("束缚攻击 - 判定窗口（帧 → 秒）")]
        [SerializeField] private float mountHitWindowStart = 0f;
        [SerializeField] private float mountHitWindowEnd   = 65f / 60f;

        [Header("束缚攻击 - 飞扑运动")]
        [SerializeField] private float mountLeapSpeedX = 5f;
        [SerializeField] private float mountLeapSpeedY = 3.5f;

        [Header("束缚攻击 - 飞扑带人")]
        [Tooltip("命中后玩家相对敌人的位置偏移（Y 为负代表敌人在玩家上方）。")]
        [SerializeField] private Vector2 mountLeapPlayerOffset = new Vector2(0f, -1.5f);

        [Header("束缚攻击 - 吸附玩家")]
        [SerializeField] private Vector2 mountPositionOffset = new Vector2(0f, 0.8f);
        [SerializeField] private float snapDuration = 0.2f;

        [Header("束缚攻击 - 挣脱击退")]
        [SerializeField] private float escapeKnockbackX = 12f;
        [SerializeField] private float escapeKnockbackY = 6f;

        [Header("束缚攻击 - 击杀等待")]
        [SerializeField] private float maxExtraDeathWait = 3.0f;

        [Header("束缚攻击 - 动画名")]
        [SerializeField] private string standbyAnim     = "StandBy";
        [SerializeField] private string jumpAttack0Anim = "Jump_Attack0";
        [SerializeField] private string jumpAttackAnim  = "Jump_Attack1";
        [SerializeField] private string mountedAnim     = "Mounted";
        [SerializeField] private string mountedLoopAnim = "Mounted_Loop";
        [SerializeField] private string mountKillAnim   = "Mount_Kill";
        [SerializeField] private string mountBreakAnim  = "Hit_Blow1";
        [SerializeField] private string movementAnim    = "Movement";

        [Header("视觉提示")]
        [SerializeField] private EnemyHighlight highlight;

        private const float ATTACK_START_DELAY = 39f / 60f;
        private const float ATTACK_DURATION = (42f - 39f) / 60f;
        private const float TOTAL_ATTACK_DURATION = 93f / 60f;

        private float cooldownTimer;
        private bool isAttacking;
        private Coroutine attackCoroutine;
        private readonly HashSet<Collider2D> hitThisSwing = new();

        private float mountCooldownTimer;
        private bool isMountAttacking;
        private bool isBindingActive;
        private Coroutine mountCoroutine;
        private IDamageable currentMountTarget;
        private Transform currentMountTargetTf;
        private Transform currentMountRootTf;
        private bool mountHitConfirmed;
        private bool mountBrokenFree;
        private int lastLeapDir = 1;
        private bool isPostKillFrozen;

        private EnemyMotor motor;
        private EnemyAnimDriver anim;

        public bool IsBinding => isBindingActive;
        public BindingProfile Profile => bindingProfile;
        public bool IsPostKillFrozen => isPostKillFrozen;

        public void OnPlayerEscaped()
        {
            if (!isMountAttacking) return;
            mountBrokenFree = true;
            isBindingActive = false;
        }

        public void OnPlayerDied() => isBindingActive = false;

        public bool CanAttack => cooldownTimer <= 0f && !isAttacking && !isMountAttacking;
        public bool IsAttacking => isAttacking || isMountAttacking;

        public bool IsAttackInWindup
        {
            get
            {
                if (isMountAttacking) return mountDetectCollider == null || !mountDetectCollider.enabled;
                if (isAttacking) return attackCollider == null || !attackCollider.enabled;
                return false;
            }
        }

        public bool CanMountAttack
        {
            get
            {
                if (mountCooldownTimer > 0f) return false;
                if (isMountAttacking || isAttacking) return false;

                var pb = PlayerBinding.Instance;
                if (pb != null && pb.IsActive) return false;

                return true;
            }
        }

        public bool IsMountAttacking => isMountAttacking;
        public Collider2D AttackCollider => attackCollider;
        public float MountCooldownRemaining => Mathf.Max(0f, mountCooldownTimer);

        public void Init(EnemyMotor motor, EnemyAnimDriver anim)
        {
            this.motor = motor;
            this.anim = anim;
            if (attackCollider != null) attackCollider.enabled = false;
            if (mountDetectCollider != null) mountDetectCollider.enabled = false;

            if (highlight == null) highlight = GetComponent<EnemyHighlight>();

            mountCooldownTimer = useCooldownOnStart ? mountCooldown : 0f;
            isPostKillFrozen = false;
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (mountCooldownTimer > 0f) mountCooldownTimer -= Time.deltaTime;
        }

        private static bool IsPlayerInAirOrInvincible()
        {
            var pb = PlayerBinding.Instance;
            if (pb == null) return false;

            var pm = pb.GetComponent<PlayerMotor>();
            if (pm != null && !pm.IsGrounded) return true;

            var ph = pb.GetComponent<PlayerHealth>();
            if (ph != null && ph.IsEvadeInvincible) return true;

            return false;
        }

        public void PerformAttack()
        {
            if (!CanAttack) return;
            cooldownTimer = attackCooldown;
            isAttacking = true;
            if (attackCoroutine != null) StopCoroutine(attackCoroutine);
            attackCoroutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            hitThisSwing.Clear();
            yield return new WaitForSeconds(ATTACK_START_DELAY);
            if (attackCollider != null) attackCollider.enabled = true;
            yield return new WaitForSeconds(ATTACK_DURATION);
            if (attackCollider != null) attackCollider.enabled = false;
            float remaining = TOTAL_ATTACK_DURATION - ATTACK_START_DELAY - ATTACK_DURATION;
            if (remaining > 0) yield return new WaitForSeconds(remaining);
            isAttacking = false;
            attackCoroutine = null;
        }

        public void CancelAttack()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
            if (attackCollider != null) attackCollider.enabled = false;
            isAttacking = false;
            hitThisSwing.Clear();
        }

        public void PerformMountAttack()
        {
            if (!CanMountAttack) return;

            mountCooldownTimer = mountCooldown;
            isMountAttacking = true;
            isBindingActive = true;
            isPostKillFrozen = false;

            if (mountCoroutine != null) StopCoroutine(mountCoroutine);
            mountCoroutine = StartCoroutine(MountRoutine());
        }

        private IEnumerator MountRoutine()
        {
            mountHitConfirmed = false;
            mountBrokenFree = false;
            currentMountTarget = null;
            currentMountTargetTf = null;
            currentMountRootTf = null;

            int leapDir = 1;
            var tgtAtStart = FindPlayerTransform();
            if (tgtAtStart != null)
                leapDir = tgtAtStart.position.x >= transform.position.x ? 1 : -1;
            lastLeapDir = leapDir;
            motor.SetFacing(leapDir);

            highlight?.ShowWarning();
            motor.Stop();
            motor.FreezePhysics();
            anim.PlayState(standbyAnim);

            yield return new WaitForSeconds(standbyDuration);

            highlight?.HideWarning();
            motor.UnfreezePhysics();
            motor.SetIgnorePhysicsUpdate(true);
            motor.SetVelocity(new Vector2(0f, mountLeapSpeedY));
            anim.PlayState(jumpAttack0Anim);

            yield return new WaitForSeconds(jumpAttack0Duration);

            // ---------- 阶段3: Jump_Attack1（飞扑 + 判定 + 带人） ----------
            anim.PlayState(jumpAttackAnim);
            motor.SetInterpolation(RigidbodyInterpolation2D.None);

            float clipDuration = jumpAttackDuration;
            float windowStart  = Mathf.Clamp(mountHitWindowStart, 0f, clipDuration);
            float windowEnd    = Mathf.Clamp(mountHitWindowEnd, windowStart, clipDuration);

            float atkElapsed = 0f;
            bool prevWindowOpen = false;

            while (atkElapsed < clipDuration)
            {
                float dt = Time.deltaTime;
                atkElapsed += dt;

                var p = transform.position;
                p.x += leapDir * mountLeapSpeedX * dt;
                if (motor.Rb != null) p.y = motor.Rb.position.y;
                transform.position = p;
                motor.SetRigidbodyPositionX(p.x);

                // ★ 命中后强制带着玩家一起飞（加入 Y 轴偏移）
                if (mountHitConfirmed && currentMountRootTf != null)
                {
                    Vector3 targetPlayerPos = transform.position + (Vector3)mountLeapPlayerOffset;
                    currentMountRootTf.position = targetPlayerPos;

                    var pbRb = currentMountRootTf.GetComponent<Rigidbody2D>();
                    if (pbRb != null)
                    {
                        // 强制 Kinematic，防止物理引擎和玩家自己的 FixedUpdate 覆盖位置
                        if (pbRb.bodyType != RigidbodyType2D.Kinematic)
                            pbRb.bodyType = RigidbodyType2D.Kinematic;

                        pbRb.velocity = Vector2.zero;
                        pbRb.position = targetPlayerPos;
                    }
                }

                bool windowOpen = atkElapsed >= windowStart && atkElapsed <= windowEnd;
                if (windowOpen != prevWindowOpen)
                {
                    if (mountDetectCollider != null) mountDetectCollider.enabled = windowOpen;
                    highlight?.SetPinkOutline(windowOpen);
                }
                prevWindowOpen = windowOpen;

                if (windowOpen && !mountHitConfirmed)
                {
                    PollMountHit();
                    if (mountHitConfirmed)
                    {
                        // 命中瞬间扣血，使玩家立刻进入束缚状态（播放被扑倒的挣扎动画）
                        DealMountDamage();
                    }
                }

                yield return null;
            }

            if (mountDetectCollider != null) mountDetectCollider.enabled = false;
            highlight?.SetPinkOutline(false);

            if (!mountHitConfirmed || currentMountTarget == null)
            {
                motor.SetInterpolation(RigidbodyInterpolation2D.Interpolate);
                EndMountAttack(freezeAnim: false);
                yield break;
            }

            // ---------- 阶段4: Mounted（到达落点后播放扑倒） ----------
            motor.SetInterpolation(RigidbodyInterpolation2D.Interpolate);
            motor.SetIgnorePhysicsUpdate(true);
            motor.Stop();

            anim.PlayState(mountedAnim);

            yield return SnapToPlayerSmoothly();
            motor.FreezePhysics();

            float pinnedDuration = bindingProfile != null ? bindingProfile.pinnedDuration : 1.1f;
            yield return FollowAndWait(pinnedDuration);

            while (!mountBrokenFree && currentMountTarget != null && !currentMountTarget.IsDead)
            {
                anim.PlayState(mountedLoopAnim);
                yield return FollowAndWait(mountedLoopDuration);

                if (mountBrokenFree || currentMountTarget == null || currentMountTarget.IsDead)
                    break;

                DealMountDamage();
            }

            bool targetDied = currentMountTarget != null && currentMountTarget.IsDead;

            if (targetDied)
            {
                anim.PlayState(mountKillAnim);
                yield return new WaitForSeconds(mountKillDuration);

                float waitExtra = 0f;
                while (PlayerBinding.Instance != null && PlayerBinding.Instance.IsActive && waitExtra < maxExtraDeathWait)
                {
                    waitExtra += Time.deltaTime;
                    yield return null;
                }

                EndMountAttack(freezeAnim: true);
                yield break;
            }
            else
            {
                anim.PlayState(mountBreakAnim);

                motor.SetIgnorePhysicsUpdate(false);
                if (motor.IsFrozen) motor.UnfreezePhysics();

                int knockDir = -lastLeapDir;
                Vector2 kb = new Vector2(knockDir * escapeKnockbackX, escapeKnockbackY);
                motor.ApplyKnockback(kb);

                yield return new WaitForSeconds(mountBreakDuration);

                EndMountAttack(freezeAnim: false);
            }
        }

        private Transform FindPlayerTransform()
        {
            var go = GameObject.FindGameObjectWithTag("PlayerPresence");
            return go != null ? go.transform : null;
        }

        private Vector2 GetAttachTarget()
        {
            Transform root = currentMountRootTf;
            if (root == null) root = currentMountTargetTf;
            if (root == null) return transform.position;

            return new Vector2(root.position.x + mountPositionOffset.x, root.position.y + mountPositionOffset.y);
        }

        private IEnumerator SnapToPlayerSmoothly()
        {
            Vector2 startPos = transform.position;
            float elapsed = 0f;

            if (snapDuration <= 0f)
            {
                Vector2 target = GetAttachTarget();
                transform.position = target;
                if (motor.Rb != null) motor.Rb.position = target;
                yield break;
            }

            while (elapsed < snapDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / snapDuration);
                float eased = t * t * (3f - 2f * t);

                Vector2 targetNow = GetAttachTarget();
                Vector2 cur = Vector2.Lerp(startPos, targetNow, eased);
                transform.position = cur;
                if (motor.Rb != null) motor.Rb.position = cur;

                yield return null;
            }

            Vector2 final = GetAttachTarget();
            transform.position = final;
            if (motor.Rb != null) motor.Rb.position = final;
        }

        private IEnumerator FollowAndWait(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (mountBrokenFree) yield break;
                if (currentMountTarget == null || currentMountTarget.IsDead) yield break;

                Vector2 target = GetAttachTarget();
                transform.position = target;
                if (motor.Rb != null) motor.Rb.position = target;

                // 同时强制让玩家的 Rigidbody 位置跟着
                if (currentMountRootTf != null)
                {
                    var pbRb = currentMountRootTf.GetComponent<Rigidbody2D>();
                    if (pbRb != null)
                    {
                        pbRb.velocity = Vector2.zero;
                        pbRb.position = target;
                    }
                }

                yield return null;
            }
        }

        private void DealMountDamage()
        {
            if (currentMountTarget == null || currentMountTarget.IsDead) return;
            currentMountTarget.TakeDamage(mountDamagePerTick, mountKnockback, gameObject);
        }

        private void EndMountAttack(bool freezeAnim = false)
        {
            if (mountDetectCollider != null) mountDetectCollider.enabled = false;
            motor.SetIgnorePhysicsUpdate(false);
            motor.SetInterpolation(RigidbodyInterpolation2D.Interpolate);
            if (motor.IsFrozen) motor.UnfreezePhysics();

            highlight?.HideWarning();
            highlight?.SetPinkOutline(false);

            isMountAttacking = false;
            isBindingActive = false;
            mountCoroutine = null;
            currentMountTarget = null;
            currentMountTargetTf = null;
            currentMountRootTf = null;
            mountHitConfirmed = false;
            mountBrokenFree = false;

            if (freezeAnim)
            {
                isPostKillFrozen = true;
                GlobalTimeFrozen = true;
                anim.FreezeAnimator();
            }
            else
            {
                isPostKillFrozen = false;
                anim.PlayState(movementAnim);
            }
        }

        public void ForceEndMountAttack()
        {
            if (mountCoroutine != null) { StopCoroutine(mountCoroutine); mountCoroutine = null; }
            if (mountDetectCollider != null) mountDetectCollider.enabled = false;
            motor?.SetInterpolation(RigidbodyInterpolation2D.Interpolate);

            highlight?.HideWarning();
            highlight?.SetPinkOutline(false);

            isMountAttacking = false;
            isBindingActive = false;
            mountBrokenFree = false;
            mountHitConfirmed = false;
            currentMountTarget = null;
            currentMountTargetTf = null;
            currentMountRootTf = null;
        }

        private void PollMountHit()
        {
            if (mountDetectCollider == null) return;
            if (IsPlayerInAirOrInvincible()) return;

            var results = new Collider2D[4];
            var filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
            filter.layerMask = mountTargetMask;

            int count = mountDetectCollider.OverlapCollider(filter, results);

            for (int i = 0; i < count; i++)
            {
                if (results[i] == null) continue;
                if (results[i].transform == transform || results[i].transform.IsChildOf(transform)) continue;

                var d = results[i].GetComponentInParent<IDamageable>();
                if (d != null && !d.IsDead)
                {
                    currentMountTarget = d;
                    currentMountTargetTf = results[i].transform;

                    var pb = PlayerBinding.Instance;
                    if (pb != null) currentMountRootTf = pb.transform;
                    else currentMountRootTf = results[i].transform.root;

                    mountHitConfirmed = true;
                    return;
                }
            }
        }

        public void OnMountHit()
        {
            if (!isMountAttacking || mountHitConfirmed) return;
            PollMountHit();
        }

        public void OnClawStart() { }
        public void OnClawEnd() { }
        public void OnAttackEnd() { }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAttacking || isMountAttacking) return;
            if (attackCollider == null || !attackCollider.enabled) return;
            if (other.gameObject == gameObject) return;
            if (hitThisSwing.Contains(other)) return;
            if (IsPlayerInAirOrInvincible()) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            hitThisSwing.Add(other);
            Vector2 knockback = new Vector2(
                knockbackForce.x * (other.transform.position.x > transform.position.x ? 1 : -1),
                knockbackForce.y);
            damageable.TakeDamage(damage, knockback, gameObject);
        }

        public Collider2D GetAttackCollider() => attackCollider;
    }
}