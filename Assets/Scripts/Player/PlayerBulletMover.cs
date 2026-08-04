using UnityEngine;

namespace Player
{
    /// <summary>
    /// _Bullet PlayMaker 飞行/命中循环的轻量替代。
    /// floor.unity SetVelocity 沿生成朝向使用速度 75。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerBulletMover : MonoBehaviour
    {
        [SerializeField] private float speed = 75f;
        [SerializeField] private float lifetime = 2.5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private GameObject hitFxPrefab;
        [SerializeField] private float hitFxLifetime = 1.5f;

        [Tooltip("_Bullet 预制体根制作时欧拉角 z=90。在 Atan2（+X）之上保留该偏移。")]
        [SerializeField] private float spriteAngleOffset = 90f;

        private Rigidbody2D _rb;
        private float _life;
        private int _facing = 1;
        private bool _launched;
        private bool _spent;
        private Transform _owner;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            DisableLegacyDrivers();
#if UNITY_EDITOR
            if (hitFxPrefab == null)
            {
                hitFxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameObject/Bullet_GoldFire_Medium_Impact.prefab");
            }
#endif
        }

        /// <summary>
        /// 忽略射击者碰撞体，以免枪口生成与女主重叠
        ///（瞄准姿势落定前的首发很常见）而立刻消耗。
        /// </summary>
        public void SetOwner(Transform owner)
        {
            _owner = owner;
            var myCols = GetComponentsInChildren<Collider2D>(true);
            if (owner == null || myCols == null || myCols.Length == 0)
            {
                return;
            }

            var ownerCols = owner.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < myCols.Length; i++)
            {
                if (myCols[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < ownerCols.Length; j++)
                {
                    if (ownerCols[j] == null)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(myCols[i], ownerCols[j], true);
                }
            }
        }

        public void Launch(int facing, float bulletSpeed = -1f)
        {
            Launch(new Vector2(facing >= 0 ? 1f : -1f, 0f), bulletSpeed);
        }

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

            // 预制体默认欧拉角 z=90；CreateObject 在该朝向上叠加瞄准 ZAngle。
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + spriteAngleOffset);

            // 仅靠旋转瞄准精灵；保持均匀缩放（无 X 翻转镜像）。
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
            if (!_launched || other == null || ShouldIgnore(other.transform, other.gameObject))
            {
                return;
            }

            if (((1 << other.gameObject.layer) & hitMask) == 0 && hitMask != ~0)
            {
                return;
            }

            Spend(transform.position);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_launched || collision == null || collision.collider == null)
            {
                return;
            }

            if (ShouldIgnore(collision.transform, collision.gameObject))
            {
                return;
            }

            Vector3 pos = collision.contactCount > 0
                ? (Vector3)collision.GetContact(0).point
                : transform.position;
            Spend(pos);
        }

        private bool ShouldIgnore(Transform otherT, GameObject otherGo)
        {
            if (otherGo == null)
            {
                return true;
            }

            // 忽略自身 / 友方投射物 / 特效 / 玩家存在。
            if (otherGo.CompareTag("AllyProjectile") || otherGo.CompareTag("PlayerPresence")
                || otherGo.CompareTag("FX"))
            {
                return true;
            }

            if (_owner != null && otherT != null
                && (otherT == _owner || otherT.IsChildOf(_owner)))
            {
                return true;
            }

            return false;
        }

        private void Spend(Vector3 hitPos)
        {
            if (_spent)
            {
                return;
            }

            _spent = true;
            if (hitFxPrefab != null)
            {
                var fx = Instantiate(hitFxPrefab, hitPos, Quaternion.identity);
                if (hitFxLifetime > 0f)
                {
                    Destroy(fx, hitFxLifetime);
                }
            }

            Destroy(gameObject);
        }

        private void DisableLegacyDrivers()
        {
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb == this)
                {
                    continue;
                }

                string tn = mb.GetType().FullName ?? "";
                // 撕取预制体上的 PlayMaker FSM（缺失 DLL）与 3D ExplodingProjectile。
                if (tn.Contains("PlayMaker") || tn.Contains("ExplodingProjectile"))
                {
                    mb.enabled = false;
                }
            }
        }
    }
}
