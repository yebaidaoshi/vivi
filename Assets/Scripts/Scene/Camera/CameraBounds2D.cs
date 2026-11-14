using UnityEngine;

namespace SceneCamera
{
    /// <summary>
    /// 相机边界：用一个 Collider2D（Box / Polygon / Composite）限定相机可视范围。
    /// 对应原版的 CinemachineConfiner（m_ConfineMode = 2D, m_ConfineScreenEdges = 1）。
    ///
    /// 用法：在场景里放一个带 Collider2D 的空物体当“相机区域”，
    /// 把这个组件挂到 Main Camera，把区域拖到 Bounds Collider。
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraBounds2D : MonoBehaviour
    {
        [Tooltip("相机可移动区域的碰撞体。留空则自动取本物体或子物体上的 Collider2D。")]
        [SerializeField] private Collider2D boundsCollider;

        [Tooltip("没有碰撞体时，用这个矩形当边界（世界坐标，中心 + 尺寸）。")]
        [SerializeField] private bool useFallbackRect;
        [SerializeField] private Vector2 fallbackCenter = Vector2.zero;
        [SerializeField] private Vector2 fallbackSize = new Vector2(40f, 20f);

        private Bounds cachedBounds;
        private bool cached;

        public Collider2D BoundsCollider => boundsCollider;

        private void Awake()
        {
            if (boundsCollider == null)
            {
                boundsCollider = GetComponent<Collider2D>();
                if (boundsCollider == null) boundsCollider = GetComponentInChildren<Collider2D>();
            }
        }

        /// <summary>
        /// 把相机位置夹到边界内，保证画面不越界。
        /// 如果某个轴上边界比视野还小，则强制居中。
        /// </summary>
        public Vector3 Clamp(Camera cam, Vector3 position)
        {
            if (cam == null) return position;

            Bounds b;
            if (!TryGetBounds(out b)) return position;

            float halfHeight = cam.orthographic ? cam.orthographicSize : 0f;
            if (!cam.orthographic) return position; // 透视相机暂不做边界

            float halfWidth = halfHeight * cam.aspect;

            float minX = b.min.x + halfWidth;
            float maxX = b.max.x - halfWidth;
            float minY = b.min.y + halfHeight;
            float maxY = b.max.y - halfHeight;

            float x = minX > maxX ? b.center.x : Mathf.Clamp(position.x, minX, maxX);
            float y = minY > maxY ? b.center.y : Mathf.Clamp(position.y, minY, maxY);

            return new Vector3(x, y, position.z);
        }

        private bool TryGetBounds(out Bounds b)
        {
            if (boundsCollider != null)
            {
                b = boundsCollider.bounds;
                return true;
            }

            if (useFallbackRect)
            {
                b = new Bounds(fallbackCenter, fallbackSize);
                return true;
            }

            if (!cached)
            {
                var col = GetComponentInChildren<Collider2D>();
                if (col != null)
                {
                    boundsCollider = col;
                    cachedBounds = col.bounds;
                    cached = true;
                    b = cachedBounds;
                    return true;
                }
            }

            b = default;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Bounds b;
            if (!TryGetBounds(out b)) return;

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
