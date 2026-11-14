using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 命中伤害数字（对象池版，避免每次命中都 new GameObject 造成卡顿）。
    /// 敌人受击 = 白色，玩家受击 = 红色；大小/颜色/动画由 DamageNumberConfig 调。
    /// </summary>
    public static class DamageNumber
    {
        private const string FontPath = "Fonts/hanatotyoutyo";

        private static Font _font;
        private static Material _fontMaterial;
        private static readonly List<Item> _pool = new List<Item>();

        // 可在运行时被 DamageNumberConfig 覆盖
        private static int _fontSize = 64;
        private static float _characterSize = 0.08f;
        private static float _enemyScale = 1f;
        private static float _playerScale = 1.1f;
        private static float _lifetime = 0.8f;
        private static float _riseSpeed = 2.2f;
        private static float _heightOffset = 0.4f;
        private static Color _enemyColor = Color.white;
        private static Color _playerColor = new Color(1f, 0.25f, 0.25f, 1f);

        /// <summary>启动时预热：加载字体 + 创建材质 + 生成一个隐藏的数字，避免第一次命中卡顿。</summary>
        public static void Warmup()
        {
            EnsureFont();
            var item = GetItem();
            if (item != null)
            {
                item.Play(new Vector3(0f, -9999f, 0f), 0, Color.clear, 1f);
                item.gameObject.SetActive(false);
            }
        }

        public static void ApplyConfig(DamageNumberConfig cfg)
        {
            if (cfg == null) return;

            _fontSize = cfg.fontSize;
            _characterSize = cfg.characterSize;
            _enemyScale = cfg.enemyScale;
            _playerScale = cfg.playerScale;
            _lifetime = cfg.lifetime;
            _riseSpeed = cfg.riseSpeed;
            _heightOffset = cfg.heightOffset;
            _enemyColor = cfg.enemyColor;
            _playerColor = cfg.playerColor;
        }

        public static void ShowEnemy(Vector3 worldPos, int value)
            => Show(worldPos, value, _enemyColor, _enemyScale);

        public static void ShowPlayer(Vector3 worldPos, int value)
            => Show(worldPos, value, _playerColor, _playerScale);

        public static void Show(Vector3 worldPos, int value, Color color, float scale = 1f)
        {
            if (value <= 0) return;

            EnsureFont();

            var item = GetItem();
            item.Play(worldPos + Vector3.up * _heightOffset, value, color, scale);
        }

        private static void EnsureFont()
        {
            if (_font != null) return;

            _font = Resources.Load<Font>(FontPath);
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", _fontSize);
            if (_font != null) _fontMaterial = _font.material;
        }

        private static Item GetItem()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf) return _pool[i];
            }

            var go = new GameObject("DamageNumber");
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = _font;
            mesh.fontSize = _fontSize;
            mesh.characterSize = _characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.text = "0";

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (_fontMaterial != null) renderer.sharedMaterial = _fontMaterial;
                renderer.sortingOrder = 32000;
            }

            var item = go.AddComponent<Item>();
            go.SetActive(false);
            _pool.Add(item);
            return item;
        }

        public class Item : MonoBehaviour
        {
            private TextMesh _mesh;
            private Color _color;
            private float _age;
            private float _scale = 1f;

            private void Awake()
            {
                _mesh = GetComponent<TextMesh>();
            }

            public void Play(Vector3 worldPos, int value, Color color, float scale)
            {
                transform.position = worldPos;
                transform.localScale = Vector3.one * scale;
                _color = color;
                _age = 0f;
                _scale = scale;

                if (_mesh != null)
                {
                    _mesh.text = value.ToString();
                    _mesh.color = color;
                    _mesh.fontSize = _fontSize;
                    _mesh.characterSize = _characterSize;
                }

                gameObject.SetActive(true);
            }

            private void Update()
            {
                float dt = Time.unscaledDeltaTime;
                _age += dt;

                transform.position += Vector3.up * _riseSpeed * dt;

                if (_mesh != null)
                {
                    float k = Mathf.Clamp01(_age / Mathf.Max(0.05f, _lifetime));
                    var c = _color;
                    c.a = 1f - k * k;
                    _mesh.color = c;
                }

                if (_age >= _lifetime) gameObject.SetActive(false);
            }
        }
    }
}
