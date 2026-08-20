using Player;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ScoutCameraFinal : MonoBehaviour
{
    public Transform player;
    public float orthoSize = 10f;
    public float deadZoneWidth = 3f;
    public float deadZoneHeight = 2f;
    public float verticalOffset = 2.5f;   // 玩家在屏幕偏下
    public float jumpOffset = 1.5f;
    public float jumpSmooth = 0.15f;
    public float detectRadius = 12f;
    public LayerMask enemyLayer;
    [Range(0, 0.5f)] public float enemyInfluence = 0.2f;

    private Camera cam;
    private float fixedZ;
    private float jumpVel;

    void Awake()
    {
        cam = GetComponent<Camera>();
        fixedZ = transform.position.z;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 playerPos = player.position;
        Vector3 camPos = transform.position;

        // 玩家虚拟位置（向下偏移，使玩家在屏幕下方）
        Vector3 virtualPlayer = playerPos - new Vector3(0, verticalOffset, 0);

        // 偏移量（相对于摄像机）
        Vector3 delta = virtualPlayer - camPos;
        delta.z = 0;

        // 死区处理
        float dx = delta.x, dy = delta.y;
        bool moveX = Mathf.Abs(dx) > deadZoneWidth;
        bool moveY = Mathf.Abs(dy) > deadZoneHeight;

        Vector3 targetPos = camPos;
        if (moveX)
            targetPos.x += dx - Mathf.Sign(dx) * deadZoneWidth;
        if (moveY)
            targetPos.y += dy - Mathf.Sign(dy) * deadZoneHeight;
        targetPos.z = fixedZ;

        // 敌人影响（轻微偏移）
        Collider2D[] enemies = Physics2D.OverlapCircleAll(playerPos, detectRadius, enemyLayer);
        if (enemies.Length > 0 && enemyInfluence > 0.01f)
        {
            Vector3 avg = Vector3.zero;
            int cnt = 0;
            foreach (var e in enemies)
            {
                if (e != null && e.gameObject.activeInHierarchy)
                {
                    avg += e.transform.position;
                    cnt++;
                }
            }
            if (cnt > 0)
            {
                avg /= cnt;
                Vector3 offset = avg - targetPos;
                offset.y *= 0.5f;
                targetPos += offset * enemyInfluence;
            }
        }

        // 跳跃上抬（平滑）
        bool jumping = false;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null && rb.velocity.y > 1.5f) jumping = true;
        PlayerJump jc = player.GetComponent<PlayerJump>();
        if (jc != null && jc.OnAir) jumping = true;

        float jumpVal = jumping ? jumpOffset : 0f;
        jumpVal = Mathf.SmoothDamp(0f, jumpVal, ref jumpVel, jumpSmooth);
        targetPos.y += jumpVal;

        // ★ 核心：直接赋值，瞬间同步
        transform.position = targetPos;
        cam.orthographicSize = orthoSize;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Vector3 center = transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(deadZoneWidth * 2, deadZoneHeight * 2, 0.1f));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(player.position, detectRadius);
    }
}