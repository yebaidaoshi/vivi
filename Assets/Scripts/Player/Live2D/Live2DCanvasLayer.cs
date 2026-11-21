using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 对话用 Live2D 的"屏幕层"：把角色搬到很远的地方（主相机远裁剪面之外，主相机看不见它），
/// 再用一台专用相机把它渲染到 RenderTexture，最后用一张全屏 RawImage 贴回屏幕。
///
/// 好处：
///  - 淡入淡出作用在整张 RawImage 的 alpha 上 → 整体一起淡，没有 Cubism 逐 drawable 的"重叠透底"
///  - 不用关 CubismRenderController（那会破坏动作/参数更新）
///  - 不依赖相机的 cullingMask（这个 SDK 的 URP pass 本来就不看它）
///  - 每个角色自己的远端舞台相隔 10000，彼此相机看不到对方（被投影裁掉）
/// </summary>
public class Live2DCanvasLayer : MonoBehaviour
{
    private const float StageSpacing = 10000f;
    private static int _nextStageIndex;

    [Tooltip("UI 排序层：比对话 UI 小一点，保证立绘在对话框下面")]
    public int canvasSortingOrder = -1;

    private Camera _renderCamera;
    private Camera _sourceCamera;
    private RenderTexture _rt;
    private RawImage _image;
    private Vector3 _stageCenter;
    private Vector3 _originalCameraOffset;
    private int _rtW;
    private int _rtH;

    /// <summary>整层透明度（0~1）。</summary>
    public float Alpha
    {
        get => _image != null ? _image.color.a : 0f;
        set
        {
            if (_image == null) return;
            var c = _image.color;
            c.a = Mathf.Clamp01(value);
            _image.color = c;
        }
    }

    /// <summary>给某个对话角色挂一层屏幕层。sourceCamera 一般是 Camera.main；worldDisplayPosition 是角色"本该出现"的世界位置。</summary>
    public static Live2DCanvasLayer Attach(GameObject modelRoot, Camera sourceCamera, Vector3 worldDisplayPosition)
    {
        if (modelRoot == null) return null;

        var go = new GameObject("[Live2D Screen Layer]");
        var layer = go.AddComponent<Live2DCanvasLayer>();
        layer.Build(modelRoot, sourceCamera, worldDisplayPosition);
        return layer;
    }

    private void Build(GameObject modelRoot, Camera sourceCamera, Vector3 worldDisplayPosition)
    {
        _sourceCamera = sourceCamera;

        // 1) 角色搬到远端舞台（保持世界朝向/缩放），并脱离原父物体，避免对话移动影响它
        var stageIndex = _nextStageIndex++;
        _stageCenter = new Vector3(StageSpacing * (stageIndex + 1), 0f, 0f);

        _originalCameraOffset = sourceCamera != null
            ? worldDisplayPosition - sourceCamera.transform.position
            : Vector3.zero;

        modelRoot.transform.SetParent(transform, true);
        modelRoot.transform.position = _stageCenter;

        // 2) 全屏 RawImage
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        var imageGo = new GameObject("Live2D", typeof(RectTransform));
        imageGo.transform.SetParent(canvasGo.transform, false);

        _image = imageGo.AddComponent<RawImage>();
        _image.raycastTarget = false;

        var rect = _image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 3) 渲染相机：用主相机的参数，但站在远端舞台对应位置 —— 画面里角色的屏幕位置跟对话相机一致
        var camGo = new GameObject("Camera", typeof(Camera));
        camGo.transform.SetParent(transform, false);

        _renderCamera = camGo.GetComponent<Camera>();
        _renderCamera.orthographic = true;
        _renderCamera.clearFlags = CameraClearFlags.SolidColor;
        _renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _renderCamera.cullingMask = ~0;
        _renderCamera.allowHDR = false;
        _renderCamera.allowMSAA = false;
        _renderCamera.useOcclusionCulling = false;
        _renderCamera.depth = 90f;

        var data = _renderCamera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.SetRenderer(1);

        EnsureRenderTexture(Mathf.Max(Screen.width, 128), Mathf.Max(Screen.height, 128));
        CopyCameraSettings();
    }

    private void LateUpdate()
    {
        CopyCameraSettings();
        EnsureRenderTexture(Mathf.Max(Screen.width, 128), Mathf.Max(Screen.height, 128));
    }

    private void CopyCameraSettings()
    {
        if (_renderCamera == null) return;

        if (_sourceCamera != null)
        {
            _renderCamera.transform.SetPositionAndRotation(
                _stageCenter - _originalCameraOffset,
                _sourceCamera.transform.rotation);

            _renderCamera.orthographic = _sourceCamera.orthographic;
            _renderCamera.orthographicSize = _sourceCamera.orthographicSize;
            _renderCamera.fieldOfView = _sourceCamera.fieldOfView;
            _renderCamera.nearClipPlane = _sourceCamera.nearClipPlane;
            _renderCamera.farClipPlane = _sourceCamera.farClipPlane;
            _renderCamera.rect = _sourceCamera.rect;
        }
        else
        {
            _renderCamera.transform.SetPositionAndRotation(_stageCenter - Vector3.forward * 10f, Quaternion.identity);
            _renderCamera.orthographicSize = 10f;
        }
    }

    private void EnsureRenderTexture(int width, int height)
    {
        if (!Application.isPlaying) return;

        width = Mathf.Clamp(width, 64, 2048);
        height = Mathf.Clamp(height, 64, 2048);

        if (_rt != null && Mathf.Abs(width - _rtW) < 8 && Mathf.Abs(height - _rtH) < 8) return;

        if (_rt != null)
        {
            if (_renderCamera != null) _renderCamera.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
        }

        _rtW = width;
        _rtH = height;
        _rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "Live2D Screen Layer RT",
            filterMode = FilterMode.Bilinear
        };
        _rt.Create();

        if (_renderCamera != null) _renderCamera.targetTexture = _rt;
        if (_image != null) _image.texture = _rt;
    }

    private void OnDestroy()
    {
        if (_renderCamera != null) _renderCamera.targetTexture = null;

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }
}
