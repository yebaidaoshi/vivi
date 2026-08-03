using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    /// <summary>
    /// Procedural slash (刀光) generator that reproduces the reference-art crescent
    /// entirely in code, with no authored prefab / mesh dependency.
    ///
    /// It mirrors <c>Assets/Material/SlashMaterial.mat</c> +
    /// <c>Shader "Shader Graphs/SlashShaderGraph"</c>: an unlit, additive, two-sided arc.
    /// The visible streaks ARE <b>tex_vfx-ult_trail_haze</b> — it feeds the base brightness
    /// (<c>_MainTex</c>) plus a second scrolling copy (<c>_BrushTex</c>) for animated shimmer;
    /// a <b>mask</b> gradient (Gradation_BtoW) fades tail→head and an HDR emissive tint drives
    /// the glow. The crescent silhouette + soft edges come from a generated arc mesh whose
    /// vertex alpha tapers at the tips and across the blade width.
    ///
    /// <see cref="PlayerMelee"/> uses this as the runtime fallback when a slash prefab is
    /// not assigned (the player rig is composed at runtime with no serialized VFX refs).
    /// </summary>
    public class PlayerSlashVfx : MonoBehaviour
    {
        public enum SlashKind
        {
            Attack1,
            Attack2,
            Attack3,
            Slide,
            JumpUp,
            JumpDown,
            After
        }

        private struct KindProfile
        {
            public float StartDeg;
            public float SweepDeg;
            public float InnerRadius;
            public float OuterRadius;
            public float RotationDeg;
            public Color Tint;
            public float ScrollSpeed;
        }

        // Shared authored asset GUIDs (resolved in-editor); safe procedural fallbacks otherwise.
        private const string HazeGuid = "21289cd1fced7be4daee55a968efbf24"; // tex_vfx-ult_trail_haze
        private const string NoiseGuid = "8efed133e4f422240b95ae722414eea9"; // Noise1
        private const string GradationGuid = "835c19757c26aec4bbd12e2c7b116dc1"; // Gradation_BtoW

        private const int ArcSegments = 48;
        private const int WidthRows = 6;

        private Material _sharedMaterial;
        private readonly Dictionary<SlashKind, Mesh> _meshCache = new Dictionary<SlashKind, Mesh>();

        /// <summary>
        /// Spawn a one-shot procedural slash at a world pose, mirrored by <paramref name="facing"/>.
        /// Matches <see cref="PlayerMelee"/>'s prefab path: unparented, world-space, own lifetime.
        /// </summary>
        public GameObject Play(SlashKind kind, Vector3 worldPos, Quaternion worldRot, int facing,
            float lifetime)
        {
            KindProfile p = GetProfile(kind);

            var go = new GameObject("SlashVfx_" + kind);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            mf.sharedMesh = GetOrBuildMesh(kind, p);
            mr.sharedMaterial = GetOrBuildMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            Transform t = go.transform;
            t.position = worldPos;
            t.rotation = worldRot * Quaternion.Euler(0f, 0f, p.RotationDeg);
            // Mirror the arc horizontally when facing left (flat quad in XY).
            t.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);

            var inst = go.AddComponent<SlashInstance>();
            inst.Play(mr, p.Tint, p.ScrollSpeed, lifetime > 0f ? lifetime : 0.35f);
            return go;
        }

        private static KindProfile GetProfile(SlashKind kind)
        {
            // Neutral grey HDR tint (matches SlashMaterial's ~0.52 grey), emissive via the material.
            Color grey = new Color(0.72f, 0.74f, 0.82f, 1f);

            switch (kind)
            {
                case SlashKind.Attack2:
                    return new KindProfile
                    {
                        StartDeg = 210f,
                        SweepDeg = -230f,
                        InnerRadius = 0.35f,
                        OuterRadius = 1.5f,
                        RotationDeg = -20f,
                        Tint = grey,
                        ScrollSpeed = -0.6f
                    };
                case SlashKind.Attack3:
                    return new KindProfile
                    {
                        StartDeg = 150f,
                        SweepDeg = 250f,
                        InnerRadius = 0.4f,
                        OuterRadius = 1.75f,
                        RotationDeg = 0f,
                        Tint = new Color(0.85f, 0.86f, 0.95f, 1f),
                        ScrollSpeed = 0.8f
                    };
                case SlashKind.Slide:
                    return new KindProfile
                    {
                        StartDeg = 200f,
                        SweepDeg = -160f,
                        InnerRadius = 0.3f,
                        OuterRadius = 1.4f,
                        RotationDeg = -55f,
                        Tint = grey,
                        ScrollSpeed = 0.5f
                    };
                case SlashKind.JumpUp:
                    return new KindProfile
                    {
                        StartDeg = 300f,
                        SweepDeg = 220f,
                        InnerRadius = 0.35f,
                        OuterRadius = 1.5f,
                        RotationDeg = 35f,
                        Tint = grey,
                        ScrollSpeed = 0.6f
                    };
                case SlashKind.JumpDown:
                    return new KindProfile
                    {
                        StartDeg = 60f,
                        SweepDeg = 220f,
                        InnerRadius = 0.35f,
                        OuterRadius = 1.5f,
                        RotationDeg = -35f,
                        Tint = grey,
                        ScrollSpeed = 0.6f
                    };
                case SlashKind.After:
                    return new KindProfile
                    {
                        StartDeg = 160f,
                        SweepDeg = 200f,
                        InnerRadius = 0.45f,
                        OuterRadius = 1.35f,
                        RotationDeg = 0f,
                        Tint = new Color(0.7f, 0.72f, 0.8f, 0.7f),
                        ScrollSpeed = 1.0f
                    };
                default: // Attack1
                    return new KindProfile
                    {
                        StartDeg = 150f,
                        SweepDeg = 240f,
                        InnerRadius = 0.35f,
                        OuterRadius = 1.55f,
                        RotationDeg = 10f,
                        Tint = grey,
                        ScrollSpeed = 0.6f
                    };
            }
        }

        private Mesh GetOrBuildMesh(SlashKind kind, KindProfile p)
        {
            if (_meshCache.TryGetValue(kind, out Mesh cached) && cached != null)
            {
                return cached;
            }

            Mesh mesh = BuildArcMesh(p.InnerRadius, p.OuterRadius, p.StartDeg, p.SweepDeg);
            _meshCache[kind] = mesh;
            return mesh;
        }

        /// <summary>
        /// Build a curved ribbon (ring sector). UV.x runs tail→head along the arc (brush scrolls
        /// along it); UV.y runs across the blade width. Vertex alpha bakes the crescent silhouette:
        /// it tapers to 0 at both tips and toward the two radial edges, leaving a bright core.
        /// </summary>
        private static Mesh BuildArcMesh(float innerR, float outerR, float startDeg, float sweepDeg)
        {
            int cols = ArcSegments + 1;
            int rows = WidthRows + 1;
            int vCount = cols * rows;

            var verts = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var colors = new Color32[vCount];
            var normals = new Vector3[vCount];
            float midR = (innerR + outerR) * 0.5f;

            for (int c = 0; c < cols; c++)
            {
                float t = c / (float)ArcSegments;
                float ang = Mathf.Deg2Rad * Mathf.Lerp(startDeg, startDeg + sweepDeg, t);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

                // Tip taper: 0 at both ends, peaks mid-arc (slightly head-weighted).
                float tipTaper = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.6f);
                // Blade gets thinner toward the tips too (pull width toward the mid radius).
                float widthScale = Mathf.Lerp(0.12f, 1f, Mathf.Sin(Mathf.PI * t));

                for (int r = 0; r < rows; r++)
                {
                    float v = r / (float)WidthRows;
                    float radius = Mathf.Lerp(midR, Mathf.Lerp(innerR, outerR, v), widthScale);

                    int idx = c * rows + r;
                    verts[idx] = new Vector3(dir.x * radius, dir.y * radius, 0f);
                    uvs[idx] = new Vector2(t, v);
                    normals[idx] = Vector3.back;

                    // Soft edge across the width (bright core, transparent radial edges).
                    float widthProfile = Mathf.Sin(Mathf.PI * v);
                    byte a = (byte)Mathf.Clamp(tipTaper * widthProfile * 255f, 0f, 255f);
                    colors[idx] = new Color32(255, 255, 255, a);
                }
            }

            var tris = new int[ArcSegments * WidthRows * 6];
            int ti = 0;
            for (int c = 0; c < ArcSegments; c++)
            {
                for (int r = 0; r < WidthRows; r++)
                {
                    int i0 = c * rows + r;
                    int i1 = i0 + 1;
                    int i2 = (c + 1) * rows + r;
                    int i3 = i2 + 1;

                    tris[ti++] = i0;
                    tris[ti++] = i2;
                    tris[ti++] = i1;
                    tris[ti++] = i1;
                    tris[ti++] = i2;
                    tris[ti++] = i3;
                }
            }

            var mesh = new Mesh { name = "ProcSlashArc" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material GetOrBuildMaterial()
        {
            if (_sharedMaterial != null)
            {
                return _sharedMaterial;
            }

            Shader shader = Shader.Find("Shader Graphs/SlashShaderGraph")
                ?? Shader.Find("ErbGameArt/Particles/FadeSlash")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");

            var mat = new Material(shader) { name = "ProcSlashMaterial" };

            // The haze IS the visible slash: its fibers drive the base brightness (_MainTex,
            // the universal slot honored by every fallback shader too) and a second scrolling
            // copy in _BrushTex adds animated shimmer. Noise1 only perturbs; Gradation shapes.
            Texture haze = LoadTexture(HazeGuid) ?? GenerateHazeTexture();
            Texture noise = LoadTexture(NoiseGuid) ?? haze;
            Texture mask = LoadTexture(GradationGuid) ?? GenerateGradientTexture();

            // SlashShaderGraph slots (guarded so fallback shaders don't spam warnings).
            SetTexture(mat, "_MainTex", haze);
            SetTexture(mat, "_BrushTex", haze);
            SetTexture(mat, "_Noise", noise);
            SetTexture(mat, "Texture2D_4AF1F502", mask);
            SetFloat(mat, "_BrushStrength", 0.7f);
            SetFloat(mat, "_BrushSpeed", 0.4f);
            SetFloat(mat, "_EmissionStrength", 2.5f);
            SetFloat(mat, "_Alpha", 1f);
            SetColor(mat, "Color_39BB2441", new Color(0.72f, 0.74f, 0.82f, 1f));
            SetColor(mat, "_TintColor", new Color(0.72f, 0.74f, 0.82f, 1f));

            _sharedMaterial = mat;
            return mat;
        }

        private static void SetTexture(Material m, string prop, Texture tex)
        {
            if (tex != null && m.HasProperty(prop))
            {
                m.SetTexture(prop, tex);
            }
        }

        private static void SetFloat(Material m, string prop, float value)
        {
            if (m.HasProperty(prop))
            {
                m.SetFloat(prop, value);
            }
        }

        private static void SetColor(Material m, string prop, Color value)
        {
            if (m.HasProperty(prop))
            {
                m.SetColor(prop, value);
            }
        }

        private static Texture LoadTexture(string guid)
        {
#if UNITY_EDITOR
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (!string.IsNullOrEmpty(path))
			{
				return AssetDatabase.LoadAssetAtPath<Texture>(path);
			}
#endif
            return null;
        }

        /// <summary>
        /// Runtime fallback for the haze brush: a stack of soft horizontal streaks (grey) so the
        /// slash keeps its wispy look even in a build where the authored texture isn't loadable.
        /// </summary>
        private static Texture2D GenerateHazeTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    // Elongated (streaky) fbm: high frequency across, low along the streaks.
                    float n = Mathf.PerlinNoise(u * 24f, v * 4f) * 0.6f
                        + Mathf.PerlinNoise(u * 8f, v * 2f) * 0.4f;
                    byte c = (byte)Mathf.Clamp(n * 255f, 0f, 255f);
                    px[y * size + x] = new Color32(c, c, c, c);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Runtime fallback for the shape mask (mimics Gradation_BtoW): a smooth 0→1 ramp along
        /// U so the slash fades from a transparent tail to a bright leading head.
        /// </summary>
        private static Texture2D GenerateGradientTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, 1, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size];
            for (int x = 0; x < size; x++)
            {
                byte c = (byte)Mathf.Clamp(Mathf.SmoothStep(0f, 1f, x / (float)(size - 1)) * 255f, 0f, 255f);
                px[x] = new Color32(c, c, c, c);
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Per-instance driver: quick reveal, hold, then fade — plus a subtle grow.</summary>
        private sealed class SlashInstance : MonoBehaviour
        {
            private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
            private static readonly int ColorId = Shader.PropertyToID("Color_39BB2441");
            private static readonly int TintId = Shader.PropertyToID("_TintColor");
            private static readonly int BrushSpeedId = Shader.PropertyToID("_BrushSpeed");

            private MeshRenderer _renderer;
            private MaterialPropertyBlock _mpb;
            private Color _tint;
            private float _scrollSpeed;
            private float _life;
            private float _age;
            private bool _hasAlpha;
            private bool _hasColor;
            private bool _hasTint;
            private bool _hasBrushSpeed;

            public void Play(MeshRenderer renderer, Color tint, float scrollSpeed, float life)
            {
                _renderer = renderer;
                _tint = tint;
                _scrollSpeed = scrollSpeed;
                _life = Mathf.Max(life, 0.05f);
                _mpb = new MaterialPropertyBlock();

                Material shared = renderer.sharedMaterial;
                _hasAlpha = shared != null && shared.HasProperty(AlphaId);
                _hasColor = shared != null && shared.HasProperty(ColorId);
                _hasTint = shared != null && shared.HasProperty(TintId);
                _hasBrushSpeed = shared != null && shared.HasProperty(BrushSpeedId);

                Apply(0f);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float t = Mathf.Clamp01(_age / _life);
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                Apply(t);
            }

            private void Apply(float t)
            {
                // Reveal in the first 20%, then ease out to nothing.
                float reveal = Mathf.Clamp01(t / 0.2f);
                float fade = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - 0.2f) / 0.8f));
                float a = reveal * fade;

                // Grow slightly as it dissipates + drift the brush for a flowing streak.
                transform.localScale *= 1f + 0.35f * Time.deltaTime;

                _renderer.GetPropertyBlock(_mpb);
                if (_hasAlpha)
                {
                    _mpb.SetFloat(AlphaId, a);
                }

                if (_hasBrushSpeed)
                {
                    _mpb.SetFloat(BrushSpeedId, _scrollSpeed);
                }

                Color c = _tint;
                c.a = _tint.a * a;
                if (_hasColor)
                {
                    _mpb.SetColor(ColorId, c);
                }

                if (_hasTint)
                {
                    _mpb.SetColor(TintId, c);
                }

                _renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
