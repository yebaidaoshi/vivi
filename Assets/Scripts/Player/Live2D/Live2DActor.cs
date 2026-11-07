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
    public float fadeSpeed = 5f;

    [Header("动作片段配置")]
    public AnimationClip idleClip;
    public List<AnimationClip> motionClips = new List<AnimationClip>();

    [Header("动作衔接")]
    [Tooltip("提前多少秒开始接后续动作/待机，让两段动作的淡入淡出重叠，衔接更自然。0 = 播完才接")]
    public float postMotionOverlap = 0.15f;
    [Tooltip("打开后会在控制台打印每次动作播放和 SDK 的动作结束回调，用来排查问题")]
    public bool debugMotion = false;

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
        currentState = ActorState.Hidden;
        transform.localPosition = hidePosition;
        targetPosition = hidePosition;
        targetAlpha = 0f;

        if (renderer != null) renderer.Opacity = 0f;
        SetMultiplyColor(Color.white);

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
                if (wasHidden)
                {
                    transform.localPosition = stagePosition;
                    targetPosition = stagePosition;
                    if (renderer != null) renderer.Opacity = 0f;
                }
                break;

            case ActorState.Listening:
                targetColor = listenColor;
                targetAlpha = 1f;
                if (wasHidden)
                {
                    transform.localPosition = stagePosition;
                    targetPosition = stagePosition;
                    if (renderer != null) renderer.Opacity = 0f;
                }
                break;

            case ActorState.Hidden:
                targetPosition = hidePosition;
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

        if (renderer == null) return;

        if (Vector3.Distance(transform.localPosition, targetPosition) > 0.1f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, Time.deltaTime * moveSpeed);
        }

        if (currentState == ActorState.Hidden)
        {
            if (Vector3.Distance(transform.localPosition, hidePosition) < 1f)
            {
                targetAlpha = 0f;
            }
        }

        renderer.Opacity = Mathf.MoveTowards(renderer.Opacity, targetAlpha, Time.deltaTime * fadeSpeed);

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
}
