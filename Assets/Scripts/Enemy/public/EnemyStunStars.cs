using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 眩晕星星：不用任何美术资源，运行时生成五角星贴图，围绕头顶转圈。
    /// 由 EnemyStunGauge.IsStunned 驱动，挂在敌人身上或由 EnemyController 自动补。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyStunStars : MonoBehaviour
    {
        [SerializeField] private EnemyStunGauge gauge;
        [SerializeField] private int starCount = 3;
        [SerializeField] private float radius = 0.55f;
        [SerializeField] private float height = 2.9f;
        [SerializeField] private float orbitSpeed = 200f;
        [SerializeField] private float starSize = 0.34f;
        [SerializeField] private Color starColor = new Color(1f, 0.9f, 0.25f, 1f);
        [SerializeField] private int sortingOrder = 31000;

        private static Sprite _starSprite;

        private Transform _root;
        private Transform[] _stars;
        private float _angle;

        private void Awake()
        {
            if (gauge == null) gauge = GetComponent<EnemyStunGauge>();
        }

        private void Start()
        {
            Build();
            SetVisible(false);
        }

        private static Sprite StarSprite()
        {
            if (_starSprite != null) return _starSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * 0.47f;
            float inner = outer * 0.45f;

            // 10 个顶点：外/内交替
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.Deg2Rad * (90f + i * 36f);
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), pts);
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;

            _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _starSprite.hideFlags = HideFlags.HideAndDontSave;
            return _starSprite;
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private void Build()
        {
            if (_root != null) return;

            var root = new GameObject("EnemyStunStars");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, height, 0f);
            _root = root.transform;

            _stars = new Transform[starCount];
            for (int i = 0; i < starCount; i++)
            {
                var go = new GameObject("star" + i);
                go.transform.SetParent(_root, false);
                go.transform.localScale = Vector3.one * starSize;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = StarSprite();
                sr.color = starColor;
                sr.sortingOrder = sortingOrder;

                _stars[i] = go.transform;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            bool stunned = gauge != null && gauge.IsStunned;
            SetVisible(stunned);
            if (!stunned || _stars == null) return;

            _angle += orbitSpeed * Time.unscaledDeltaTime;
            float sign = transform.lossyScale.x < 0f ? -1f : 1f;

            for (int i = 0; i < _stars.Length; i++)
            {
                float a = Mathf.Deg2Rad * (_angle + i * (360f / _stars.Length));
                _stars[i].localPosition = new Vector3(Mathf.Cos(a) * radius * sign, Mathf.Sin(a) * 0.22f, 0f);
                _stars[i].localRotation = Quaternion.Euler(0f, 0f, _angle * 2f);
            }
        }
    }
}
