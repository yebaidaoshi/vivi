using UnityEngine;

namespace Player
{
    /// <summary>
    /// Lightweight replacement for the _Bullet PlayMaker fly/hit loop.
    /// floor.unity SetVelocity uses speed 75 along the spawn facing.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerBulletMover : MonoBehaviour
    {
        [SerializeField] private float speed = 75f;
        [SerializeField] private float lifetime = 2.5f;
        [SerializeField] private LayerMask hitMask = ~0;

        private Rigidbody2D _rb;
        private float _life;
        private int _facing = 1;
        private bool _launched;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            DisablePlayMaker();
        }

        public void Launch(int facing, float bulletSpeed = -1f)
        {
            Launch(new Vector2(facing >= 0 ? 1f : -1f, 0f), bulletSpeed);
        }

        [Tooltip("_Bullet prefab root is authored at euler z=90. Keep that offset on top of Atan2 (+X).")]
        [SerializeField] private float spriteAngleOffset = 90f;

        public void Launch(Vector2 direction, float bulletSpeed = -1f)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            _facing = direction.x >= 0f ? 1 : -1;
            if (bulletSpeed > 0f)
            {
                speed = bulletSpeed;
            }

            _life = lifetime;
            _launched = true;
            _rb.velocity = direction * speed;

            // Prefab default euler z=90; CreateObject used aim ZAngle on top of that orientation.
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + spriteAngleOffset);

            // Rotation alone aims the sprite; keep uniform scale (no X-flip mirror).
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x);
            s.y = Mathf.Abs(s.y);
            transform.localScale = s;
        }

        private void Update()
        {
            if (!_launched)
            {
                return;
            }

            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_launched || other == null)
            {
                return;
            }

            if (((1 << other.gameObject.layer) & hitMask) == 0 && hitMask != ~0)
            {
                return;
            }

            // Ignore self / ally projectiles / triggers on the shooter.
            if (other.CompareTag("AllyProjectile") || other.CompareTag("PlayerPresence")
                || other.CompareTag("FX"))
            {
                return;
            }

            Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_launched)
            {
                return;
            }

            Destroy(gameObject);
        }

        private void DisablePlayMaker()
        {
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                {
                    continue;
                }

                string tn = mb.GetType().FullName ?? "";
                if (tn.Contains("PlayMaker"))
                {
                    mb.enabled = false;
                }
            }
        }
    }
}
