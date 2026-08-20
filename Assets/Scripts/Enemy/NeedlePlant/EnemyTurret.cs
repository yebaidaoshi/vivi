using Player;
using UnityEngine;

public class EnemyTurret : MonoBehaviour, IDamageable
{
    public enum State { Idle, Aiming, Fire, Hit, Die, AimingBurst, FireBurst }

    [Header("攻击设置")]
    public Transform player;
    public float attackRange = 8f;
    [Range(0f, 180f)] public float attackAngle = 60f;
    public float aimDuration = 0.5f;
    public float fireCooldown = 1.5f;
    public int damage = 10;

    [Header("大招设置")]
    public float aimBurstDuration = 0.75f;
    public float fireBurstDuration = 1f;
    public int damageBurst = 25;

    [Header("子弹预制体")]
    public GameObject needPrefab;
    public Transform firePoint;

    [Header("生命值")]
    public int maxHealth = 50;

    [Header("子弹方向修正")]
    public bool reverseBulletDirection = true; // ★ 如果子弹方向反了，勾选这个

    private State currentState = State.Idle;
    private int health;
    private float stateTimer;
    private bool hasFired;
    private Animator anim;

    private int _shotCounter;
    private int _burstFrameIndex;
    private float _burstTimer;
    private float[] _burstFrameTimes = { 0.05f, 0.3f, 0.583f };

    private Vector2 Forward => transform.right;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("❌ 缺少 Animator 组件！");
            enabled = false;
            return;
        }

        health = maxHealth;

        if (firePoint == null)
        {
            firePoint = transform.Find("FirePoint");
            if (firePoint == null) firePoint = transform;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        PlayState("Idle");
    }

    void Update()
    {
        if (currentState == State.Die || player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        switch (currentState)
        {
            case State.Idle:
                if (dist <= attackRange && IsPlayerInFront())
                    ChangeState(State.Aiming);
                else
                    _shotCounter = 0;
                break;

            case State.Aiming:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    ChangeState(State.Fire);
                break;

            case State.Fire:
                if (!hasFired)
                {
                    FireNeed();
                    hasFired = true;
                }
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    ChangeState(State.Idle);
                break;

            case State.AimingBurst:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    ChangeState(State.FireBurst);
                break;

            case State.FireBurst:
                stateTimer -= Time.deltaTime;
                _burstTimer += Time.deltaTime;

                if (_burstFrameIndex < _burstFrameTimes.Length)
                {
                    float targetTime = _burstFrameTimes[_burstFrameIndex];
                    if (_burstTimer >= targetTime)
                    {
                        // ★ 根据 reverseBulletDirection 决定是否反转方向
                        Vector2 fireDir = Forward;
                        if (reverseBulletDirection) fireDir = -fireDir;
                        SpawnBullet(fireDir, damageBurst);
                        _burstFrameIndex++;
                    }
                }

                if (stateTimer <= 0f)
                    ChangeState(State.Idle);
                break;

            case State.Hit:
                if (!IsPlayingState("Hit"))
                    ChangeState(State.Idle);
                break;
        }
    }

    private bool IsPlayerInFront()
    {
        if (player == null) return false;
        Vector2 toPlayer = (player.position - transform.position).normalized;
        float dot = Vector2.Dot(toPlayer, Forward);
        float cosHalfAngle = Mathf.Cos(attackAngle * 0.5f * Mathf.Deg2Rad);
        return dot >= cosHalfAngle;
    }

    private void FireNeed()
    {
        if (needPrefab == null || player == null || firePoint == null)
        {
            Debug.LogError("❌ 缺少必要引用，无法发射！");
            return;
        }

        if (_shotCounter >= 2)
        {
            _shotCounter = 0;
            ChangeState(State.AimingBurst);
            return;
        }

        // ★ 根据 reverseBulletDirection 决定是否反转方向
        Vector2 fireDir = Forward;
        if (reverseBulletDirection) fireDir = -fireDir;
        SpawnBullet(fireDir, damage);
        _shotCounter++;
    }

    private void SpawnBullet(Vector2 dir, int bulletDamage)
    {
        Vector3 spawnPos = firePoint.position;
        spawnPos.z = 0f;

        GameObject need = Instantiate(needPrefab, spawnPos, Quaternion.identity);
        SpriteRenderer sr = need.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 10;

        NeedProjectile proj = need.GetComponent<NeedProjectile>();
        if (proj != null)
            proj.Initialize(dir, bulletDamage, gameObject);
        else
        {
            Rigidbody2D rb = need.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = dir * 5f;
        }
    }

    void ChangeState(State newState)
    {
        if (currentState == State.Fire || currentState == State.FireBurst)
        {
            hasFired = false;
            _burstFrameIndex = 0;
            _burstTimer = 0f;
        }

        currentState = newState;
        stateTimer = 0f;

        string stateName = GetStateName(newState);
        PlayState(stateName);

        switch (newState)
        {
            case State.Aiming:
                stateTimer = aimDuration;
                break;
            case State.Fire:
                stateTimer = fireCooldown;
                break;
            case State.AimingBurst:
                stateTimer = aimBurstDuration;
                break;
            case State.FireBurst:
                stateTimer = fireBurstDuration;
                _burstFrameIndex = 0;
                _burstTimer = 0f;
                break;
        }

        if (newState == State.Die)
        {
            GetComponent<Collider2D>().enabled = false;
            enabled = false;
        }
    }

    private string GetStateName(State state)
    {
        switch (state)
        {
            case State.AimingBurst: return "Aiming_Burst";
            case State.FireBurst: return "Fire_Burst";
            default: return state.ToString();
        }
    }

    private void PlayState(string stateName)
    {
        if (anim == null) return;
        anim.Play(stateName, 0, 0f);
    }

    private bool IsPlayingState(string stateName)
    {
        if (anim == null) return false;
        return anim.GetCurrentAnimatorStateInfo(0).IsName(stateName);
    }

    // ========== IDamageable ==========
    public bool IsDead => currentState == State.Die;

    public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
    {
        if (currentState == State.Die) return;
        health -= damage;
        if (health <= 0)
        {
            ChangeState(State.Die);
            return;
        }
        ChangeState(State.Hit);
    }

    // ========== 可视化 ==========
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Vector3 center = transform.position;
        Vector3 forward = Forward;
        if (reverseBulletDirection) forward = -forward;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(center, forward * 3f);

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        int segments = 30;
        float halfAngleRad = attackAngle * 0.5f * Mathf.Deg2Rad;
        Vector3 startDir = Quaternion.Euler(0, 0, -halfAngleRad * Mathf.Rad2Deg) * forward;
        Vector3 endDir = Quaternion.Euler(0, 0, halfAngleRad * Mathf.Rad2Deg) * forward;

        Vector3 prevPoint = center + startDir * attackRange;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            Vector3 dir = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg) * forward;
            Vector3 point = center + dir * attackRange;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
        Gizmos.DrawLine(center, center + startDir * attackRange);
        Gizmos.DrawLine(center, center + endDir * attackRange);
    }
}