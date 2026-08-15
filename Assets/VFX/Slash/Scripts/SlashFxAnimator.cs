using UnityEngine;

namespace Vivi.Slash
{
    /// <summary>
    /// 复现原作 FadeSlash / ViviSlash 的播放曲线：
    /// Path 扫过（_Progress + _TailLength）、侧向/起笔透明度、噪声溶解边、整体淡出。
    /// </summary>
    public class SlashFxAnimator : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float duration = 0.42f;
        [SerializeField] private bool destroyOnEnd = true;

        [Header("Curves (0-1 lifetime)")]
        [SerializeField] private AnimationCurve progressCurve = new AnimationCurve(
            new Keyframe(0f, 0.08f, 0f, 4f),
            new Keyframe(0.28f, 1f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        [SerializeField] private AnimationCurve tailCurve = new AnimationCurve(
            new Keyframe(0f, 0.18f),
            new Keyframe(0.3f, 0.82f),
            new Keyframe(1f, 0.35f));

        [SerializeField] private AnimationCurve opacityCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 8f),
            new Keyframe(0.12f, 1f, 0f, 0f),
            new Keyframe(0.52f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -2.5f, 0f));

        [SerializeField] private AnimationCurve dissolveCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.48f, 0f),
            new Keyframe(1f, 1f));

        [SerializeField] private AnimationCurve scaleCurve = new AnimationCurve(
            new Keyframe(0f, 0.88f),
            new Keyframe(0.22f, 1.04f),
            new Keyframe(1f, 1.08f));

        [Header("Material defaults")]
        [SerializeField] private float scanSoftness = 0.055f;
        [SerializeField] private bool reverseDirection;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int TailId = Shader.PropertyToID("_TailLength");
        private static readonly int SoftId = Shader.PropertyToID("_ScanSoftness");
        private static readonly int ReverseId = Shader.PropertyToID("_ReverseDirection");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int PathId = Shader.PropertyToID("_PathSet0ifyouuseinPS");
        private static readonly int LengthId = Shader.PropertyToID("_LenghtSet1ifyouuseinPS");
        private static readonly int FinalOpacityId = Shader.PropertyToID("_Finalopacity");
        private float _age;
        private Vector3 _baseScale;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private SpriteRenderer[] _sprites;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _mpb = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = duration > 0.0001f ? _age / duration : 1f;
            if (t >= 1f)
            {
                Apply(1f);
                if (destroyOnEnd)
                {
                    Destroy(gameObject);
                }

                enabled = false;
                return;
            }

            Apply(t);
        }

        private void Apply(float t)
        {
            float progress = progressCurve.Evaluate(t);
            float tail = tailCurve.Evaluate(t);
            float opacity = Mathf.Clamp01(opacityCurve.Evaluate(t));
            float dissolve = Mathf.Clamp01(dissolveCurve.Evaluate(t));
            transform.localScale = _baseScale * scaleCurve.Evaluate(t);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_mpb);
                SetIfPresent(r, ProgressId, progress);
                SetIfPresent(r, TailId, tail);
                SetIfPresent(r, SoftId, scanSoftness);
                SetIfPresent(r, ReverseId, reverseDirection ? 1f : 0f);
                SetIfPresent(r, OpacityId, opacity);
                SetIfPresent(r, DissolveId, dissolve);
                SetIfPresent(r, AlphaId, opacity);
                SetIfPresent(r, PathId, progress);
                SetIfPresent(r, LengthId, tail);
                SetIfPresent(r, FinalOpacityId, opacity);
                r.SetPropertyBlock(_mpb);
            }

            for (int i = 0; i < _sprites.Length; i++)
            {
                SpriteRenderer sr = _sprites[i];
                if (sr == null)
                {
                    continue;
                }

                Color c = sr.color;
                c.a = opacity;
                sr.color = c;
            }
        }

        private void SetIfPresent(Renderer renderer, int id, float value)
        {
            Material shared = renderer.sharedMaterial;
            if (shared != null && shared.HasProperty(id))
            {
                _mpb.SetFloat(id, value);
            }
        }
    }
}
