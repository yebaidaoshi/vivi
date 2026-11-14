using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 通用感知组件：视野锥 + 遮挡线检测 + 背后近身感知 + 距离判断。
    /// 新敌人可以直接挂这个，不用再每个 AI 抄一遍 CheckPlayerDetected。
    ///
    /// 用法：
    ///   sensor.TryAcquireTarget();           // 找玩家（按 tag）
    ///   if (sensor.CanSeeTarget()) ...        // 视野内且无遮挡
    ///   if (sensor.InRange(sensor.MeleeRange)) ...
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySensor : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private string[] targetTags = { "PlayerPresence", "Player" };
        [SerializeField] private Transform target;
        [Tooltip("目标丢失后仍保留引用的时间（秒）。")]
        [SerializeField] private float targetMemory = 3f;

        [Header("视野")]
        [SerializeField] private float viewDistance = 8f;
        [Tooltip("视野总角度（度）。")]
        [SerializeField] private float viewAngle = 60f;
        [Tooltip("背后贴脸就能发现的距离。")]
        [SerializeField] private float backDetectDistance = 2f;
        [Tooltip("遮挡层（墙、地面等）。")]
        [SerializeField] private LayerMask obstacleMask;

        [Header("距离")]
        [SerializeField] private float meleeRange = 2.5f;

        private float _lostTimer;

        public Transform Target => target;
        public float ViewDistance => viewDistance;
        public float MeleeRange => meleeRange;
        public bool HasTarget => target != null;

        public void SetTarget(Transform t)
        {
            target = t;
            _lostTimer = 0f;
        }

        /// <summary>按 tag 找目标；找不到返回 null。</summary>
        public Transform TryAcquireTarget()
        {
            if (target != null) return target;
            if (targetTags == null) return null;

            for (int i = 0; i < targetTags.Length; i++)
            {
                if (string.IsNullOrEmpty(targetTags[i])) continue;
                GameObject go = GameObject.FindGameObjectWithTag(targetTags[i]);
                if (go != null)
                {
                    target = go.transform;
                    return target;
                }
            }

            return null;
        }

        /// <summary>视野内 + 无遮挡（或贴脸）判定。</summary>
        public bool CanSeeTarget()
        {
            if (target == null) return false;

            Vector2 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;

            if (distance > viewDistance && distance > backDetectDistance)
            {
                _lostTimer += Time.deltaTime;
                if (_lostTimer > targetMemory) target = null;
                return false;
            }

            float dot = Vector2.Dot(transform.right, toTarget.normalized);
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            bool inFront = angle < viewAngle * 0.5f;
            bool inBack = distance <= backDetectDistance;

            if (!inFront && !inBack)
            {
                _lostTimer += Time.deltaTime;
                if (_lostTimer > targetMemory) target = null;
                return false;
            }

            if (obstacleMask.value != 0)
            {
                RaycastHit2D hit = Physics2D.Linecast(transform.position, target.position, obstacleMask);
                if (hit.collider != null && hit.collider.transform != target
                    && !hit.collider.transform.IsChildOf(target))
                {
                    _lostTimer += Time.deltaTime;
                    if (_lostTimer > targetMemory) target = null;
                    return false;
                }
            }

            _lostTimer = 0f;
            return true;
        }

        public float HorizontalDistanceToTarget()
        {
            if (target == null) return float.PositiveInfinity;
            return Mathf.Abs(target.position.x - transform.position.x);
        }

        public bool InRange(float range)
        {
            return HorizontalDistanceToTarget() <= range;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Vector3 right = transform.right;

            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            float half = viewAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 a = Quaternion.Euler(0, 0, -half * Mathf.Rad2Deg) * right;
            Vector3 b = Quaternion.Euler(0, 0, half * Mathf.Rad2Deg) * right;
            Gizmos.DrawLine(origin, origin + a * viewDistance);
            Gizmos.DrawLine(origin, origin + b * viewDistance);

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(origin, backDetectDistance);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(origin, meleeRange);
        }
    }
}
