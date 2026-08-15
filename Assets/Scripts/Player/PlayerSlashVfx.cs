using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    /// <summary>
    /// 程序化斩击（刀光）生成器：完全用代码复现参考美术的新月形，
    /// 不依赖已制作的预制体 / 网格。
    ///
    /// 镜像 <c>Assets/Material/SlashMaterial.mat</c> +
    /// <c>Shader "Shader Graphs/SlashShaderGraph"</c>：无光照、加法、双面弧。
    /// 可见条纹来自噪音/雾纹（<c>_Noise</c> / <c>_BrushTex</c>）+ HDR 色调，
    /// <b>TrailRGB1</b> 只做层遮罩；<b>mask</b> 渐变（Gradation_BtoW）做扫过。
    /// 新月轮廓 + 柔边来自生成的弧形网格。
    ///
    /// <see cref="PlayerMelee"/> 在未指定斩击预制体时以此作为运行时回退
    ///（玩家骨架在运行时组合，无序列化特效引用）。
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

        // 共享已制作资源 GUID（编辑器内解析）；否则用安全的程序化回退。
        private const string HazeGuid = "21289cd1fced7be4daee55a968efbf24"; // tex_vfx-ult_trail_haze
        private const string NoiseGuid = "8efed133e4f422240b95ae722414eea9"; // Noise1
        private const string StreakGuid = "147c29879c27b1008a1ac3f9dc9fc4a1"; // trail_002
        private const string GradationGuid = "835c19757c26aec4bbd12e2c7b116dc1"; // Gradation_BtoW
        private const string TrailRgbGuid = "94556fda5b4ed5c41be333d1d7368295"; // TrailRGB1 layer mask

        private const int ArcSegments = 48;
        private const int WidthRows = 6;

        private Material _sharedMaterial;
        private readonly Dictionary<SlashKind, Mesh> _meshCache = new Dictionary<SlashKind, Mesh>();

        /// <summary>
        /// 在世界姿势处生成一次性程序化斩击，按 <paramref name="facing"/> 镜像。
        /// 匹配 <see cref="PlayerMelee"/> 的预制体路径：未挂接、世界空间、自有生命周期。
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
            // 朝左时水平镜像弧线（XY 平面四边形）。
            t.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);

            var inst = go.AddComponent<SlashInstance>();
            inst.Play(mr, p.Tint, p.ScrollSpeed, lifetime > 0f ? lifetime : 0.35f);
            return go;
        }

        private static KindProfile GetProfile(SlashKind kind)
        {
            // 中性灰 HDR 色调（匹配 SlashMaterial 约 0.52 灰），自发光由材质驱动。
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
        /// 构建弯曲缎带（环扇区）。UV.x 沿弧尾→头（笔刷沿其滚动）；
        /// UV.y 跨刀刃宽度。顶点 alpha 烘焙新月轮廓：两端尖端与两侧径向边缘渐变到 0，留下明亮核心。
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

                // 尖端渐变：两端为 0，弧中部峰值（略偏头部）。
                float tipTaper = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.6f);
                // 刀刃向尖端也变细（把宽度拉向中半径）。
                float widthScale = Mathf.Lerp(0.12f, 1f, Mathf.Sin(Mathf.PI * t));

                for (int r = 0; r < rows; r++)
                {
                    float v = r / (float)WidthRows;
                    float radius = Mathf.Lerp(midR, Mathf.Lerp(innerR, outerR, v), widthScale);

                    int idx = c * rows + r;
                    verts[idx] = new Vector3(dir.x * radius, dir.y * radius, 0f);
                    uvs[idx] = new Vector2(t, v);
                    normals[idx] = Vector3.back;

                    // 宽度上的柔边（明亮核心，透明径向边缘）。
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

            Texture haze = LoadTexture(HazeGuid) ?? GenerateHazeTexture();
            Texture noise = LoadTexture(NoiseGuid) ?? haze;
            Texture streak = LoadTexture(StreakGuid) ?? haze;
            Texture mask = LoadTexture(GradationGuid) ?? GenerateGradientTexture();
            Texture trail = LoadTexture(TrailRgbGuid) ?? haze;

            // 主体是 trail_002 / Noise1，TrailRGB1 只做层遮罩。
            SetTexture(mat, "_MainTex", trail);
            SetTexture(mat, "_BrushTex", haze);
            SetTexture(mat, "_Noise", noise);
            SetTexture(mat, "_StreakTex", streak);
            SetTexture(mat, "Texture2D_4AF1F502", mask);
            SetFloat(mat, "_BrushStrength", 0.35f);
            SetFloat(mat, "_NoiseStrength", 1f);
            SetFloat(mat, "_Distortion", 0.06f);
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
        /// 雾纹笔刷的运行时回退：一叠柔和水平条纹（灰色），以便在无法加载制作贴图的构建中
        /// 斩击仍保持飘逸外观。
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
                    // 拉长（条纹状）fbm：横向高频，沿条纹低频。
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
        /// 形状遮罩的运行时回退（模仿 Gradation_BtoW）：沿 U 的平滑 0→1 坡度，
        /// 使斩击从透明尾部淡入到明亮前端。
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

        /// <summary>每实例驱动：快速显现、保持、然后淡出 — 外加轻微放大。</summary>
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
                // 前 20% 显现，随后缓出至无。
                float reveal = Mathf.Clamp01(t / 0.2f);
                float fade = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - 0.2f) / 0.8f));
                float a = reveal * fade;

                // 扩大剑气：消散时放大暂时关闭
                // transform.localScale *= 1f + 0.35f * Time.deltaTime;

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
