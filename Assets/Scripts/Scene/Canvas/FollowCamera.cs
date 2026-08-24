using UnityEngine;

/// <summary>
/// 挂载到 World Space Canvas 上，使其始终跟随摄像机
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [Header("摄像机（留空自动取主摄像机）")]
    public Camera targetCamera;
    [Tooltip("Canvas 与摄像机的距离")]
    public float distanceFromCamera = 10f;

    private Canvas canvas;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.renderMode = RenderMode.WorldSpace;

        // 如果 Canvas 是 World Space，自动调整大小以匹配摄像机视野
        if (targetCamera != null && canvas != null && targetCamera.orthographic)
        {
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // 位置放在摄像机正前方
        transform.position = targetCamera.transform.position
                           + targetCamera.transform.forward * distanceFromCamera;
        // 朝向摄像机
        transform.LookAt(targetCamera.transform);
        // 或者只旋转 Z 轴保持水平？根据需求调整
        // 如果您想要 Canvas 始终平行于屏幕（而不是旋转），可以改为：
        // transform.rotation = Quaternion.identity;
        // 但最好正面朝向摄像机，否则 UI 可能倾斜
    }
}