using UnityEngine;
using Player;   // 用于 IDamageable

public class NeedProjectile : MonoBehaviour
{
    [Header("飞行参数")]
    public float speed = 8f;
    public int damage = 10;
    public float lifetime = 3f;

    private Vector2 direction;
    private GameObject owner;
    private Rigidbody2D rb;

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
        // 如果是发射者本身，忽略
        if (other.gameObject == owner) return;

        // ★ 检测是否被玩家的攻击碰撞体击中（玩家挥刀砍到子弹）
        if (other.CompareTag("PlayerAttack"))
        {
            Destroy(gameObject);
            return;
        }

        // 对玩家造成伤害（原有逻辑）
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null && !damageable.IsDead)
        {
            Vector2 knockback = direction * 3f;
            damageable.TakeDamage(damage, knockback, owner);
        }

        Destroy(gameObject);
    }
}