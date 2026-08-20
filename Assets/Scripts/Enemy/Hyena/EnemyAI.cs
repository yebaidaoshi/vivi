using UnityEngine;

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

        private enum State { Idle, Patrol, Chase }
        private State currentState;
        private float timer;
        private float patrolTargetX;
        private bool isChasing = false;
        private float attackCooldownTimer;
        private bool isMoving = false;

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
                    // 攻击期间完全锁定：不移动、不切换动画、不做任何逻辑
                    if (combat.IsAttacking)
                        break;

                    if (isChasing && target != null)
                    {
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

            attackCooldownTimer = attackCooldown;
            motor.FreezePhysics();
            motor.Stop();
            anim.PlayState(clawAnim);
            combat.PerformAttack();
            isMoving = false;
        }

        // ★ Gizmos 绘制（已恢复）
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;
            Vector3 start = transform.position;
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
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(start, backDetectDistance);
        }
    }
}