using Chronos;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Rigidbody2D 驱动器：即时速度、朝向翻转、地面/墙壁探测。
    /// 若存在 Chronos Timeline.rigidbody2D 则使用之（与 SetVelocity2dChronos 相同）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField]
        private PlayerMotorSettings settings = new PlayerMotorSettings();

        private Rigidbody2D _rb;
        private Timeline _timeline;
        private int _resolvedGroundMask;
        private float _smoothedX;
        private float _coyoteTimer;
        private bool _grounded;
        private bool _wallLeft;
        private bool _wallRight;
        private int _facing = 1;
        private Vector3 _baseScale = Vector3.one;

        public PlayerMotorSettings Settings => settings;
        public bool IsGrounded => _grounded;
        public bool WallLeft => _wallLeft;
        public bool WallRight => _wallRight;
        public int Facing => _facing;
        public float SmoothedVelocityX => _smoothedX;
        public Vector2 Velocity => GetVelocity();
        public float TimeScale => _timeline != null ? _timeline.timeScale : 1f;
        public float DeltaTime => _timeline != null ? _timeline.deltaTime : Time.deltaTime;
        public float FixedDeltaTime => _timeline != null ? _timeline.fixedDeltaTime : Time.fixedDeltaTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _timeline = GetComponent<Timeline>();

            // 解析地面遮罩：0（Nothing）=> 自动 Ground + GroundCollider。
            _resolvedGroundMask = settings.groundMask.value;
            if (_resolvedGroundMask == 0)
            {
                int auto = LayerMask.GetMask("Ground", "GroundCollider");
                _resolvedGroundMask = auto != 0 ? auto : ~0;
            }

            // Chronos 拥有 gravityScale（会把 Rigidbody2D 置零并由自身积分）。
            if (_timeline == null)
            {
                _rb.gravityScale = settings.gravityScale;
            }

            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 保留制作时设定的（均匀）缩放；朝向只翻转其符号。
            _baseScale = transform.localScale;
            if (Mathf.Abs(_baseScale.x) < 0.0001f)
            {
                _baseScale.x = 1f;
            }

            _facing = transform.localScale.x >= 0f ? 1 : -1;
        }

        private Vector2 GroundOrigin()
        {
            return (Vector2)transform.position
                + new Vector2(settings.groundCheckOffset.x * _facing, settings.groundCheckOffset.y);
        }

        public void ProbeEnvironment()
        {
            _grounded = CheckGrounded();

            _wallRight = CastWall(Vector2.right);
            _wallLeft = CastWall(Vector2.left);

            if (_grounded)
            {
                _coyoteTimer = settings.coyoteTime;
            }
            else
            {
                _coyoteTimer -= FixedDeltaTime;
            }
        }

        private bool CheckGrounded()
        {
            // 仅在非上升时计为着地（防止刚起跳后粘地）。
            if (GetVelocity().y > 0.5f)
            {
                return false;
            }

            Vector2 origin = GroundOrigin();

            // Overlap 捕获静止/穿透情况；CircleCast 捕获接近过程。
            var overlap = Physics2D.OverlapCircle(origin, settings.groundCastRadius, _resolvedGroundMask);
            if (IsValidGround(overlap))
            {
                return true;
            }

            var hit = Physics2D.CircleCast(origin, settings.groundCastRadius, Vector2.down,
                settings.groundCastDistance, _resolvedGroundMask);
            return IsValidGround(hit.collider);
        }

        private bool IsValidGround(Collider2D col)
        {
            return col != null && !col.isTrigger
                && col.transform != transform
                && !col.transform.IsChildOf(transform);
        }

        public bool CanJump => _grounded || _coyoteTimer > 0f;

        public void SetImmediateVelocityX(float x)
        {
            _smoothedX = x;
            var v = GetVelocity();
            v.x = x;
            SetVelocity(v);
        }

        public void SetVelocityY(float y)
        {
            var v = GetVelocity();
            v.y = y;
            SetVelocity(v);
        }

        public void SetVelocity(Vector2 velocity)
        {
            if (_timeline != null && _timeline.rigidbody2D != null)
            {
                _timeline.rigidbody2D.velocity = velocity;
            }
            else
            {
                _rb.velocity = velocity;
            }
        }

        public Vector2 GetVelocity()
        {
            if (_timeline != null && _timeline.rigidbody2D != null)
            {
                return _timeline.rigidbody2D.velocity;
            }

            return _rb.velocity;
        }

        public void AddForce(Vector2 force, ForceMode2D mode = ForceMode2D.Impulse)
        {
            if (_timeline != null && _timeline.rigidbody2D != null)
            {
                _timeline.rigidbody2D.AddForce(force, mode);
            }
            else
            {
                _rb.AddForce(force, mode);
            }
        }

        public void ClampFallSpeed()
        {
            if (settings.maxFallSpeed <= 0f)
            {
                return;
            }

            var v = GetVelocity();
            if (v.y < -settings.maxFallSpeed)
            {
                v.y = -settings.maxFallSpeed;
                SetVelocity(v);
            }
        }

        public void UpdateFacing(float moveAxis, bool allowFlip)
        {
            if (!allowFlip || Mathf.Abs(moveAxis) < 0.01f)
            {
                ApplyFacingScale();
                return;
            }

            int desired = moveAxis > 0f ? 1 : -1;
            if (desired != _facing)
            {
                _facing = desired;
            }

            ApplyFacingScale();
        }

        public void ForceFacing(int facing)
        {
            _facing = facing >= 0 ? 1 : -1;
            ApplyFacingScale();
        }

        private void ApplyFacingScale()
        {
            var s = _baseScale;
            s.x = Mathf.Abs(_baseScale.x) * _facing;
            transform.localScale = s;
        }

        private bool CastWall(Vector2 dir)
        {
            Vector2 origin = (Vector2)transform.position + settings.wallCheckOffset;
            var hit = Physics2D.CircleCast(origin, settings.wallCastRadius, dir,
                settings.wallCastDistance, _resolvedGroundMask);
            return IsValidGround(hit.collider);
        }

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			int facing = Application.isPlaying ? _facing : 1;
			Vector2 g = (Vector2)transform.position
				+ new Vector2(settings.groundCheckOffset.x * facing, settings.groundCheckOffset.y);
			Gizmos.color = Application.isPlaying && _grounded ? Color.green : Color.yellow;
			Gizmos.DrawWireSphere(g, settings.groundCastRadius);
			Gizmos.DrawLine(g, g + Vector2.down * settings.groundCastDistance);

			Vector2 w = (Vector2)transform.position + settings.wallCheckOffset;
			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(w, w + Vector2.right * settings.wallCastDistance);
			Gizmos.DrawLine(w, w + Vector2.left * settings.wallCastDistance);
		}
#endif
    }
}
