using UnityEngine;
using System.Collections;
using Player;

namespace Enemy
{
    public class EnemyAI : MonoBehaviour
    {
        private EnemyMotor motor;
        private EnemyAnimDriver anim;
        private EnemyCombat combat;
        private EnemyHealth health;

        [Header("目标")]
        [SerializeField] private Transform target;

        [Header("移动速度")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 10f;

        [Header("距离阈值")]
        [SerializeField] private float attackRange = 2.5f;
        [SerializeField] private float minMountAttackRange = 3.0f;
        [SerializeField] private float mountAttackRange = 6.5f;
        [SerializeField] private float runThreshold = 9f;

        [Header("飞扑距离可视化（只读）")]
        [SerializeField] private float gizmoLeapSpeedX = 5.0f;
        [SerializeField] private float gizmoLeapDuration = 65f / 60f;

        [Header("视野")]
        [SerializeField] private float viewAngle = 60f;
        [SerializeField] private float viewDistance = 8f;
        [SerializeField] private float backDetectDistance = 2f;
        [SerializeField] private LayerMask obstacleMask;

        [Header("攻击冷却")]
        [SerializeField] private float attackCooldown = 0.8f;

        [Header("动画状态名")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string movementState = "Movement";
        [SerializeField] private string clawAnim = "Claw";

        [Header("攀爬检测")]
        [SerializeField] private float climbCheckDistance = 2.5f;
        [SerializeField] private float climbCheckHeight = 2.5f;
        [SerializeField] private float climbForce = 12f;
        [SerializeField] private float climbHorizontalForce = 3f;
        [SerializeField] private LayerMask climbObstacleMask;
        [SerializeField] private string jumpAnim = "Jump_High";

        private enum State { Idle, Patrol, Chase }
        private State currentState;
        private float timer;
        private float patrolTargetX;
        private bool isChasing = false;
        private float attackCooldownTimer;
        private bool isMoving = false;

        private bool isClimbing = false;
        private float climbCooldownTimer = 0f;
        private Coroutine climbCoroutine;
        private bool jumpEventTriggered = false;

        // ★ 本地冻结标志，避免重复调用 FreezeAnimator
        private bool _frozenByGlobal = false;

        public void Init(EnemyMotor motor, EnemyAnimDriver anim, EnemyCombat combat, EnemyHealth health)
        {
            this.motor = motor;
            this.anim = anim;
            this.combat = combat;
            this.health = health;
        }

        private void Start()
        {
            if (target == null)
                target = GameObject.FindGameObjectWithTag("PlayerPresence")?.transform;

            currentState = State.Idle;
            anim.PlayState(idleAnim);
            timer = Random.Range(3f, 5f);
            isMoving = false;
            _frozenByGlobal = false;
        }

        public void Tick()
        {
            if (target == null || health.IsDead) return;

            // ★★ 全局时间冻结：击杀者播完 Mount_Kill，所有敌人停在当前帧
            if (EnemyCombat.GlobalTimeFrozen)
            {
                if (!_frozenByGlobal)
                {
                    _frozenByGlobal = true;
                    motor.Stop();
                    if (motor.IsFrozen) motor.UnfreezePhysics();
                    anim.FreezeAnimator();   // 冻结在当前帧
                }
                return;
            }

            // 自己正在束缚玩家 → 全权交给 EnemyCombat
            if (combat != null && combat.IsMountAttacking) return;

            // 已击杀玩家并冻结在 Mount_Kill 最后一帧 → AI 不干预
            if (combat != null && combat.IsPostKillFrozen) return;

            // 玩家死亡 / 被其他敌人束缚 → 自己停下播 Idle
            if (IsPlayerDead() || IsPlayerBound())
            {
                ForcePassiveIdle();
                return;
            }

            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
            if (climbCooldownTimer > 0f) climbCooldownTimer -= Time.deltaTime;

            float distX = Mathf.Abs(target.position.x - transform.position.x);
            bool playerDetected = CheckPlayerDetected();

            if (!isChasing && playerDetected)
            {
                isChasing = true;
                currentState = State.Chase;
            }

            anim.SetFloat("Speed", motor.Speed);

            switch (currentState)
            {
                case State.Idle:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        if (isChasing) currentState = State.Chase;
                        else
                        {
                            currentState = State.Patrol;
                            patrolTargetX = transform.position.x + Random.Range(-4f, 4f);
                            timer = Random.Range(2f, 4f);
                            isMoving = true;
                            anim.PlayState(movementState);
                        }
                    }
                    break;

                case State.Patrol:
                    if (isChasing)
                    {
                        currentState = State.Chase;
                        isMoving = true;
                        anim.PlayState(movementState);
                        break;
                    }
                    float dirPatrol = Mathf.Sign(patrolTargetX - transform.position.x);
                    motor.MoveTo(new Vector2(dirPatrol, 0), patrolSpeed);
                    motor.FaceDirection(new Vector2(dirPatrol, 0));
                    timer -= Time.deltaTime;
                    if (timer <= 0f || Mathf.Abs(transform.position.x - patrolTargetX) < 0.5f)
                    {
                        currentState = State.Idle;
                        anim.PlayState(idleAnim);
                        isMoving = false;
                        timer = Random.Range(3f, 5f);
                    }
                    break;

                case State.Chase:
                    if (combat.IsAttacking) break;
                    if (isClimbing) break;

                    if (isChasing && target != null)
                    {
                        bool canClimb = CanClimbUp();
                        if (canClimb && climbCooldownTimer <= 0f)
                        {
                            StartClimb();
                            break;
                        }

                        if (!combat.IsAttacking && motor.IsFrozen)
                            motor.UnfreezePhysics();

                        Chase(distX);
                    }
                    break;
            }
        }

        private bool IsPlayerDead()
        {
            if (target == null) return false;

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsDead) return true;

            var pb = PlayerBinding.Instance;
            if (pb != null && pb.Phase == BindingPhase.Dead) return true;

            return false;
        }

        private bool IsPlayerBound()
        {
            var pb = PlayerBinding.Instance;
            if (pb == null) return false;
            return pb.IsActive;
        }

        private void ForcePassiveIdle()
        {
            if (combat != null && combat.IsAttacking && !combat.IsMountAttacking)
                combat.CancelAttack();

            if (motor.IsFrozen) motor.UnfreezePhysics();
            motor.Stop();

            isMoving = false;
            isChasing = false;
            currentState = State.Idle;
            timer = Random.Range(3f, 5f);

            if (!anim.IsPlaying(idleAnim))
                anim.PlayState(idleAnim);
        }

        private void Chase(float distX)
        {
            if (distX <= attackRange)
            {
                motor.Stop();

                if (!combat.IsAttacking && attackCooldownTimer <= 0f)
                    StartAttack();
                return;
            }

            if (distX <= mountAttackRange && distX >= minMountAttackRange)
            {
                if (combat.CanMountAttack)
                {
                    motor.Stop();
                    float d = Mathf.Sign(target.position.x - transform.position.x);
                    motor.FaceDirection(new Vector2(d, 0));
                    combat.PerformMountAttack();
                    isMoving = false;
                    return;
                }

                MoveToward(distX, walkSpeed);
                return;
            }

            float speed = distX > runThreshold ? runSpeed : walkSpeed;
            MoveToward(distX, speed);
        }

        private void MoveToward(float distX, float speed)
        {
            float dir = Mathf.Sign(target.position.x - transform.position.x);
            motor.FaceDirection(new Vector2(dir, 0));
            motor.MoveTo(new Vector2(dir, 0), speed);

            if (!isMoving)
            {
                isMoving = true;
                anim.PlayState(movementState);
            }
        }

        private bool CanClimbUp()
        {
            if (target == null) return false;
            int dirSign = target.position.x > transform.position.x ? 1 : -1;
            Vector2 direction = Vector2.right * dirSign;
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.5f;

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, climbCheckDistance, climbObstacleMask);
            if (hit.collider != null)
            {
                Vector2 topOrigin = (Vector2)hit.point + Vector2.up * climbCheckHeight;
                RaycastHit2D topHit = Physics2D.Raycast(topOrigin, direction, 0.5f, climbObstacleMask);
                if (topHit.collider == null) return true;
            }
            return false;
        }

        private void StartClimb()
        {
            if (isClimbing || climbCooldownTimer > 0f) return;
            isClimbing = true;
            climbCooldownTimer = 2.5f;
            jumpEventTriggered = false;
            motor.FreezePhysics();
            motor.Stop();
            anim.PlayState(jumpAnim);
            if (climbCoroutine != null) StopCoroutine(climbCoroutine);
            climbCoroutine = StartCoroutine(ClimbRoutine());
        }

        private IEnumerator ClimbRoutine()
        {
            motor.UnfreezePhysics();
            motor.SetIgnorePhysicsUpdate(true);
            motor.ApplyKnockback(Vector2.up * climbForce);

            float waitForEvent = 0.6f;
            float elapsed = 0f;
            while (elapsed < waitForEvent)
            {
                elapsed += Time.deltaTime;
                if (jumpEventTriggered) break;
                yield return null;
            }

            if (!jumpEventTriggered)
            {
                int dirSign = target != null && target.position.x > transform.position.x ? 1 : -1;
                motor.ApplyKnockback(Vector2.right * dirSign * climbHorizontalForce);
            }

            yield return new WaitForSeconds(2.233f);

            motor.SetIgnorePhysicsUpdate(false);
            motor.Stop();
            isClimbing = false;
            climbCoroutine = null;
            climbCooldownTimer = 0f;

            anim.PlayState(movementState);
            isMoving = true;
        }

        public void OnJumpHighEvent()
        {
            if (isClimbing && motor != null && !jumpEventTriggered)
            {
                jumpEventTriggered = true;
                int dirSign = target != null && target.position.x > transform.position.x ? 1 : -1;
                motor.ApplyKnockback(Vector2.right * dirSign * climbHorizontalForce);
            }
        }

        private bool CheckPlayerDetected()
        {
            if (target == null) return false;
            Vector2 dirToPlayer = target.position - transform.position;
            float distance = dirToPlayer.magnitude;
            if (distance > viewDistance && distance > backDetectDistance) return false;

            float dot = Vector2.Dot(transform.right, dirToPlayer.normalized);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            bool inFront = angle < viewAngle * 0.5f;
            bool inBack = distance <= backDetectDistance;

            if (inFront)
            {
                RaycastHit2D hit = Physics2D.Linecast(transform.position, target.position, obstacleMask);
                if (hit.collider == null || hit.collider.transform == target)
                    return true;
            }
            else if (inBack)
            {
                return true;
            }
            return false;
        }

        private void StartAttack()
        {
            if (combat.IsAttacking || attackCooldownTimer > 0f) return;

            if (target != null)
            {
                float d = Mathf.Sign(target.position.x - transform.position.x);
                motor.FaceDirection(new Vector2(d, 0));
            }

            attackCooldownTimer = attackCooldown;
            motor.FreezePhysics();
            motor.Stop();
            anim.PlayState(clawAnim);
            combat.PerformAttack();
            isMoving = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 start = transform.position;

            Vector3 forward = transform.right;
            float halfAngle = viewAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 leftDir = Quaternion.Euler(0, 0, -halfAngle) * forward;
            Vector3 rightDir = Quaternion.Euler(0, 0, halfAngle) * forward;
            Gizmos.color = new Color(0, 1, 0, 0.15f);
            Gizmos.DrawLine(start, start + leftDir * viewDistance);
            Gizmos.DrawLine(start, start + rightDir * viewDistance);
            int segments = 20;
            Vector3 prev = start + leftDir * viewDistance;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = -halfAngle + t * 2 * halfAngle;
                Vector3 dir = Quaternion.Euler(0, 0, angle) * forward;
                Vector3 next = start + dir * viewDistance;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawWireSphere(start, backDetectDistance);

            DrawCircleAt(start, attackRange, new Color(1f, 0.3f, 0.3f));
            DrawCircleAt(start, minMountAttackRange, new Color(0.5f, 0.5f, 1f));
            DrawCircleAt(start, mountAttackRange, new Color(1f, 0.85f, 0f));
            float leapDistance = gizmoLeapSpeedX * gizmoLeapDuration;
            DrawCircleAt(start, leapDistance, new Color(0f, 0.8f, 1f));

            DrawLeapArrow(start, -1, leapDistance);
            DrawLeapArrow(start, +1, leapDistance);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(start + Vector3.up * 4.5f,
                $"红:攻击 {attackRange:F1}m\n" +
                $"蓝:最小飞扑 {minMountAttackRange:F1}m\n" +
                $"黄:最大飞扑 {mountAttackRange:F1}m\n" +
                $"青:飞扑距离 {leapDistance:F2}m",
                new GUIStyle
                {
                    normal = { textColor = Color.white },
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                });
#endif
        }

        private void DrawCircleAt(Vector3 center, float radius, Color c)
        {
            Gizmos.color = c;
            int seg = 48;
            Vector3 prev = center + Vector3.right * radius;
            for (int i = 1; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawLeapArrow(Vector3 origin, int dirSign, float distance)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
            Vector3 end = origin + new Vector3(dirSign * distance, 0f, 0f);
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawLine(end, end + new Vector3(-dirSign * 0.3f, 0.3f, 0f));
            Gizmos.DrawLine(end, end + new Vector3(-dirSign * 0.3f, -0.3f, 0f));
        }
    }
}