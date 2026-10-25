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
    [Header("角色标识（在对话数据中使用的名字）")]
    public string actorId = "vivi";

    [Header("动作片段配置")]
    public AnimationClip idleClip;
    public List<AnimationClip> motionClips = new List<AnimationClip>();

    private CubismExpressionController expr;
    private CubismMotionController motion;
    private CubismRenderController renderer;

    private Dictionary<string, int> exprIndex = new Dictionary<string, int>();
    private Dictionary<string, AnimationClip> clipByName = new Dictionary<string, AnimationClip>();
    private bool isVisible = true;

    void Awake()
    {
        expr = GetComponent<CubismExpressionController>();
        motion = GetComponent<CubismMotionController>();
        renderer = GetComponent<CubismRenderController>();

        // 收集表情
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

        // 收集动作
        foreach (var c in motionClips)
        {
            if (c != null) clipByName[c.name] = c;
        }
    }

    void OnEnable()
    {
        SetAlpha(1f);
        isVisible = true;
    }

    void Start()
    {
        SetVisible(false, true); // 初始隐藏
        if (idleClip != null) Invoke(nameof(PlayIdleDelay), 0.1f);
    }

    private void PlayIdleDelay()
    {
        if (idleClip != null) PlayMotion(idleClip.name, true);
    }

    public void SetVisible(bool visible, bool instant = false)
    {
        if (isVisible == visible && !instant) return;
        isVisible = visible;
        gameObject.SetActive(true); 

        StopAllCoroutines();
        if (instant)
        {
            SetAlpha(visible ? 1f : 0f);
        }
        else
        {
            StartCoroutine(FadeRoutine(visible));
        }
    }

    // ★ 修复：只做透明度渐变，绝不缩放
    private IEnumerator FadeRoutine(bool show)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        float startAlpha = show ? 0f : 1f;
        float targetAlpha = show ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }
        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (renderer == null) return;
        renderer.Opacity = alpha; 
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
                motion.PlayAnimation(clip, isIdle ? CubismMotionPriority.PriorityIdle : CubismMotionPriority.PriorityNormal, 0);
            }
            catch (System.Exception) { }
        }
    }
}