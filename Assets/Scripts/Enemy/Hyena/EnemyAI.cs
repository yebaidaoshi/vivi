using UnityEngine;
using System.Collections;

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
        [SerializeField] private float runThreshold = 4f;

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

        // ---- 攀爬检测参数 ----
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

        // ---- 攀爬状态 ----
        private bool isClimbing = false;
        private float climbCooldownTimer = 0f;
        private Coroutine climbCoroutine;
        private bool jumpEventTriggered = false;

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
        }

        public void Tick()
        {
            if (target == null || health.IsDead) return;

            if (attackCooldownTimer > 0f)
                attackCooldownTimer -= Time.deltaTime;

            if (climbCooldownTimer > 0f)
                climbCooldownTimer -= Time.deltaTime;

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
                        if (isChasing)
                            currentState = State.Chase;
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
                    if (combat.IsAttacking)
                        break;

                    if (isClimbing)
                        break;

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

                        if (distX <= attackRange)
                        {
                            motor.Stop();
                            if (!combat.IsAttacking && attackCooldownTimer <= 0f)
                                StartAttack();
                        }
                        else
                        {
                            float dir = Mathf.Sign(target.position.x - transform.position.x);
                            motor.FaceDirection(new Vector2(dir, 0));

                            float targetSpeed = distX > runThreshold ? runSpeed : walkSpeed;
                            motor.MoveTo(new Vector2(dir, 0), targetSpeed);

                            if (!isMoving)
                            {
                                isMoving = true;
                                anim.PlayState(movementState);
                            }
                        }
                    }
                    break;
            }
        }

        // ---- 检测是否可以攀爬 ----
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

                if (topHit.collider == null)
                    return true;
            }

            return false;
        }

        // ---- 开始攀爬 ----
        private void StartClimb()
        {
            if (isClimbing || climbCooldownTimer > 0f) return;

            isClimbing = true;
            climbCooldownTimer = 2.5f;
            jumpEventTriggered = false;

            motor.FreezePhysics();
            motor.Stop();
            anim.PlayState(jumpAnim);

            if (climbCoroutine != null)
                StopCoroutine(climbCoroutine);
            climbCoroutine = StartCoroutine(ClimbRoutine());
        }

        // ---- 攀爬协程 ----
        private IEnumerator ClimbRoutine()
        {
            // 1. 解冻并施加向上力（起跳）
            motor.UnfreezePhysics();
            motor.SetIgnorePhysicsUpdate(true);
            motor.ApplyKnockback(Vector2.up * climbForce);

            // 2. 等待动画事件触发（大约到第36帧，约0.6秒）
            float waitForEvent = 0.6f;
            float elapsed = 0f;
            while (elapsed < waitForEvent)
            {
                elapsed += Time.deltaTime;
                if (jumpEventTriggered)
                    break;
                yield return null;
            }

            // 如果事件未触发，作为保险也施加水平力
            if (!jumpEventTriggered)
            {
                int dirSign = target != null && target.position.x > transform.position.x ? 1 : -1;
                motor.ApplyKnockback(Vector2.right * dirSign * climbHorizontalForce);
            }

            // 3. 等待动画播放完毕（剩余时间 ≈ 2.233秒）
            yield return new WaitForSeconds(2.233f);

            // 4. 强制重置所有状态
            motor.SetIgnorePhysicsUpdate(false);
            motor.Stop();
            isClimbing = false;
            climbCoroutine = null;
            climbCooldownTimer = 0f;

            // 5. 恢复移动状态，继续追击
            anim.PlayState(movementState);
            isMoving = true;
        }

        // ---- 接收动画事件（第36帧触发） ----
        public void OnJumpHighEvent()
        {
            if (isClimbing && motor != null && !jumpEventTriggered)
            {
                jumpEventTriggered = true;
                int dirSign = target != null && target.position.x > transform.position.x ? 1 : -1;
                motor.ApplyKnockback(Vector2.right * dirSign * climbHorizontalForce);
            }
        }

        // ---- 检查玩家是否被检测到 ----
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

        // ---- 开始攻击 ----
        private void StartAttack()
        {
            if (combat.IsAttacking || attackCooldownTimer > 0f) return;

            attackCooldownTimer = attackCooldown;
            motor.FreezePhysics();
            motor.Stop();
            anim.PlayState(clawAnim);
            combat.PerformAttack();
            isMoving = false;
        }

        // ---- 可视化调试（保留原有视野绘制） ----
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;

            Vector3 start = transform.position;

            // 视野扇形
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Vector3 forward = transform.right;
            float halfAngle = viewAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 leftDir = Quaternion.Euler(0, 0, -halfAngle) * forward;
            Vector3 rightDir = Quaternion.Euler(0, 0, halfAngle) * forward;
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

            // 背后检测圈
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(start, backDetectDistance);
        }
    }
}