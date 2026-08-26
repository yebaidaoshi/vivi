using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Camera targetCamera;
    public float distanceFromCamera = 5f;

    public enum RotationMode
    {
        FullBillboard,      // 完全面对摄像机（可能倾斜）
        YAxisBillboard,     // 仅绕Y轴面对摄像机（保持竖直）
        FixedDirection      // 固定朝向（完全不旋转）
    }
    public RotationMode rotationMode = RotationMode.YAxisBillboard;

    private Canvas canvas;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.renderMode = RenderMode.WorldSpace;

        // 重置变换
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        // 自动适应摄像机视野（仅正交模式）
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

        // 1. 位置始终在摄像机前方
        transform.position = targetCamera.transform.position
                           + targetCamera.transform.forward * distanceFromCamera;

        // 2. 根据模式设置旋转
        switch (rotationMode)
        {
            case RotationMode.FullBillboard:
                // 完全面对摄像机（可能倾斜）
                Vector3 dirFull = targetCamera.transform.position - transform.position;
                transform.rotation = Quaternion.LookRotation(dirFull, Vector3.up);
                break;

            case RotationMode.YAxisBillboard:
                // 仅绕Y轴面对摄像机（保持竖直）
                Vector3 dirY = targetCamera.transform.position - transform.position;
                dirY.y = 0f; // 忽略垂直差异，只计算水平方向
                if (dirY.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dirY, Vector3.up);
                break;

            case RotationMode.FixedDirection:
                // 固定方向（完全不旋转）
                // 保持你设定的初始旋转，或者显式设为 (0,0,0)
                // 这里保持当前旋转不变
                break;
        }
    }
}