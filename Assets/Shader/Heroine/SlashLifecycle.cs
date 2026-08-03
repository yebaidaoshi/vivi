using UnityEngine;

public class SlashLifecycle : MonoBehaviour
{
    [Header("爆发动画设置")]
    public float duration = 0.5f; // 特效存在多久
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private float timer = 0f;
    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
        // 出场时，瞬间把缩放变成 0，准备爆发
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer < duration)
        {
            // 根据时间曲线，控制模型从 0 瞬间放大到 1，甚至稍微超出一点（产生爆发力）
            float scaleValue = scaleCurve.Evaluate(timer / duration);
            transform.localScale = originalScale * scaleValue;
        }
        else
        {
            // 时间到了，直接销毁，防止场景里堆满特效
            Destroy(gameObject);
        }
    }
}