using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Spine;
using Spine.Unity;
using SceneCamera;

// Spine 4.x 的 Spine 命名空间里也有 Color，这里明确指定用 Unity 的
using Color = UnityEngine.Color;

namespace Player
{
    public class PlayerEvade : MonoBehaviour
    {
        [Header("叠加剪影材质")]
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private int sortingOrderOffset = 1;

        [Header("取消窗口")]
        [SerializeField] private float cancelableAt = 47f / 60f;

        [Header("完美闪避（无敌帧内躲过敌人攻击判定）")]
        [Tooltip("完美闪避成功的震屏时长 / 强度")]
        [SerializeField] private float perfectShakeDuration = 0.16f;
        [SerializeField] private float perfectShakeStrength = 0.3f;
        [Tooltip("完美闪避成功后额外延长多少秒无敌（方便穿出攻击）")]
        [SerializeField] private float perfectExtraInvincible = 0.08f;
        [Tooltip("同一次闪避最短判定间隔，防止一个判定盒多次触发")]
        [SerializeField] private float perfectDodgeGuard = 0.15f;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;
        private PlayerHealth _health;

        private SkeletonRenderer _skeletonRenderer;
        private MeshFilter _mainMeshFilter;
        private MeshRenderer _mainMeshRenderer;

        private Transform _overlayLayer;
        private MeshFilter _overlayMeshFilter;
        private MeshRenderer _overlayMeshRenderer;

        private bool _active;
        private float _elapsed;
        private bool _invincibleApplied;
        private bool _overlayApplied;

        private float _invincibleBonus;
        private float _perfectGuardUntil;

        /// <summary>找到"身体"那块网格（取顶点最多的 MeshRenderer，Spine 有多个 Submesh 渲染器）。</summary>
        private void ResolveMainMesh()
        {
            // 已经找到有效网格就不再重复找
            if (_mainMeshFilter != null && _mainMeshFilter.sharedMesh != null && _mainMeshRenderer != null)
            {
                return;
            }

            MeshFilter bestFilter = null;
            MeshRenderer bestRenderer = null;
            var bestVerts = -1;

            var roots = new System.Collections.Generic.List<Transform> { transform, transform.root };

            foreach (var r in roots)
            {
                if (r == null) continue;

                foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;

                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    var verts = mf.sharedMesh.vertexCount;
                    if (verts > bestVerts)
                    {
                        bestVerts = verts;
                        bestFilter = mf;
                        bestRenderer = mr;
                    }
                }

                if (bestFilter != null) break;
            }

            if (bestFilter != null)
            {
                _mainMeshFilter = bestFilter;
                _mainMeshRenderer = bestRenderer;
                return;
            }

            // 最后兜底：重试一次 SkeletonRenderer
            var skel = GetComponentInParent<SkeletonRenderer>();
            if (skel == null) skel = GetComponentInChildren<SkeletonRenderer>(true);
            if (skel == null) return;

            var renderer = skel.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = skel.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null) return;

            _mainMeshRenderer = renderer;
            _mainMeshFilter = renderer.GetComponent<MeshFilter>();
        }

        /// <summary>完美闪避成功（参数 = 被躲过的攻击者）。</summary>
        public event System.Action<GameObject> OnPerfectDodge;

        /// <summary>累计完美闪避次数（调试/成就用）。</summary>
        public int PerfectDodgeCount { get; private set; }

        public bool IsActive => _active;
        public float Elapsed => _elapsed;
        public float CancelableAt => cancelableAt;
        public bool CanCancel => _active && _elapsed >= cancelableAt;

        /// <summary>现在是不是闪避的无敌帧窗口（原版判定"完美闪避"就靠这个）。</summary>
        public bool IsInvincibleWindow => _active && _invincibleApplied;

        public void Init(PlayerContext ctx, PlayerHealth health)
        {
            ctx.Bind(out _motor, out _anim, out _audio, out _settings);
            _health = health;
            _skeletonRenderer = GetComponent<SkeletonRenderer>();
            _mainMeshFilter = GetComponent<MeshFilter>();
            _mainMeshRenderer = GetComponent<MeshRenderer>();

            // Spine 的 MeshRenderer 一般在子物体上，根节点拿不到就往下找一层
            if (_mainMeshRenderer == null)
            {
                _mainMeshRenderer = GetComponentInChildren<MeshRenderer>();

                if (_mainMeshRenderer != null)
                {
                    _mainMeshFilter = _mainMeshRenderer.GetComponent<MeshFilter>();
                }
            }

            EnsureOverlayLayer();
        }

        private void EnsureOverlayLayer()
        {
            if (overlayMaterial == null) return;

            var existing = transform.Find("EvadeOverlay");
            if (existing != null)
            {
                _overlayLayer = existing;
            }
            else
            {
                var go = new GameObject("EvadeOverlay");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _overlayLayer = go.transform;
            }

            // 挂在真正的 Spine 渲染节点下，否则 overlay 网格会出现在角色根节点的位置（看起来"没有剪影"）
            var overlayParent = _mainMeshFilter != null ? _mainMeshFilter.transform : transform;

            if (_overlayLayer.parent != overlayParent)
            {
                _overlayLayer.SetParent(overlayParent, false);
            }

            _overlayLayer.localPosition = Vector3.zero;
            _overlayLayer.localRotation = Quaternion.identity;
            _overlayLayer.localScale = Vector3.one;

            _overlayMeshFilter = _overlayLayer.GetComponent<MeshFilter>();
            if (_overlayMeshFilter == null)
                _overlayMeshFilter = _overlayLayer.gameObject.AddComponent<MeshFilter>();

            _overlayMeshRenderer = _overlayLayer.GetComponent<MeshRenderer>();
            if (_overlayMeshRenderer == null)
                _overlayMeshRenderer = _overlayLayer.gameObject.AddComponent<MeshRenderer>();

            if (_mainMeshRenderer != null)
            {
                _overlayMeshRenderer.sortingLayerID = _mainMeshRenderer.sortingLayerID;
                _overlayMeshRenderer.sortingOrder = _mainMeshRenderer.sortingOrder + sortingOrderOffset;
            }

            _overlayMeshRenderer.sharedMaterial = overlayMaterial;
            _overlayMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayMeshRenderer.receiveShadows = false;
            _overlayMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _overlayMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _overlayLayer.gameObject.SetActive(false);
        }

        /// <summary>
        /// 第三段挥砍的特效入口（PlayerMelee 现有调用点不用改）：默认放出 3~5 张纯色全身残影。
        /// 想同时保留旧的蓝色描边：勾上 outlineOnFlashSilhouette。
        /// </summary>
        public void FlashSilhouette(float seconds = 0.12f)
        {
            if (trailOnFlashSilhouette)
            {
                PlayAfterimageTrail();
            }

            if (!outlineOnFlashSilhouette)
            {
                return;
            }

            ResolveMainMesh();
            EnsureOverlayLayer();

            if (_overlayLayer == null || _overlayMeshRenderer == null)
            {
                return;
            }

            _overlayLayer.gameObject.SetActive(true);
            _overlayApplied = true;

            CancelInvoke(nameof(HideSilhouette));
            Invoke(nameof(HideSilhouette), Mathf.Max(0.02f, seconds));
        }

        // ───────────────────────── 残影：网格快照 ─────────────────────────
        //
        // 做法：不克隆 Spine 对象（克隆会连带复制 Animator / 玩家脚本，也很难把 Mecanim 姿势原样定格），
        // 而是在生成残影的那一帧，把玩家身上所有"有顶点的 MeshRenderer"
        // （主网格 + SkeletonRenderSeparator 的各个 Submesh 部件）的网格原样复制一份，
        // 换成 Vivi/SpineSilhouette 材质（只取图集 alpha，输出纯色），再按存活时间淡出。
        // 复制出来的网格本身就是"定格姿势"，不需要 Animator.speed = 0，也不会被 SkeletonMecanim 覆盖材质。

        public const string AfterimageShaderName = "Vivi/SpineSilhouette";

        [Header("残影（第三段挥砍）")]
        [Tooltip("残影剪影 Shader（Vivi/SpineSilhouette）。编辑器里留空会自动查找；打包请保持这个引用，否则 Shader 可能被裁掉。")]
        [SerializeField] private Shader afterimageShader;
        [Tooltip("残影颜色（只用 RGB，透明度由 ghostAlpha 决定）")]
        [SerializeField] private Color afterimageColor = new Color(0.55f, 0.82f, 1f, 1f);
        [Tooltip("残影刚生成时的透明度（之后线性淡出到 0）")]
        [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.5f;
        [Tooltip("一次挥砍放几张残影（建议 3~5）")]
        [SerializeField] private int afterimageCount = 4;
        [Tooltip("相邻两张残影的间隔（秒）")]
        [SerializeField] private float afterimageInterval = 0.045f;
        [Tooltip("第一张残影的延迟（秒）：给 Animator 一点时间切到挥砍姿势")]
        [SerializeField] private float afterimageFirstDelay = 0.03f;
        [Tooltip("单张残影从出现到完全消失的时间（秒）")]
        [SerializeField] private float afterimageLifetime = 0.3f;
        [Tooltip("同时存在的残影上限（超过就销毁最旧的）")]
        [SerializeField] private int maxAfterimages = 24;

        [Header("完美闪避残影（独立参数，不影响第三段攻击）")]
        [SerializeField] private Color dodgeAfterimageColor = new Color(0.55f, 0.82f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float dodgeGhostAlpha = 0.5f;
        [SerializeField] private int dodgeAfterimageCount = 4;
        [SerializeField] private float dodgeAfterimageInterval = 0.045f;
        [SerializeField] private float dodgeAfterimageLife = 0.3f;
        [Tooltip("闪避残影的第一张延迟多少秒（想晚几帧出就调大，0.05 ≈ 3 帧）")]
        [SerializeField] private float dodgeAfterimageDelay = 0.06f;
        [Tooltip("true = 整张残影是一块纯色剪影（用模板缓冲去掉身体部件叠加处的颜色加深，硬边）；false = 普通 alpha 叠加（软边，部件重叠处更实）")]
        [SerializeField] private bool flatSilhouette = true;
        [Tooltip("玩家自身节点下找不到网格时，到根节点下按距离兜底搜索的半径")]
        [SerializeField] private float afterimageSearchRadius = 4f;
        [Tooltip("FlashSilhouette（第三段挥砍）时是否放残影")]
        [SerializeField] private bool trailOnFlashSilhouette = true;
        [Tooltip("FlashSilhouette 时是否同时保留旧的蓝色描边")]
        [SerializeField] private bool outlineOnFlashSilhouette;

        private sealed class AfterimageGhost
        {
            public GameObject Root;
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public readonly List<Material> Materials = new List<Material>();
            public float Age;
            public float Life;
            public Color Tint = Color.white;
            public float Alpha = 0.5f;
        }

        private const int StencilBase = 32;
        private const int StencilRange = 64;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int FlatId = Shader.PropertyToID("_Flat");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
        private static readonly int StencilCompId = Shader.PropertyToID("_StencilComp");

        private readonly List<AfterimageGhost> _ghosts = new List<AfterimageGhost>();
        private readonly List<MeshRenderer> _ghostSources = new List<MeshRenderer>();
        private Transform _ghostContainer;
        private int _ghostSerial;
        private int _trailRemaining;
        private float _trailTimer;
        private float _trailInterval;
        private float _trailLife;
        private Color _trailTint = Color.white;
        private float _trailAlpha = 0.5f;
        private bool _ghostDiagLogged;
        private bool _ghostShaderWarned;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (afterimageShader == null)
            {
                afterimageShader = Shader.Find(AfterimageShaderName);
            }
        }
#endif

        /// <summary>放一串残影（默认 afterimageCount 张，间隔 afterimageInterval）。</summary>
        public void PlayAfterimageTrail(int count = -1, float interval = -1f, float life = -1f, Color? tint = null, float alpha = -1f, float firstDelay = -1f)
        {
            // 已经在放的时候只补数量、不重置计时：就算调用方每帧都调，也能照常按间隔出残影
            if (_trailRemaining <= 0)
            {
                _trailTimer = Mathf.Max(0f, firstDelay >= 0f ? firstDelay : afterimageFirstDelay);
            }

            _trailRemaining = count > 0 ? count : Mathf.Max(1, afterimageCount);
            _trailInterval = interval > 0f ? interval : Mathf.Max(0.005f, afterimageInterval);
            _trailLife = life > 0f ? life : afterimageLifetime;
            _trailTint = tint ?? afterimageColor;
            _trailAlpha = alpha >= 0f ? alpha : ghostAlpha;
        }

        /// <summary>立刻放一张残影（旧接口，保持兼容）。</summary>
        public void SpawnAfterimage(float life = 0.22f)
        {
            CaptureAfterimage(life);
        }

        /// <summary>完美闪避的残影：和第三段攻击走完全同一条路径（FlashSilhouette → PlayAfterimageTrail）。</summary>
        public void SpawnDodgeAfterimages()
        {
            PlayAfterimageTrail(dodgeAfterimageCount, dodgeAfterimageInterval,
                dodgeAfterimageLife, dodgeAfterimageColor, dodgeGhostAlpha, dodgeAfterimageDelay);
        }

        /// <summary>清掉所有残影并停止正在放的残影序列。</summary>
        public void ClearAfterimages()
        {
            _trailRemaining = 0;

            for (int i = 0; i < _ghosts.Count; i++)
            {
                DestroyGhost(_ghosts[i]);
            }

            _ghosts.Clear();
        }

        private Shader ResolveAfterimageShader()
        {
            if (afterimageShader == null)
            {
                afterimageShader = Shader.Find(AfterimageShaderName);
            }

            if (afterimageShader == null && !_ghostShaderWarned)
            {
                _ghostShaderWarned = true;
                Debug.LogError($"[Afterimage] 找不到 Shader '{AfterimageShaderName}'：请把 SpineSilhouette.shader 放进工程，并把它拖到 PlayerEvade 的 Afterimage Shader 上。", this);
            }

            return afterimageShader;
        }

        /// <summary>收集要做成残影的网格：先找玩家自己节点下的，找不到再到根节点下按距离兜底。</summary>
        private void CollectAfterimageSources()
        {
            _ghostSources.Clear();
            AddAfterimageSources(transform, false);

            if (_ghostSources.Count == 0 && transform.root != transform)
            {
                AddAfterimageSources(transform.root, true);
            }
        }

        private void AddAfterimageSources(Transform root, bool limitByDistance)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(false);
            var center = transform.position;

            for (int i = 0; i < renderers.Length; i++)
            {
                var mr = renderers[i];
                if (mr == null || !mr.enabled) continue;
                if (mr == _overlayMeshRenderer) continue;
                if (_ghostContainer != null && mr.transform.IsChildOf(_ghostContainer)) continue;
                if (_ghostSources.Contains(mr)) continue;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null) continue;

                var mesh = mf.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) continue;
                if (mr.sharedMaterials.Length == 0) continue;

                if (limitByDistance && Vector3.Distance(mr.bounds.center, center) > afterimageSearchRadius) continue;

                _ghostSources.Add(mr);
            }
        }

        private void EnsureGhostContainer()
        {
            if (_ghostContainer != null) return;

            var go = new GameObject("AfterimageRoot");
            _ghostContainer = go.transform;
        }

        /// <summary>把当前这一帧的玩家外观拍成一张残影。成功返回 true。</summary>
        private bool CaptureAfterimage(float life)
        {
            return CaptureAfterimage(life, afterimageColor, ghostAlpha);
        }

        private bool CaptureAfterimage(float life, Color tint, float alpha)
        {
            var shader = ResolveAfterimageShader();
            if (shader == null) return false;

            CollectAfterimageSources();
            if (_ghostSources.Count == 0)
            {
                LogAfterimageDiag();
                return false;
            }

            // 残影整体只排到"本体最靠后的部件"后面一格（按排序层分别算）。
            // 残影是纯色剪影，不需要保留部件之间的前后顺序；这样也不容易掉到背景 / 场景装饰后面。
            var minOrderByLayer = new Dictionary<int, int>();
            for (int i = 0; i < _ghostSources.Count; i++)
            {
                int layerId = _ghostSources[i].sortingLayerID;
                int order = _ghostSources[i].sortingOrder;
                if (!minOrderByLayer.TryGetValue(layerId, out var currentMin) || order < currentMin)
                {
                    minOrderByLayer[layerId] = order;
                }
            }

            EnsureGhostContainer();

            var ghost = new AfterimageGhost
            {
                Life = Mathf.Max(0.02f, life),
                Tint = tint,
                Alpha = Mathf.Clamp01(alpha)
            };
            ghost.Root = new GameObject("Afterimage");
            ghost.Root.transform.SetParent(_ghostContainer, false);

            // 每张残影一个模板值：同一张里的部件互相挡（不叠色），不同残影之间互不挡
            int stencilRef = StencilBase + (_ghostSerial++ % StencilRange);
            var materialByTexture = new Dictionary<int, Material>();

            for (int i = 0; i < _ghostSources.Count; i++)
            {
                var src = _ghostSources[i];
                var srcMesh = src.GetComponent<MeshFilter>().sharedMesh;
                var srcMaterials = src.sharedMaterials;

                var part = new GameObject("Part");
                part.layer = src.gameObject.layer;

                var t = part.transform;
                t.SetParent(ghost.Root.transform, false);

                // 容器是单位变换，所以这里的 local 就是 world；lossyScale 带着朝向翻转（负 X 缩放）
                var st = src.transform;
                t.SetPositionAndRotation(st.position, st.rotation);
                t.localScale = st.lossyScale;

                // 复制一份网格数据（Spine 的网格是双缓冲，会被它自己改写，不能直接引用）
                var mesh = Instantiate(srcMesh);
                mesh.name = srcMesh.name + "_Afterimage";
                ghost.Meshes.Add(mesh);

                part.AddComponent<MeshFilter>().sharedMesh = mesh;

                var mr = part.AddComponent<MeshRenderer>();
                var ghostMaterials = new Material[srcMaterials.Length];
                for (int m = 0; m < srcMaterials.Length; m++)
                {
                    ghostMaterials[m] = GetOrCreateSilhouetteMaterial(shader, srcMaterials[m], stencilRef, materialByTexture, ghost);
                }

                mr.sharedMaterials = ghostMaterials;
                mr.sortingLayerID = src.sortingLayerID;
                mr.sortingOrder = minOrderByLayer[src.sortingLayerID] - 1;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            ApplyGhostAlpha(ghost, 1f);
            _ghosts.Add(ghost);
            TrimGhosts();
            return true;
        }

        private Material GetOrCreateSilhouetteMaterial(Shader shader, Material source, int stencilRef,
            Dictionary<int, Material> cache, AfterimageGhost ghost)
        {
            // 取原图集材质的贴图（只用它的 alpha 来抠形状）
            UnityEngine.Texture texture = null;
            if (source != null)
            {
                if (source.HasProperty(MainTexId)) texture = source.GetTexture(MainTexId);
                else if (source.HasProperty(BaseMapId)) texture = source.GetTexture(BaseMapId);
            }

            int key = texture != null ? texture.GetInstanceID() : 0;
            if (cache.TryGetValue(key, out var existing)) return existing;

            var material = new Material(shader)
            {
                name = "AfterimageSilhouette",
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (texture != null) material.SetTexture(MainTexId, texture);
            material.SetFloat(FlatId, flatSilhouette ? 1f : 0f);
            material.SetFloat(CutoffId, flatSilhouette ? 0.5f : 0.05f);
            material.SetFloat(StencilRefId, stencilRef);
            material.SetFloat(StencilCompId, flatSilhouette ? (float)CompareFunction.NotEqual : (float)CompareFunction.Always);

            cache[key] = material;
            ghost.Materials.Add(material);
            return material;
        }

        private void ApplyGhostAlpha(AfterimageGhost ghost, float fade)
        {
            var c = ghost.Tint;
            c.a = Mathf.Clamp01(ghost.Alpha) * Mathf.Clamp01(fade);

            for (int i = 0; i < ghost.Materials.Count; i++)
            {
                if (ghost.Materials[i] != null)
                {
                    ghost.Materials[i].SetColor(ColorId, c);
                }
            }
        }

        private void TickAfterimages(float dt)
        {
            if (_trailRemaining > 0)
            {
                _trailTimer -= dt;
                if (_trailTimer <= 0f)
                {
                    CaptureAfterimage(_trailLife, _trailTint, _trailAlpha);
                    _trailRemaining--;
                    _trailTimer += _trailInterval;
                }
            }

            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = _ghosts[i];
                ghost.Age += dt;

                if (ghost.Root == null || ghost.Age >= ghost.Life)
                {
                    DestroyGhost(ghost);
                    _ghosts.RemoveAt(i);
                    continue;
                }

                ApplyGhostAlpha(ghost, 1f - ghost.Age / ghost.Life);
            }
        }

        private void TrimGhosts()
        {
            while (_ghosts.Count > Mathf.Max(1, maxAfterimages))
            {
                DestroyGhost(_ghosts[0]);
                _ghosts.RemoveAt(0);
            }
        }

        private static void DestroyGhost(AfterimageGhost ghost)
        {
            if (ghost == null) return;

            if (ghost.Root != null) Destroy(ghost.Root);

            for (int i = 0; i < ghost.Meshes.Count; i++)
            {
                if (ghost.Meshes[i] != null) Destroy(ghost.Meshes[i]);
            }

            for (int i = 0; i < ghost.Materials.Count; i++)
            {
                if (ghost.Materials[i] != null) Destroy(ghost.Materials[i]);
            }

            ghost.Meshes.Clear();
            ghost.Materials.Clear();
        }

        /// <summary>一次性诊断：列出玩家节点下所有 MeshRenderer 的顶点数，方便排查"为什么没有残影"。</summary>
        private void LogAfterimageDiag()
        {
            if (_ghostDiagLogged) return;
            _ghostDiagLogged = true;

        }

        private void HideSilhouette()
        {
            _overlayApplied = false;

            if (_overlayLayer != null)
            {
                _overlayLayer.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            TickAfterimages(Time.deltaTime);

            if (!_overlayApplied) return;
            if (_mainMeshFilter == null || _overlayMeshFilter == null) return;
            if (_overlayMeshFilter.sharedMesh != _mainMeshFilter.sharedMesh)
                _overlayMeshFilter.sharedMesh = _mainMeshFilter.sharedMesh;
        }

        public bool TryGetThreat(out Transform attacker)
        {
            attacker = null;
            if (_settings == null) return false;

            var hits = Physics2D.OverlapCircleAll(
                transform.position, _settings.evadeDetectRadius, ~0);

            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var combat = hit.GetComponentInParent<Enemy.EnemyCombat>();
                if (combat != null && combat.IsAttackInWindup)
                {
                    attacker = combat.transform;
                    return true;
                }
            }
            return false;
        }

        public bool HasThreatInWindup() => TryGetThreat(out _);

        /// <summary>
        /// 完美闪避：闪避的无敌帧窗口里被敌人攻击判定扫到时，由 EnemyCombat 调用。
        /// 原版的判定就是"闪避无敌帧躲过正在生效的攻击判定"，反馈 = 拖影 + 震屏 + 敌人攻击落空。
        /// </summary>
        public void NotifyPerfectDodge(GameObject attacker)
        {
            SpawnDodgeAfterimages();

            if (!IsInvincibleWindow) return;
            if (Time.time < _perfectGuardUntil) return;

            _perfectGuardUntil = Time.time + Mathf.Max(0.05f, perfectDodgeGuard);
            PerfectDodgeCount++;

            _invincibleBonus += Mathf.Max(0f, perfectExtraInvincible);

            var cam = CameraDirector.Instance;
            if (cam != null) cam.Shake(perfectShakeDuration, perfectShakeStrength);

            _audio?.PlayBackStep();

            OnPerfectDodge?.Invoke(attacker);
        }

        public bool TryStart(int facing)
        {
            if (_active) return false;
            if (_motor == null || !_motor.IsGrounded) return false;

            _active = true;
            _elapsed = 0f;
            _invincibleApplied = false;
            _overlayApplied = false;
            _invincibleBonus = 0f;

            _anim.ForcePlay(PlayerAnimDriver.States.JumpEvade);

            // 闪避起手就铺残影（用闪避自己那套参数，和第三段攻击分开）
            SpawnDodgeAfterimages();

            _motor.ForceFacing(facing);
            _motor.SetVelocity(new Vector2(facing * _settings.evadeForceX, 0f));
            _motor.AddForce(new Vector2(0f, _settings.evadeForceY), ForceMode2D.Impulse);

            _audio?.PlayJump();
            return true;
        }

        public void Tick(float dt)
        {
            if (!_active || _settings == null) return;

            _elapsed += dt;

            bool inWindow = _elapsed >= _settings.evadeInvincibleStart
                         && _elapsed <= _settings.evadeInvincibleEnd + _invincibleBonus;

            if (inWindow != _invincibleApplied)
            {
                _invincibleApplied = inWindow;
                _health?.SetEvadeInvincible(inWindow);
            }

            if (inWindow != _overlayApplied)
            {
                _overlayApplied = inWindow;
                SetOverlay(inWindow);
            }

            if (_elapsed >= _settings.evadeDuration)
                End();
        }

        private void SetOverlay(bool on)
        {
            if (_overlayLayer == null) return;

            _overlayLayer.gameObject.SetActive(on);

            if (on && _mainMeshFilter != null && _overlayMeshFilter != null)
                _overlayMeshFilter.sharedMesh = _mainMeshFilter.sharedMesh;
        }

        public void End()
        {
            if (!_active) return;
            _active = false;

            _health?.SetEvadeInvincible(false);
            SetOverlay(false);
            _invincibleApplied = false;
            _overlayApplied = false;
        }

        private void OnDisable()
        {
            End();
            ClearAfterimages();
        }

        private void OnDestroy()
        {
            ClearAfterimages();

            if (_ghostContainer != null)
            {
                Destroy(_ghostContainer.gameObject);
            }
        }
    }
}
