using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Player;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Rendering;

/// <summary>
/// 游戏内实时 Live2D：把 Vivi 的 Live2D 立绘放到屏幕一角，并跟着玩家实时变化。
///
///   · 站位/大小：每帧按相机正交尺寸换算，跟随相机、随镜头缩放保持屏占比
///   · 血量      ：按血量切 HP_100 / HP_075 / HP_050 / HP_025 / HP_001 / HP_000 循环（原版就是这套身体待机）
///   · 受击      ：Damage1 / Damage2 交替，播完回到当前血量的循环
///   · 攻击/闪避/滞空/瞄准/蓄力/被束缚/回血/死亡：各自对应一个动作
///   · 站久了    ：随机播一个待机小动作
///
/// 由 Live2DHudBootstrap 在进入有玩家的场景时自动生成，不需要手动摆场景。
/// </summary>
[DisallowMultipleComponent]
public class Live2DRealtimeHud : MonoBehaviour
{
    /// <summary>游玩时专用的 HUD 预制体放在这个 Resources 路径下（可选）。</summary>
    public const string PrefabResourcePath = "Live2D/Live2DHud";

    public Live2DHudConfig config;

    // ---- 模型 ----
    private GameObject _modelInstance;
    private Transform _modelRoot;
    private Live2DActor _actor;
    private CubismModel _model;
    private CubismRenderController _render;
    private CubismMotionController _motion;
    private Camera _hudCamera;

    // ---- 显示（渲染到 RenderTexture → UI 上的 RawImage，透明背景无黑边） ----
    private Canvas _canvas;
    private RawImage _rawImage;
    private RenderTexture _renderTexture;
    private int _rtWidth;
    private int _rtHeight;
    private Color _tint = Color.white;

    // ---- 玩家 ----
    private PlayerController _controller;
    private PlayerHealth _health;
    private PlayerMelee _melee;
    private PlayerEvade _evade;
    private PlayerJump _jump;
    private PlayerGun _gun;
    private PlayerMagic _magic;
    private PlayerBinding _binding;

    // ---- 模型本地包围盒（用 drawable 顶点算，打包后也有效） ----
    private Vector3 _boundsMin;
    private Vector3 _boundsMax;
    private bool _boundsReady;

    // ---- 状态 ----
    private enum HudState { None, Idle, Damage, Attack, Evade, Air, Aim, Magic, Bound, Heal, Dead, Override }

    private HudState _state = HudState.None;
    private string _currentMotion = "";
    private int _hpBucket = int.MinValue;
    private int _lastHp = -1;
    private bool _pendingDamage;
    private bool _alternateDamage;
    private float _lastDamageTime = -100f;
    private float _lastHealTime = -100f;
    private float _nextFlavorTime;
    private float _flavorUntil;
    private float _overrideUntil;
    private string _overrideMotion = "";
    private float _fade;
    private float _findTimer;

    private bool _debugVisible;
    private bool _missingWarned;
    private bool _built;
    private int _damageEvents;
    private float _spawnTime;
    private bool _selfHealed;
    private bool _warnedNoLayer;

    private static readonly string[] Dummy = new string[0];

    // =====================================================================
    // 生成
    // =====================================================================

    /// <summary>
    /// 建一个实时 Live2D HUD。
    /// 优先用 Resources/Live2D/Live2DHud.prefab（游玩时专用，可在 Inspector 里调）；
    /// 没有预制体就按同样的结构在代码里搭一套。
    /// </summary>
    public static Live2DRealtimeHud Spawn(Live2DHudConfig cfg)
    {
        if (cfg == null)
        {
            Debug.LogWarning("[Live2D HUD] 配置为空，跳过生成。");
            return null;
        }

        if (cfg.modelPrefab == null)
        {
            Debug.LogWarning("[Live2D HUD] 配置里没有填 modelPrefab，跳过生成。");
            return null;
        }

        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        GameObject host;
        Live2DRealtimeHud hud;

        if (prefab != null)
        {
            host = Instantiate(prefab);
            host.name = "[Live2D HUD]";

            hud = host.GetComponent<Live2DRealtimeHud>();

            if (hud == null)
            {
                hud = host.AddComponent<Live2DRealtimeHud>();
            }
        }
        else
        {
            host = new GameObject("[Live2D HUD]");
            hud = host.AddComponent<Live2DRealtimeHud>();
        }

        hud.config = cfg;

        if (!hud.Build())
        {
            Destroy(host);
            return null;
        }

        return hud;
    }

    private bool Build()
    {
        try
        {
            _modelInstance = Instantiate(config.modelPrefab);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Live2D HUD] 实例化 modelPrefab 失败（引用可能丢了）：{e.Message}");
            return false;
        }

        _modelInstance.name = "Vivi_Live2D_Realtime";

        _modelRoot = _modelInstance.transform;
        _modelRoot.localPosition = Vector3.zero;
        _modelRoot.localRotation = Quaternion.identity;
        _modelRoot.localScale = Vector3.one;

        _model = _modelInstance.GetComponent<CubismModel>();
        _render = _modelInstance.GetComponent<CubismRenderController>();
        _motion = _modelInstance.GetComponent<CubismMotionController>();
        _actor = _modelInstance.GetComponent<Live2DActor>();

        if (_model == null || _motion == null)
        {
            Debug.LogError("[Live2D HUD] prefab 上缺 CubismModel / CubismMotionController，无法生成。");
            return false;
        }

        // 相机 + UI（预制体里已经摆好就直接用，没有就现搭）
        BuildRig();

        _modelRoot.SetParent(_hudCamera.transform, false);
        _modelRoot.localPosition = Vector3.zero;
        SetLayerRecursively(_modelInstance, LayerForHud());

        // 动作诊断脚本是给对话场景排查用的，HUD 里静音，免得每次进游戏刷一屏
        var diagnostics = _modelInstance.GetComponent<Live2DMotionDiagnostics>();
        if (diagnostics != null)
        {
            diagnostics.runOnStart = false;
        }

        // ★ 关键：必须在 Live2DActor.Start 之前把这些开关关掉
        if (_actor != null)
        {
            _actor.actorId = "vivi_hud";
            _actor.hideOnStart = false;                 // 不要场外滑入
            _actor.manageTransformAndFade = false;      // 位置 / 透明度由本脚本控制
            _actor.debugMotion = false;                 // 不要每次播放都刷日志
            _actor.RegisterClips(config.motionClips);   // 把整套动作登记进来
        }
        else
        {
            Debug.LogWarning("[Live2D HUD] prefab 上没有 Live2DActor，动作会没法播。");
        }

        // 不再用 Cubism 的整体 Opacity（每个 drawable 各乘一遍，重叠部位会露出"透底"）。
        // 淡入淡出 / 死亡变暗一律改在 UI 的 RawImage 上做，整体一致。
        if (_render != null)
        {
            _render.Opacity = 1f;
        }

        _missingWarned = false;
        _built = true;
        _spawnTime = Time.time;

        StartCoroutine(MeasureBoundsRoutine());

        return true;
    }

    /// <summary>
    /// 搭/复用"相机 + Canvas + RawImage"这一套。编辑器生成预制体时也会调用。
    /// </summary>
    public void BuildRig()
    {
        if (config == null)
        {
            return;
        }

        if (_hudCamera == null)
        {
            _hudCamera = GetComponentInChildren<Camera>(true);
        }

        if (_hudCamera == null)
        {
            _hudCamera = CreateHudCamera();
        }

        if (_rawImage == null)
        {
            _rawImage = GetComponentInChildren<RawImage>(true);
        }

        if (_rawImage == null)
        {
            _rawImage = CreateRawImage();
        }

        _canvas = _rawImage != null ? _rawImage.GetComponentInParent<Canvas>() : null;

        if (_canvas != null)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = config.canvasSortingOrder;
        }

        ApplyHudCameraSettings(_hudCamera);
        EnsureRenderTexture(Mathf.RoundToInt(Screen.width * config.bustSize.x),
                            Mathf.RoundToInt(Screen.height * config.bustSize.y));
    }

    private int LayerForHud()
    {
        var layer = LayerMask.NameToLayer(config.hudCameraLayerName);

        if (layer >= 0)
        {
            return layer;
        }

        if (!_warnedNoLayer)
        {
            _warnedNoLayer = true;
            Debug.LogWarning($"[Live2D HUD] 找不到层 \"{config.hudCameraLayerName}\"，改用 Default 层。");
        }

        return 0;
    }

    private Camera CreateHudCamera()
    {
        var go = new GameObject("HUD Camera", typeof(Camera));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var cam = go.GetComponent<Camera>();
        ApplyHudCameraSettings(cam);
        return cam;
    }

    private void ApplyHudCameraSettings(Camera cam)
    {
        if (cam == null)
        {
            return;
        }

        cam.orthographic = true;
        cam.orthographicSize = 1f;
        // 渲染到 RenderTexture，背景用全透明 —— 这样 UI 上只有立绘本身，不会有黑底/黑边
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.depth = 90f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 50f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;
        cam.cullingMask = 1 << LayerForHud();
        cam.rect = new Rect(0f, 0f, 1f, 1f);

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.SetRenderer(config.hudCameraRendererIndex);

        if (_renderTexture != null)
        {
            cam.targetTexture = _renderTexture;
        }
    }

    private RawImage CreateRawImage()
    {
        var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = config.canvasSortingOrder;

        var imageGo = new GameObject("Bust", typeof(RectTransform));
        imageGo.transform.SetParent(canvasGo.transform, false);

        var image = imageGo.AddComponent<RawImage>();
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(Screen.width * config.bustSize.x, Screen.height * config.bustSize.y);

        return image;
    }

    private void EnsureRenderTexture(int width, int height)
    {
        // 编辑器里生成预制体时不建 RT（RT 是运行时资源，存不进 prefab）
        if (!Application.isPlaying)
        {
            return;
        }

        width = Mathf.Clamp(width, 64, 2048);
        height = Mathf.Clamp(height, 64, 2048);

        if (_renderTexture != null && Mathf.Abs(width - _rtWidth) < 8 && Mathf.Abs(height - _rtHeight) < 8)
        {
            return;
        }

        if (_renderTexture != null)
        {
            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = null;
            }

            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        _rtWidth = width;
        _rtHeight = height;

        _renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "Live2D HUD RT",
            filterMode = FilterMode.Bilinear
        };

        _renderTexture.Create();

        if (_hudCamera != null)
        {
            _hudCamera.targetTexture = _renderTexture;
        }

        if (_rawImage != null)
        {
            _rawImage.texture = _renderTexture;
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;

        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // =====================================================================
    // 生命周期
    // =====================================================================

    private void Awake()
    {
        _debugVisible = false;
    }

    private void Start()
    {
        if (config == null)
        {
            return;
        }

        _findTimer = 0f;
        BindPlayer();
        _nextFlavorTime = Time.time + Random.Range(config.idleFlavorDelay.x, config.idleFlavorDelay.y);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnHealthChanged -= OnHealthChanged;
        }

        if (_evade != null)
        {
            _evade.OnPerfectDodge -= OnPlayerPerfectDodge;
        }

        if (_hudCamera != null)
        {
            _hudCamera.targetTexture = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }

    /// <summary>完美闪避成功 → 立刻切"干脆利落"的表情。</summary>
    private void OnPlayerPerfectDodge(GameObject attacker)
    {
        TriggerReaction(config.perfectEvadeMotion);
    }

    private IEnumerator MeasureBoundsRoutine()
    {
        // 模型是第一帧初始化完才有点顶点的，等几帧再量
        for (var frame = 0; frame < 40; frame++)
        {
            if (TryMeasureBounds())
            {
                yield break;
            }

            if (frame == 3 || frame == 10)
            {
                _model.ForceUpdateNow();
            }

            yield return null;
        }

        // 兜底：用 Vivi 模型的已知包围盒（x -0.51..0.49 / y -1.15..1.15）
        _boundsMin = new Vector3(-0.514f, -1.145f, 0f);
        _boundsMax = new Vector3(0.486f, 1.152f, 0f);
        _boundsReady = true;
        Debug.LogWarning("[Live2D HUD] 没量到模型包围盒，改用兜底尺寸。立绘比例可能略有偏差。");
    }

    private bool TryMeasureBounds()
    {
        if (_model == null || _model.Drawables == null || _model.Drawables.Length == 0)
        {
            return false;
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var any = false;

        foreach (var drawable in _model.Drawables)
        {
            if (drawable == null)
            {
                continue;
            }

            var vertices = drawable.VertexPositions;

            if (vertices == null || vertices.Length == 0)
            {
                continue;
            }

            foreach (var v in vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
                any = true;
            }
        }

        if (!any)
        {
            return false;
        }

        var size = max - min;

        // 顶点还没初始化的帧会全是 0，这种尺寸不能当结果
        if (size.x < 0.01f || size.y < 0.01f)
        {
            return false;
        }

        _boundsMin = min;
        _boundsMax = max;
        _boundsReady = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_built)
        {
            return;
        }

        if (_health == null)
        {
            _findTimer -= Time.unscaledDeltaTime;

            if (_findTimer <= 0f)
            {
                _findTimer = 0.25f;
                BindPlayer();
            }
        }

        UpdateLayout();
        UpdateFade();
        TickPerformance();
        SelfHeal();

        if (Input.GetKeyDown(KeyCode.F2))
        {
            _debugVisible = !_debugVisible;
        }
    }

    /// <summary>
    /// 兜底：2 秒后如果还没量到尺寸 / 还几乎透明，就强制修一次，保证"一定能看见"。
    /// </summary>
    private void SelfHeal()
    {
        if (_selfHealed || _spawnTime <= 0f || Time.time - _spawnTime < 2f)
        {
            return;
        }

        _selfHealed = true;

        if (!_boundsReady)
        {
            _boundsMin = new Vector3(-0.514f, -1.145f, 0f);
            _boundsMax = new Vector3(0.486f, 1.152f, 0f);
            _boundsReady = true;
            Debug.LogWarning("[Live2D HUD] 2 秒还没量到模型包围盒，改用兜底尺寸（Vivi 的已知尺寸）。");
        }

        var inCutscene = config.hideDuringCutscene && _controller != null && _controller.cutsceneMode;
        var alpha = _rawImage != null ? _rawImage.color.a : 1f;

        if (alpha < 0.2f && !inCutscene)
        {
            Debug.LogWarning($"[Live2D HUD] 2 秒后还几乎透明（alpha={alpha:0.00}），强制显示。");
            _fade = 1f;
            SetHudAlpha(config.opacity);
        }
    }

    /// <summary>统一设置 UI 上的整体透明度（不动 Cubism 的 per-drawable Opacity）。</summary>
    private void SetHudAlpha(float alpha)
    {
        if (_rawImage == null)
        {
            return;
        }

        var color = _rawImage.color;
        color.r = _tint.r;
        color.g = _tint.g;
        color.b = _tint.b;
        color.a = Mathf.Clamp01(alpha);
        _rawImage.color = color;
    }

    // =====================================================================
    // 玩家绑定
    // =====================================================================

    private void BindPlayer()
    {
        if (config == null || _health != null)
        {
            return;
        }

        _controller = FindObjectOfType<PlayerController>();

        if (_controller == null)
        {
            return;
        }

        var go = _controller.gameObject;

        _health = go.GetComponent<PlayerHealth>();

        if (_health == null)
        {
            return;
        }

        _melee = go.GetComponent<PlayerMelee>();
        _evade = go.GetComponent<PlayerEvade>();
        _jump = go.GetComponent<PlayerJump>();
        _gun = go.GetComponent<PlayerGun>();
        _magic = go.GetComponent<PlayerMagic>();
        _binding = go.GetComponent<PlayerBinding>();

        _health.OnHealthChanged += OnHealthChanged;

        if (_evade != null)
        {
            _evade.OnPerfectDodge += OnPlayerPerfectDodge;
        }

        _lastHp = _health.CurrentHealth;
        _pendingDamage = false;
        _state = HudState.None;
        _hpBucket = int.MinValue;
        _currentMotion = "";

        _nextFlavorTime = Time.time + Random.Range(config.idleFlavorDelay.x, config.idleFlavorDelay.y);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (_lastHp >= 0)
        {
            if (current < _lastHp)
            {
                _lastDamageTime = Time.time;
                _pendingDamage = true;
                _damageEvents++;
            }
            else if (current > _lastHp)
            {
                _lastHealTime = Time.time;
            }
        }

        _lastHp = current;
    }

    // =====================================================================
    // 站位 / 大小：立绘专用相机的小视口放在屏幕左上角，只露上半身
    // =====================================================================

    private void UpdateLayout()
    {
        if (_hudCamera == null || _rawImage == null)
        {
            return;
        }

        // 1) UI 上那块"上半身"方框：左上角锚点 + 屏幕比例尺寸
        var anchor = config.bustTopLeftAnchor;
        var size = config.bustSize;
        var width = Mathf.Clamp(Screen.width * Mathf.Clamp(size.x, 0.02f, 1f), 64f, 2048f);
        var height = Mathf.Clamp(Screen.height * Mathf.Clamp(size.y, 0.02f, 1f), 64f, 2048f);

        var rectTransform = _rawImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(
            Mathf.Clamp01(anchor.x) * Screen.width,
            -Mathf.Clamp01(anchor.y) * Screen.height);
        rectTransform.sizeDelta = new Vector2(width, height);

        EnsureRenderTexture(Mathf.RoundToInt(width), Mathf.RoundToInt(height));

        if (!_boundsReady || _modelRoot == null)
        {
            return;
        }

        // 2) 只露上半身：把"头顶往下 bustFraction 那一截"正好铺满 RT
        var localSize = _boundsMax - _boundsMin;
        var fraction = Mathf.Clamp(config.bustFraction, 0.05f, 1f);
        var bustHeight = localSize.y * fraction;

        if (bustHeight < 0.0001f)
        {
            return;
        }

        var scale = Mathf.Max(0.0001f, config.bustScale);
        var scaleX = config.faceRight ? scale : -scale;

        _hudCamera.orthographicSize = bustHeight * scale * 0.5f;

        var centerX = (_boundsMin.x + _boundsMax.x) * 0.5f;
        var centerY = _boundsMax.y - bustHeight * 0.5f;

        _modelRoot.localScale = new Vector3(scaleX, scale, scale);
        _modelRoot.localPosition = new Vector3(-centerX * scaleX, -centerY * scale, config.depthFromCamera);
    }

    // =====================================================================
    // 淡入 / 死亡淡出
    // =====================================================================

    private void UpdateFade()
    {
        if (_rawImage == null)
        {
            return;
        }

        // 还没量到模型尺寸之前先别露脸，免得用错误的比例闪一下
        if (!_boundsReady)
        {
            SetHudAlpha(0f);
            return;
        }

        _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, config.fadeInSeconds));

        var isDead = _health != null && _health.IsDead;

        // 过场演出时让立绘淡出（默认关，免得一开场看不到立绘）
        var hideForCutscene = config.hideDuringCutscene && _controller != null && _controller.cutsceneMode;

        float target;
        float speed;

        if (hideForCutscene)
        {
            target = 0f;
            speed = 3.5f;
        }
        else if (isDead)
        {
            target = config.deadOpacity * _fade;
            speed = Mathf.Max(0.5f, 1f / Mathf.Max(0.01f, config.deadFadeSeconds));
        }
        else
        {
            target = config.opacity * _fade;
            speed = 6f;
        }

        // 死亡整体压暗（乘色，不是 alpha）——不会像 Cubism 逐部件 alpha 那样露出"透底"
        _tint = isDead ? config.deadTint : Color.white;

        var alpha = Mathf.MoveTowards(_rawImage.color.a, target, Time.unscaledDeltaTime * speed);
        SetHudAlpha(alpha);
    }

    // =====================================================================
    // 表现状态机
    // =====================================================================

    private void TickPerformance()
    {
        if (_actor == null || _health == null)
        {
            return;
        }

        var now = Time.time;
        var dead = _health.IsDead;

        _hpBucket = CurrentHpBucket(dead);
        var baseMotion = BaseMotionFor(_hpBucket);

        // 播任何一次性动作之前先定好"播完回哪里"——永远回到当前血量的循环
        _actor.SetIdleMotion(baseMotion);

        // 1) 受击：每次掉血都重新打断一次，连击也有反馈
        if (_pendingDamage && !dead)
        {
            _pendingDamage = false;
            Play(_alternateDamage ? config.damageMotion2 : config.damageMotion1, false);
            _alternateDamage = !_alternateDamage;
            _state = HudState.Damage;
            _lastDamageTime = now;
            return;
        }

        // 2) 死亡：先播一次倒地，播完自动回 HP_000 循环
        if (dead)
        {
            _pendingDamage = false;

            if (_state != HudState.Dead)
            {
                Play(config.deathMotion, false);
                _state = HudState.Dead;
            }

            return;
        }

        // 3) 一次性输入反应（开枪 / 换弹 / 蹲下），压住一小会儿
        if (now >= _overrideUntil)
        {
            var reaction = ReadReactionMotion();

            if (!string.IsNullOrEmpty(reaction))
            {
                TriggerReaction(reaction);
            }
        }

        if (now < _overrideUntil)
        {
            _state = HudState.Override;
            return;
        }

        // 4) 其它状态
        var state = DecideState(now);

        if (state == HudState.Idle)
        {
            // 待机小动作还在播的时候不要立刻把它盖回血量循环
            if (now >= _flavorUntil)
            {
                var needBase = _state != HudState.Idle || _currentMotion != baseMotion;

                if (needBase)
                {
                    Play(baseMotion, true);
                    _nextFlavorTime = now + Random.Range(config.idleFlavorDelay.x, config.idleFlavorDelay.y);
                }
                else if (now >= _nextFlavorTime)
                {
                    PlayRandomFlavor(now);
                }
            }

            _state = HudState.Idle;
            return;
        }

        if (state != _state)
        {
            Play(MotionForState(state), IsLoopingState(state));
            _state = state;
        }
    }

    /// <summary>读本帧的输入，返回要临时播的一次性反应动作（没有就返回空）。</summary>
    private string ReadReactionMotion()
    {
        if (_controller == null)
        {
            return "";
        }

        var intent = _controller.Intent;

        if (intent.FirePressed) return config.fireMotion;
        if (intent.ReloadPressed) return config.reloadMotion;
        if (intent.CrouchPressed) return config.crouchMotion;

        return "";
    }

    private void TriggerReaction(string motionName)
    {
        if (!_actor.HasMotion(motionName))
        {
            return;
        }

        Play(motionName, false);
        _overrideMotion = motionName;
        _overrideUntil = Time.time + Mathf.Max(0.1f, config.reactionHoldSeconds);
    }

    private HudState DecideState(float now)
    {
        // 受击动作播完之前先压住，别被闪避/攻击立刻盖掉
        if (now - _lastDamageTime < Mathf.Max(0.05f, config.damageHoldSeconds))
        {
            return HudState.Damage;
        }

        if (_binding != null && _binding.IsActive)
        {
            return HudState.Bound;
        }

        if (_magic != null && _magic.IsBusy)
        {
            return HudState.Magic;
        }

        if (_melee != null && _melee.IsAttacking)
        {
            return HudState.Attack;
        }

        if (_evade != null && _evade.IsActive)
        {
            return HudState.Evade;
        }

        if (now - _lastHealTime < 0.5f)
        {
            return HudState.Heal;
        }

        if (_jump != null && _jump.OnAir)
        {
            return HudState.Air;
        }

        if (_gun != null && _gun.IsAds)
        {
            return HudState.Aim;
        }

        return HudState.Idle;
    }

    private string MotionForState(HudState state)
    {
        switch (state)
        {
            case HudState.Attack: return config.attackMotion;
            case HudState.Evade: return config.evadeMotion;
            case HudState.Air: return config.airMotion;
            case HudState.Aim: return config.aimMotion;
            case HudState.Magic: return config.magicMotion;
            case HudState.Bound: return config.boundMotion;
            case HudState.Heal: return config.healMotion;
            case HudState.Damage: return config.damageMotion1;
        }

        return "";
    }

    private static bool IsLoopingState(HudState state)
    {
        switch (state)
        {
            case HudState.Aim:
            case HudState.Magic:
            case HudState.Bound:
                return true;
        }

        return false;
    }

    private int CurrentHpBucket(bool dead)
    {
        if (dead)
        {
            return 0;
        }

        var max = _health.MaxHealth;
        var ratio = max > 0 ? Mathf.Clamp01((float)_health.CurrentHealth / max) : 1f;

        if (ratio > 0.875f) return 5;
        if (ratio > 0.625f) return 4;
        if (ratio > 0.375f) return 3;
        if (ratio > 0.08f) return 2;
        if (ratio > 0f) return 1;

        return 0;
    }

    private string BaseMotionFor(int bucket)
    {
        switch (bucket)
        {
            case 5: return config.hp100Motion;
            case 4: return config.hp75Motion;
            case 3: return config.hp50Motion;
            case 2: return config.hp25Motion;
            case 1: return config.hp01Motion;
        }

        return config.hp00Motion;
    }

    private void PlayRandomFlavor(float now)
    {
        var flavors = config.idleFlavorMotions != null ? config.idleFlavorMotions : Dummy;

        if (flavors.Length > 0 && _actor != null)
        {
            var index = Random.Range(0, flavors.Length);

            for (var i = 0; i < flavors.Length; i++)
            {
                var name = flavors[(index + i) % flavors.Length];

                if (_actor.HasMotion(name))
                {
                    Play(name, false, true);
                    break;
                }
            }
        }

        _nextFlavorTime = now + Random.Range(config.idleFlavorDelay.x, config.idleFlavorDelay.y);
    }

    private void Play(string motionName, bool loop, bool isFlavor = false)
    {
        if (string.IsNullOrEmpty(motionName))
        {
            return;
        }

        if (_actor == null)
        {
            return;
        }

        if (!_actor.HasMotion(motionName))
        {
            if (config.warnOnMissingMotion && !_missingWarned)
            {
                _missingWarned = true;
                Debug.LogWarning($"[Live2D HUD] 动作表里没有 \"{motionName}\"，检查 Live2DHudConfig / 模型动作列表。");
            }

            return;
        }

        _actor.PlayMotion(motionName, loop);
        _currentMotion = motionName;

        // 待机小动作播完之前不要被血量循环盖掉；别的动作一律清掉这个标记
        _flavorUntil = isFlavor
            ? Time.time + Mathf.Max(0.4f, _actor.GetMotionLength(motionName))
            : 0f;
    }

    // =====================================================================
    // 调试
    // =====================================================================

    /// <summary>模型量好尺寸、可以正常显示。</summary>
    public bool IsReady => _built && _boundsReady;

    /// <summary>当前正在播的动作名。</summary>
    public string CurrentMotion => _currentMotion;

    /// <summary>当前表现状态名。</summary>
    public string CurrentState => _state.ToString();

    /// <summary>当前整体不透明度（0 = 没显示）。来自 UI 的 RawImage，不是 Cubism 的逐部件 alpha。</summary>
    public float CurrentOpacity => _rawImage != null ? _rawImage.color.a : 0f;

    /// <summary>找到玩家了没。</summary>
    public bool HasPlayer => _health != null;

    /// <summary>HUD 绑定的玩家血量组件（调试 / 自动化测试用）。</summary>
    public PlayerHealth BoundHealth => _health;

    /// <summary>调试 / 自动化测试用的一行状态。</summary>
    public string DebugInfo =>
        $"hp={(_health != null ? _health.CurrentHealth : -1)} lastHp={_lastHp} " +
        $"pendingDamage={_pendingDamage} damageEvents={_damageEvents} " +
        $"state={_state} motion={_currentMotion} bucket={_hpBucket} " +
        $"rt={_rtWidth}x{_rtHeight} alpha={CurrentOpacity:0.00} " +
        $"actorAlive={(_actor != null)} renderAlive={(_render != null)}";

    private void OnGUI()
    {
        if (!_debugVisible && (config == null || !config.showDebugLine))
        {
            return;
        }

        if (config == null)
        {
            return;
        }

        var hp = _health != null ? $"{_health.CurrentHealth}/{_health.MaxHealth}" : "-";
        var text = $"[Live2D HUD] 状态 {_state}  动作 {(_currentMotion == "" ? "-" : _currentMotion)}  " +
                   $"血量 {hp}  bucket {_hpBucket}  淡入 {_fade:0.00}";

        GUI.Label(new Rect(12f, 12f, 1200f, 22f), text);
    }
}
