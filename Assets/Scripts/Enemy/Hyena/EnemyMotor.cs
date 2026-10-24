using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMotor : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float maxSpeed = 50f;

        [Header("击退")]
        [Tooltip("击退后多少秒内不覆盖水平速度（让冲量自然衰减）。")]
        [SerializeField] private float knockbackIgnoreDuration = 1.5f;

        [Header("地面探测")]
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;

        private Rigidbody2D rb;
        private float targetVelocityX;
        private bool grounded;
        private RigidbodyType2D originalBodyType;
        private RigidbodyConstraints2D originalConstraints;
        private bool isFrozen = false;
        private bool ignorePhysicsUpdate = false;
        private float knockbackTimer = 0f;

        private int facing = 1;
        public int Facing => facing;
        public Rigidbody2D Rb => rb;

        public bool IsGrounded => grounded;
        public Vector2 Velocity => rb.velocity;
        public float Speed => Mathf.Abs(rb.velocity.x);
        public bool IsFrozen => isFrozen;

        public void FreezePhysics()
        {
            if (isFrozen) return;
            originalBodyType = rb.bodyType;
            originalConstraints = rb.constraints;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            isFrozen = true;
        }

        public void UnfreezePhysics()
        {
            if (!isFrozen) return;
            rb.bodyType = originalBodyType;
            rb.constraints = originalConstraints;
            isFrozen = false;
            rb.WakeUp();
            targetVelocityX = 0f;
            rb.velocity = new Vector2(0f, rb.velocity.y);
            transform.position = rb.position;
        }

        public void SetIgnorePhysicsUpdate(bool ignore)
        {
            ignorePhysicsUpdate = ignore;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            originalBodyType = rb.bodyType;
            originalConstraints = rb.constraints;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            facing = transform.localScale.x >= 0f ? 1 : -1;
        }

        public void PhysicsUpdate()
        {
            grounded = CheckGrounded();

            if (isFrozen) return;
            if (ignorePhysicsUpdate) return;

            // ★ 击退期间不覆盖速度，让 AddForce 的冲量自然衰减
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                transform.position = rb.position;
                return;
            }

            rb.velocity = new Vector2(targetVelocityX, rb.velocity.y);
            transform.position = rb.position;
        }

        private bool CheckGrounded()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.down * 0.1f;
            var hit = Physics2D.CircleCast(origin, groundCheckRadius, Vector2.down, groundCheckDistance, groundMask);
            return hit.collider != null;
        }

        public void MoveTo(Vector2 direction, float speed)
        {
            if (isFrozen) return;
            float dirX = Mathf.Sign(direction.x);
            if (Mathf.Abs(direction.x) < 0.01f) dirX = 0f;
            float targetSpeed = Mathf.Clamp(speed, 0f, maxSpeed);
            targetVelocityX = dirX * targetSpeed;
        }

        public void Stop()
        {
            if (isFrozen) return;
            targetVelocityX = 0f;
        }

        /// <summary>
        /// 施加击退冲量。会在 knockbackIgnoreDuration 秒内禁止 PhysicsUpdate 覆盖速度，
        /// 让冲量自然衰减。
        /// </summary>
        public void ApplyKnockback(Vector2 force)
        {
            if (isFrozen) return;
            rb.WakeUp();
            rb.AddForce(force, ForceMode2D.Impulse);
            knockbackTimer = knockbackIgnoreDuration;
        }

        public void SetVelocityX(float vx)
        {
            if (isFrozen) return;
            rb.WakeUp();
            rb.velocity = new Vector2(vx, rb.velocity.y);
            targetVelocityX = vx;
        }

        public void SetVelocity(Vector2 v)
        {
            if (isFrozen) return;
            rb.WakeUp();
            rb.velocity = v;
            targetVelocityX = v.x;
        }

        public void SetRigidbodyPositionX(float x)
        {
            if (rb == null) return;
            Vector2 p = rb.position;
            p.x = x;
            rb.position = p;
        }

        public void SetInterpolation(RigidbodyInterpolation2D mode)
        {
            if (rb != null) rb.interpolation = mode;
        }

        public void FaceDirection(Vector2 dir)
        {
            if (dir.x > 0.1f) SetFacing(1);
            else if (dir.x < -0.1f) SetFacing(-1);
        }

        public void SetFacing(int dir)
        {
            if (dir == 0 || dir == facing) return;
            facing = dir;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * dir;
            transform.localScale = s;
        }
    }
}