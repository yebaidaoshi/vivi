using UnityEngine;
using Player;

public class NeedProjectile : MonoBehaviour
{
    [Header("飞行参数")]
    public float speed = 8f;
    public int damage = 10;
    public float lifetime = 3f;

    private Vector2 direction;
    private GameObject owner;
    private Rigidbody2D rb;
    private bool _hasHit;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public void Initialize(Vector2 dir, int dmg, GameObject ownerObj)
    {
        direction = dir.normalized;
        damage = dmg;
        owner = ownerObj;
        rb.velocity = direction * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;
        if (other.gameObject == owner) return;

        if (other.CompareTag("PlayerAttack"))
        {
            Destroy(gameObject);
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();

        // ★ 如果找不到，尝试从 DamageTarget 获取
        if (damageable == null)
        {
            var link = other.GetComponent<DamageTarget>();
            if (link != null) damageable = link.Damageable;
        }

        if (damageable != null && !damageable.IsDead)
        {
            _hasHit = true;
            Vector2 knockback = direction * 3f;
            damageable.TakeDamage(damage, knockback, owner);
            Destroy(gameObject);
        }
    }
}