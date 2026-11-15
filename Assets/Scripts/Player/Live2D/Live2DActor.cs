using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2D.Cubism.Framework.Expression;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Rendering;

[RequireComponent(typeof(CubismExpressionController))]
[RequireComponent(typeof(CubismMotionController))]
[RequireComponent(typeof(CubismRenderController))]
public class Live2DActor : MonoBehaviour
{
    [Header("角色标识（场景里每个角色必须唯一！）")]
    public string actorId = "vivi";

    [Header("位置配置")]
    public Vector3 stagePosition = new Vector3(-300, 0, 0);
    public Vector3 hidePosition = new Vector3(-1500, 0, 0);
    public float moveSpeed = 10f;

    [Header("颜色与淡入配置")]
    public Color talkColor = Color.white;
    public Color listenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("淡入淡出速度（旧参数，保留兼容）")]
    public float fadeSpeed = 5f;
    [Tooltip("对话触发时淡入、对话结束时淡出，各用多少秒（新参数，默认 0.35s；旧 prefab 不用改也会生效）")]
    public float fadeSeconds = 0.35f;

    [Header("动作片段配置")]
    public AnimationClip idleClip;
    public List<AnimationClip> motionClips = new List<AnimationClip>();

    [Header("动作衔接")]
    [Tooltip("提前多少秒开始接后续动作/待机，让两段动作的淡入淡出重叠，衔接更自然。0 = 播完才接")]
    public float postMotionOverlap = 0.15f;
    [Tooltip("打开后会在控制台打印每次动作播放和 SDK 的动作结束回调，用来排查问题")]
    public bool debugMotion = false;

    [Header("游戏内实时 HUD 模式")]
    [Tooltip("勾选 = 对话用：开局隐藏、淡出。取消 = 开局直接可见（游戏内实时 HUD 用）")]
    public bool hideOnStart = true;
    [Tooltip("勾选 = 本脚本自己挪位置 / 淡入淡出 / 变色。取消 = 位置与透明度交给外部（Live2DRealtimeHud）控制")]
    public bool manageTransformAndFade = true;
    [Tooltip("对话收场方式：勾选 = 原地淡出；取消 = 旧行为（滑到 hidePosition 移出屏幕再淡出）")]
    public bool hideInPlace = false;
    [Tooltip("勾选 = 对话立绘走「相机→RenderTexture→UI alpha」。注意：当前实现下同一场景多个角色会互相串（每台相机都能看到所有角色），且半分辨率会糊，默认先关掉")]
    public bool useScreenLayer = false;

    private float _fadeT;

    private Live2DCanvasLayer _screenLayer;
    private float _screenAlpha;

    private CubismExpressionController expr;
    private CubismMotionController motion;
    private CubismRenderController renderer;
    private CubismFadeController fadeController;
    private CubismRenderer[] cubismRenderers;

    private Dictionary<string, int> exprIndex = new Dictionary<string, int>();
    private Dictionary<string, AnimationClip> clipByName = new Dictionary<string, AnimationClip>();

    public enum ActorState { Hidden, Listening, Talking }
    public ActorState currentState = ActorState.Hidden;

    private Vector3 targetPosition;
    private Color targetColor;
    private float targetAlpha = 0f;

    // ---- 动作播放状态（全部由我们自己的计时器驱动，不依赖 SDK 的动作结束回调） ----
    private string _idleClipName = "";
    private AnimationClip _currentClip;      // 当前由我们发起的动作
    private bool _currentIsLoop;
    private string _pendingNext = "";        // 当前动作播完后接的动作（空 = 回待机）
    private float _switchTime;               // 到这个时间就接后续动作
    private float _startTime;
    private int _prioritySeq = CubismMotionPriority.PriorityForce;

    void Awake()
    {
        expr = GetComponent<CubismExpressionController>();
        motion = GetComponent<CubismMotionController>();
        renderer = GetComponent<CubismRenderController>();
        fadeController = GetComponent<CubismFadeController>();
        cubismRenderers = GetComponentsInChildren<CubismRenderer>();

        if (expr != null && expr.ExpressionsList != null && expr.ExpressionsList.CubismExpressionObjects != null)
        {
            var list = expr.ExpressionsList.CubismExpressionObjects;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    string cleanName = list[i].name.Replace(".exp3", "");
                    exprIndex[cleanName] = i;
                }
            }
        }

        foreach (var c in motionClips)
        {
            if (c != null) clipByName[c.name] = c;
        }

        if (idleClip != null)
        {
            clipByName[idleClip.name] = idleClip;
            _idleClipName = idleClip.name;
        }
        else
        {
            foreach (var c in motionClips)
            {
                if (c != null && (c.name.Contains("Breathing") || c.name.Contains("Idle")))
                {
                    idleClip = c;
                    _idleClipName = c.name;
                    break;
                }
            }
        }

        if (fadeController == null || fadeController.CubismFadeMotionList == null)
        {
            Debug.LogError($"[Live2D 警告] 角色 {actorId} 的 Cubism Fade Controller 为空，动作将无法播放！");
        }
        else
        {
            Debug.Log($"[Live2D] 角色 {actorId} 已加载动作: {string.Join(", ", clipByName.Keys)}");
        }
    }

    void Start()
    {
        if (hideOnStart)
        {
            currentState = ActorState.Hidden;

            // 原地淡出模式：人留在自己的站位上，只把透明度淡到 0，不走"移出屏幕"那套
            if (!hideInPlace)
            {
                transform.localPosition = hidePosition;
            }

            targetPosition = transform.localPosition;
            targetAlpha = 0f;

            if (renderer != null) renderer.Opacity = 0f;
        }
        else
        {
            // 游戏内实时 HUD：开局就站在舞台上，不再走"从场外滑进来"的对话流程
            currentState = ActorState.Listening;
            targetPosition = transform.localPosition;
            targetColor = talkColor;
            targetAlpha = 1f;

            // 透明度由外部（Live2DRealtimeHud）控制时不要在这里抢，否则第一帧会闪一下
            if (renderer != null && manageTransformAndFade) renderer.Opacity = 1f;
        }

        SetMultiplyColor(Color.white);

        // 旧行为（移出屏幕收场）强制生效：prefab 里存了 hideInPlace: 1 也不管用。
        hideInPlace = false;
        useScreenLayer = false;
        _screenLayer = null;
        _screenAlpha = 0f;

        // actorId 重复是最常见的"NPC 莫名其妙跟着播动作"的原因（脚本默认值是 vivi）
        foreach (var other in FindObjectsOfType<Live2DActor>())
        {
            if (other != this && other.actorId == actorId)
                Debug.LogWarning($"[Live2D] 场景里有两个角色的 actorId 都是 \"{actorId}\"（{name} 和 {other.name}），对话会同时驱动它们！");
        }

        if (debugMotion && motion != null) motion.AnimationEndHandler += DebugOnAnimationEnd;

        if (!string.IsNullOrEmpty(_idleClipName)) Invoke(nameof(PlayIdleDelay), 0.1f);
    }

    void OnDestroy()
    {
        if (debugMotion && motion != null) motion.AnimationEndHandler -= DebugOnAnimationEnd;

        if (_screenLayer != null)
        {
            Destroy(_screenLayer.gameObject);
            _screenLayer = null;
        }
    }

    private void PlayIdleDelay()
    {
        if (!string.IsNullOrEmpty(_idleClipName)) PlayMotion(_idleClipName, true);
    }

    // 只用于排查：打印 SDK 的"动作结束"回调。参数在较新的 SDK 里是动画片段的 InstanceId，不是层号。
    private void DebugOnAnimationEnd(int id)
    {
        string cur = _currentClip != null ? _currentClip.name : "(无)";
        Debug.Log($"[Live2D:{actorId}] SDK 动作结束回调 arg={id}  我们当前的动作={cur}  已播放 {Time.time - _startTime:0.00}s");
    }

    public void SetState(ActorState state)
    {
        if (currentState == state) return;

        bool wasHidden = (currentState == ActorState.Hidden);
        currentState = state;

        switch (state)
        {
            case ActorState.Talking:
                targetColor = talkColor;
                targetAlpha = 1f;
                if (wasHidden && !hideInPlace)
                {
                    transform.localPosition = stagePosition;
                    targetPosition = stagePosition;
                    if (renderer != null) renderer.Opacity = 0f;
                }
                break;

            case ActorState.Listening:
                targetColor = listenColor;
                targetAlpha = 1f;
                if (wasHidden && !hideInPlace)
                {
                    transform.localPosition = stagePosition;
                    targetPosition = stagePosition;
                    if (renderer != null) renderer.Opacity = 0f;
                }
                break;

            case ActorState.Hidden:
                targetPosition = hideInPlace ? transform.localPosition : hidePosition;
                break;
        }
    }

    public void SetFacing(bool faceRight)
    {
        Vector3 scale = transform.localScale;
        scale.x = faceRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    void Update()
    {
        UpdateMotionSequence();

        if (!manageTransformAndFade) return;
        if (renderer == null) return;

        if (Vector3.Distance(transform.localPosition, targetPosition) > 0.1f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, Time.deltaTime * moveSpeed);
        }

        if (currentState == ActorState.Hidden)
        {
            // 原地淡出：不用等走到 hidePosition，直接淡
            if (hideInPlace || Vector3.Distance(transform.localPosition, hidePosition) < 1f)
            {
                targetAlpha = 0f;
            }
        }

        float fadeStep = Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);

        _fadeT = Mathf.MoveTowards(_fadeT, targetAlpha, fadeStep);
        renderer.Opacity = _fadeT;

        Color currentColor = cubismRenderers.Length > 0 ? cubismRenderers[0].MultiplyColor : Color.white;
        if (currentColor != targetColor)
        {
            Color newColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * moveSpeed);
            SetMultiplyColor(newColor);
        }
    }

    private void SetMultiplyColor(Color color)
    {
        if (cubismRenderers == null) return;
        foreach (var r in cubismRenderers)
        {
            if (r != null) r.MultiplyColor = color;
        }
    }

    public void SetExpression(string name)
    {
        if (string.IsNullOrEmpty(name) || expr == null) return;

        if (exprIndex.ContainsKey(name))
        {
            expr.CurrentExpressionIndex = exprIndex[name];
        }
        else if (clipByName.ContainsKey(name))
        {
            PlayMotion(name, false);
        }
        else if (name == "Default" || name == "")
        {
            expr.CurrentExpressionIndex = -1;
        }
    }

    /// <summary>
    /// 播放动作。
    /// isLoop = true  : 循环播放（待机用），不会触发后续动作。
    /// isLoop = false : 播放一次，播完（提前 postMotionOverlap 秒）接 nextMotion；nextMotion 为空则回到待机。
    /// </summary>
    public void PlayMotion(string name, bool isLoop = false, string nextMotion = "")
    {
        if (string.IsNullOrEmpty(name) || motion == null) return;

        if (!clipByName.TryGetValue(name, out var clip) || clip == null)
        {
            // 注意区分两种日志：这一条是"这个角色的 motionClips 里没有这个名字"；
            // 控制台里的 "can't start motion." 才是 SDK 自己拒绝播放。
            Debug.LogWarning($"[Live2D] 角色 {actorId} 的动作列表里找不到: {name}");
            return;
        }
        if (clip.length <= 0.01f) return;

        // 每次都使用比之前所有调用都大的优先级：SDK 只在"新优先级 > 当前优先级"时才会开始新动作，
        // 用递增值就不会再因为优先级相同/偏低被拒绝（Force 打断 Force、循环打断待机 都会被拒）。
        int priority = ++_prioritySeq;

        _currentClip = clip;
        _currentIsLoop = isLoop;
        _pendingNext = isLoop ? "" : nextMotion;
        _startTime = Time.time;
        float overlap = Mathf.Min(postMotionOverlap, clip.length * 0.5f);
        _switchTime = isLoop ? float.PositiveInfinity : Time.time + clip.length - overlap;

        if (debugMotion)
            Debug.Log($"[Live2D:{actorId}] 播放 {name}  长度 {clip.length:0.00}s  循环={isLoop}  优先级={priority}  接续={(string.IsNullOrEmpty(_pendingNext) ? "待机" : _pendingNext)}");

        try
        {
            motion.PlayAnimation(clip, layerIndex: 0, priority: priority, isLoop: isLoop);
        }
        catch (System.Exception e)
        {
            // 通常是这个片段没有登记到 CubismFadeMotionList，请用 Live2DMotionDiagnostics 检查
            Debug.LogWarning($"[Live2D] 角色 {actorId} 播放 {name} 失败: {e.Message}");
        }
    }

    // 计时器驱动：到时间就接后续动作或回到待机，不依赖 SDK 的动作结束回调
    private void UpdateMotionSequence()
    {
        if (_currentClip == null || _currentIsLoop) return;
        if (Time.time < _switchTime) return;

        string next = _pendingNext;
        _currentClip = null;      // 先清掉，防止重复触发
        _pendingNext = "";

        if (!string.IsNullOrEmpty(next) && clipByName.ContainsKey(next))
            PlayMotion(next, false, "");
        else if (!string.IsNullOrEmpty(_idleClipName))
            PlayMotion(_idleClipName, true);
    }

    // ================= 给游戏内实时 HUD（Live2DRealtimeHud）用的接口 =================

    /// <summary>这个角色的动作表里有没有这个名字（不会打警告）。</summary>
    public bool HasMotion(string name)
    {
        return !string.IsNullOrEmpty(name) && clipByName.ContainsKey(name);
    }

    /// <summary>动作时长（秒）。找不到返回 0。</summary>
    public float GetMotionLength(string name)
    {
        return clipByName.TryGetValue(name, out var clip) && clip != null ? clip.length : 0f;
    }

    /// <summary>
    /// 设定"动作播完后回到哪个循环"。
    /// HUD 会把它设成当前血量对应的 HP_xxx 循环，这样受击/Damage 播完就回到该血量的样子。
    /// </summary>
    public void SetIdleMotion(string name)
    {
        if (HasMotion(name)) _idleClipName = name;
    }

    /// <summary>当前的回待机动作名。</summary>
    public string IdleMotion => _idleClipName;

    /// <summary>当前正在播的动作名（HUD 自己记的，不参与对话逻辑）。</summary>
    public string CurrentMotionName => _currentClip != null ? _currentClip.name : "";

    /// <summary>外部补登记动作片段（HUD 从 Live2DHudConfig 里把整套片段喂进来）。</summary>
    public void RegisterClips(IEnumerable<AnimationClip> clips)
    {
        if (clips == null) return;

        foreach (var clip in clips)
        {
            if (clip != null) clipByName[clip.name] = clip;
        }
    }

    /// <summary>找不到动作时静默返回 false，方便 HUD 先探测再播。</summary>
    public bool TryPlayMotion(string name, bool isLoop = false, string nextMotion = "")
    {
        if (!HasMotion(name)) return false;

        PlayMotion(name, isLoop, nextMotion);
        return true;
    }

    /// <summary>立刻设置透明度（HUD 淡入淡出用）。</summary>
    public void SetOpacityNow(float opacity)
    {
        targetAlpha = Mathf.Clamp01(opacity);
        if (renderer != null) renderer.Opacity = targetAlpha;
    }

    /// <summary>改整体 MultiplyColor（HUD 配色调色用）。</summary>
    public void SetTint(Color color)
    {
        targetColor = color;
        SetMultiplyColor(color);
    }
}
