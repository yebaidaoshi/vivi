using Player;
using UnityEngine;
using Spine.Unity;

public class EnemyTurret : MonoBehaviour, IDamageable
{
    public enum State { Idle, Aiming, Fire, Hit, Die }

    [Header("攻击设置")]
    public Transform player;
    public float attackRange = 8f;
    public float aimDuration = 0.5f;
    public float fireCooldown = 1.5f;
    public int damage = 10;

    [Header("子弹预制体")]
    public GameObject needPrefab;
    public Transform firePoint;

    [Header("生命值")]
    public int maxHealth = 50;

    private State currentState = State.Idle;
    private int health;
    private float stateTimer;
    private bool hasFired;
    private Animator anim;

    // 获取实际朝向（世界空间中的右方向）
    private Vector2 Forward => transform.right;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("❌ 缺少 Animator 组件！请添加 SkeletonMecanim。");
            enabled = false;
            return;
        }

        if (anim.runtimeAnimatorController == null)
            Debug.LogWarning("⚠️ Animator Controller 未赋值，请在 SkeletonMecanim 的 Animator 字段中指定。");

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
                // 检测玩家是否在前方且距离足够
                if (dist <= attackRange && IsPlayerInFront())
                    ChangeState(State.Aiming);
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

            case State.Hit:
                if (!IsPlayingState("Hit"))
                    ChangeState(State.Idle);
                break;
        }
    }

    /// <summary>判断玩家是否在敌人正前方（夹角小于 60°）</summary>
    private bool IsPlayerInFront()
    {
        Vector2 toPlayer = (player.position - transform.position).normalized;
        float dot = Vector2.Dot(toPlayer, Forward);
        return dot > 0.5f; // 0.5 ≈ 60°
    }

    void ChangeState(State newState)
    {
        if (currentState == State.Fire)
            hasFired = false;

        currentState = newState;
        stateTimer = 0f;

        string stateName = newState.ToString();
        Debug.Log($"🔄 切换到状态: {stateName}");
        PlayState(stateName);

        if (newState == State.Aiming)
            stateTimer = aimDuration;
        else if (newState == State.Fire)
            stateTimer = fireCooldown;

        if (newState == State.Die)
        {
            GetComponent<Collider2D>().enabled = false;
            enabled = false;
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
        var info = anim.GetCurrentAnimatorStateInfo(0);
        return info.IsName(stateName);
    }

    private void FireNeed()
    {
        if (needPrefab == null || player == null || firePoint == null)
        {
            Debug.LogError("❌ 缺少必要引用，无法发射！");
            return;
        }

        // 子弹向敌人当前朝向发射（使用 transform.right）
        Vector2 dir = Forward;
        Vector3 spawnPos = firePoint.position;
        spawnPos.z = 0f;

        GameObject need = Instantiate(needPrefab, spawnPos, Quaternion.identity);
        SpriteRenderer sr = need.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 10;

        NeedProjectile proj = need.GetComponent<NeedProjectile>();
        if (proj != null)
            proj.Initialize(dir, damage, gameObject);
        else
        {
            Rigidbody2D rb = need.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = dir * 5f;
        }
    }

    // ---------- IDamageable ----------
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 显示朝向（蓝色射线）
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, (Vector3)Forward * 2f);
    }
}