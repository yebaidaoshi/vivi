using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMotor : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float maxSpeed = 50f;

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
        private bool ignorePhysicsUpdate = false;  // ★ 新增

        public bool IsGrounded => grounded;
        public Vector2 Velocity => rb.velocity;
        public float Speed => Mathf.Abs(rb.velocity.x);

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

        public bool IsFrozen => isFrozen;

        // ★ 新增：控制是否跳过物理更新
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
        }

        public void PhysicsUpdate()
        {
            grounded = CheckGrounded();
            if (isFrozen) return;

            // ★ 如果忽略物理更新，不覆盖速度
            if (ignorePhysicsUpdate)
                return;

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

        public void ApplyKnockback(Vector2 force)
        {
            if (isFrozen) return;
            rb.WakeUp();
            rb.AddForce(force, ForceMode2D.Impulse);
        }

        public void FaceDirection(Vector2 dir)
        {
            if (dir.x > 0.1f)
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
            else if (dir.x < -0.1f)
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
        }
    }
}