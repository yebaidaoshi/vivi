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
    [Tooltip("角色的固定舞台站位（比如左边角色填 -300，右边角色填 300）")]
    public Vector3 stagePosition = new Vector3(-300, 0, 0);
    [Tooltip("退场隐藏时的位置（屏幕外，左边角色填 -1500，右边填 1500）")]
    public Vector3 hidePosition = new Vector3(-1500, 0, 0);
    [Tooltip("退场时的滑步速度")]
    public float moveSpeed = 10f;

    [Header("颜色与淡入配置")]
    public Color talkColor = Color.white;
    public Color listenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("原地淡入的速度")]
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
        
        // 初始状态：在屏幕外，目标位置也是屏幕外
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
        if (idleClip != null) PlayMotion(idleClip.name, true);
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
                    // ★ 核心修复：瞬移到站位，并且把目标位置也设为站位，防止自己跑向屏幕外
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
                // 对话彻底结束，目标位置设为屏幕外，开始滑步退场
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

        // 1. 平滑移动（只在退场时移动）
        if (Vector3.Distance(transform.localPosition, targetPosition) > 0.1f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPosition, Time.deltaTime * moveSpeed);
        }

        // 2. 如果是在退场，且完全滑出了屏幕，才把透明度降为0
        if (currentState == ActorState.Hidden)
        {
            if (Vector3.Distance(transform.localPosition, hidePosition) < 1f)
            {
                targetAlpha = 0f; 
            }
        }

        // 3. 平滑处理透明度（实现原地淡入）
        renderer.Opacity = Mathf.MoveTowards(renderer.Opacity, targetAlpha, Time.deltaTime * fadeSpeed);

        // 4. 平滑处理颜色（原地变暗变亮）
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

    public void PlayMotion(string name, bool isIdle = false)
    {
        if (string.IsNullOrEmpty(name) || motion == null) return;

        if (clipByName.TryGetValue(name, out var clip))
        {
            if (clip == null || clip.length <= 0.01f) return;
            try
            {
                motion.PlayAnimation(
                    clip,
                    layerIndex: 0,
                    priority: isIdle ? CubismMotionPriority.PriorityIdle : CubismMotionPriority.PriorityNormal,
                    isLoop: isIdle
                );
            }
            catch (System.Exception e) { Debug.LogException(e, this); }
        }
    }
}