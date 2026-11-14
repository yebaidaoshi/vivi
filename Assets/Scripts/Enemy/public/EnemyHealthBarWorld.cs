using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人头顶血条（世界空间，自动生成，不需要任何美术资源）。
    ///
    /// 原版普通敌人是不显示血条的（只有 Boss 用 HealthCanvas2），
    /// 这个组件是按需加的功能：挂到敌人身上即生效，删掉就回到原作表现。
    ///
    /// 表现：黑色底 + 黄色拖尾 + 红色实时条，掉血时拖尾缓慢追赶（血条插值）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBarWorld : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Vector2 size = new Vector2(1.6f, 0.16f);
        [SerializeField] private float offsetY = 2.6f;

        [SerializeField] private Color fillColor = new Color(0.85f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color trailColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

        [SerializeField] private int sortingOrder = 30000;
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private float trailSpeed = 1.2f;

        private static Sprite _whiteSprite;

        private Transform _barRoot;
        private SpriteRenderer _fill;
        private SpriteRenderer _trail;
        private float _trailValue = 1f;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health != null) health.HealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
        }

        private void Start()
        {
            Build();
            _trailValue = health != null ? health.NormalizedHealth : 1f;
            ApplyImmediate(_trailValue);
        }

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;

            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f));
            _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return _whiteSprite;
        }

        private SpriteRenderer CreateBar(string name, Color color, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(-size.x * 0.5f, 0f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder + order;
            return sr;
        }

        private void Build()
        {
            if (_barRoot != null) return;

            var root = new GameObject("EnemyHPBar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, offsetY, 0f);
            _barRoot = root.transform;

            var bg = CreateBar("bg", backgroundColor, 0, _barRoot);
            bg.transform.localScale = new Vector3(size.x, size.y, 1f);

            _trail = CreateBar("trail", trailColor, 1, _barRoot);
            _trail.transform.localScale = new Vector3(size.x, size.y * 0.8f, 1f);

            _fill = CreateBar("fill", fillColor, 2, _barRoot);
            _fill.transform.localScale = new Vector3(size.x, size.y * 0.8f, 1f);
        }

        private void OnHealthChanged(int current, int max)
        {
            if (hideWhenFull && _barRoot != null)
                _barRoot.gameObject.SetActive(max <= 0 || current < max);
        }

        private void ApplyImmediate(float value)
        {
            if (_fill != null) _fill.transform.localScale = new Vector3(size.x * Mathf.Clamp01(value), size.y * 0.8f, 1f);
            if (_trail != null) _trail.transform.localScale = new Vector3(size.x * Mathf.Clamp01(value), size.y * 0.8f, 1f);
        }

        private void LateUpdate()
        {
            if (_barRoot == null || health == null) return;

            float value = health.NormalizedHealth;

            if (_fill != null) _fill.transform.localScale = new Vector3(size.x * Mathf.Clamp01(value), size.y * 0.8f, 1f);

            if (_trailValue > value) _trailValue = Mathf.MoveTowards(_trailValue, value, trailSpeed * Time.deltaTime);
            else _trailValue = value;

            if (_trail != null) _trail.transform.localScale = new Vector3(size.x * Mathf.Clamp01(_trailValue), size.y * 0.8f, 1f);

            if (hideWhenFull) _barRoot.gameObject.SetActive(value < 0.999f);

            // 敌人朝向翻转时，把血条翻回来，保证掉血方向一致
            float sign = transform.lossyScale.x < 0f ? -1f : 1f;
            _barRoot.localScale = new Vector3(sign, 1f, 1f);
        }
    }
}
