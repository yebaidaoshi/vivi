using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 使 UI Image 自动填满主摄像机的视口（屏幕范围）。
/// 适用于任何 Canvas 渲染模式。
/// </summary>
[RequireComponent(typeof(Image))]
public class UIFullscreenFit : MonoBehaviour
{
    [Header("目标摄像机（留空自动取主摄像机）")]
    public Camera targetCamera;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (targetCamera == null) targetCamera = Camera.main;

        // 强制将 Image 的锚点设为全屏拉伸
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;      // 偏移归零
        rectTransform.anchoredPosition = Vector2.zero;

        // 如果 Canvas 是 World Space，则根据摄像机视口大小调整
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            // 世界空间下，根据摄像机视野计算宽度/高度
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}