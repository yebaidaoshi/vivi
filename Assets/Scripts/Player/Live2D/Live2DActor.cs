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
    [Header("角色标识")]
    public string actorId = "vivi";

    [Header("位置配置")]
    [Tooltip("角色的固定舞台站位（比如 Vivi -300，NPC 300）")]
    public Vector3 stagePosition = new Vector3(-300, 0, 0);
    [Tooltip("退场隐藏时的位置（屏幕外）")]
    public Vector3 hidePosition = new Vector3(-1500, 0, 0);
    public float moveSpeed = 10f;

    [Header("颜色与淡入配置")]
    public Color talkColor = Color.white;
    public Color listenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public float fadeSpeed = 5f;
    
    [Header("动作片段配置")]
    public AnimationClip idleClip;
    public List<AnimationClip> motionClips = new List<AnimationClip>();

    private CubismExpressionController expr;
    private CubismMotionController motion;
    private CubismRenderController renderer;
    private CubismRenderer[] cubismRenderers;

    private Dictionary<string, int> exprIndex = new Dictionary<string, int>();
    private Dictionary<string, AnimationClip> clipByName = new Dictionary<string, AnimationClip>();
    
    public enum ActorState { Hidden, Listening, Talking }
    public ActorState currentState = ActorState.Hidden; 
    
    private Vector3 targetPosition;
    private Color targetColor;
    private float targetAlpha = 0f;
    
    private bool _isPlayingLoopMotion = false;

    void Awake()
    {
        expr = GetComponent<CubismExpressionController>();
        motion = GetComponent<CubismMotionController>();
        renderer = GetComponent<CubismRenderController>();
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
        if (idleClip != null) clipByName[idleClip.name] = idleClip;
    }

    void Start()
    {
        currentState = ActorState.Hidden;
        transform.localPosition = hidePosition;
        targetPosition = hidePosition;
        targetAlpha = 0f;
        
        if (renderer != null) renderer.Opacity = 0f;
        SetMultiplyColor(Color.white);

        if (motion != null) motion.AnimationEndHandler += OnAnimationEnd;
        if (idleClip != null) Invoke(nameof(PlayIdleDelay), 0.2f);
    }

    void OnDestroy()
    {
        if (motion != null) motion.AnimationEndHandler -= OnAnimationEnd;
    }

    private void PlayIdleDelay()
    {
        if (idleClip != null) PlayMotion(idleClip.name, true);
    }

    private void OnAnimationEnd(int layerIndex)
    {
        // 只有当播放的不是 Idle 且不是循环动作时，才回到 Idle
        if (idleClip != null && !_isPlayingLoopMotion)
        {
            PlayMotion(idleClip.name, true);
        }
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
        if (exprIndex.ContainsKey(name)) expr.CurrentExpressionIndex = exprIndex[name];
        else if (name == "Default" || name == "") expr.CurrentExpressionIndex = -1;
    }

    public void PlayMotion(string name, bool isLoop = false)
    {
        if (string.IsNullOrEmpty(name) || motion == null) return;

        if (clipByName.TryGetValue(name, out var clip))
        {
            if (clip == null || clip.length <= 0.01f) return;
            try
            {
                _isPlayingLoopMotion = isLoop;
                motion.PlayAnimation(
                    clip,
                    layerIndex: 0,
                    priority: isLoop ? CubismMotionPriority.PriorityIdle : CubismMotionPriority.PriorityNormal,
                    isLoop: isLoop
                );
            }
            catch (System.Exception e) { Debug.LogException(e, this); }
        }
    }
}