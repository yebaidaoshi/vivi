using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 对话用 Live2D 的"屏幕层"：把角色渲染到 RenderTexture，再用一张全屏 RawImage 贴回屏幕。
/// 这样淡入淡出作用在整张图的 alpha 上，不会出现 Cubism 逐 drawable 透明导致的"重叠部位透底"，
/// 也不需要把角色移出屏幕才能收场。
/// </summary>
public class Live2DCanvasLayer : MonoBehaviour
{
    private const string HudLayerName = "L2D";

    [Tooltip("UI 排序层：比对话 UI 小一点，保证立绘在对话框下面")]
    public int canvasSortingOrder = -1;

    private Camera _renderCamera;
    private Camera _sourceCamera;
    private RenderTexture _rt;
    private RawImage _image;
    private int _rtW;
    private int _rtH;
    private int _layer = -1;
    private int _savedCullingMask;
    private bool _maskSaved;

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

    /// <summary>给某个对话角色挂一层屏幕层。sourceCamera 一般是 Camera.main（对话用的那台相机）。</summary>
    public static Live2DCanvasLayer Attach(GameObject modelRoot, Camera sourceCamera)
    {
        if (modelRoot == null) return null;

        var go = new GameObject("[Live2D Screen Layer]");
        var layer = go.AddComponent<Live2DCanvasLayer>();
        layer.Build(modelRoot, sourceCamera);
        return layer;
    }

    private void Build(GameObject modelRoot, Camera sourceCamera)
    {
        _sourceCamera = sourceCamera;

        // 1) 模型换到 L2D 层，并从主相机里排除掉（由本层的相机单独渲染）
        _layer = LayerMask.NameToLayer(HudLayerName);

        if (_layer < 0)
        {
            _layer = 0;
            Debug.LogWarning($"[Live2D] 找不到层 \"{HudLayerName}\"，对话立绘的屏幕层会渲染 Default 层。");
        }

        SetLayerRecursively(modelRoot, _layer);

        if (_sourceCamera != null && _layer > 0)
        {
            _savedCullingMask = _sourceCamera.cullingMask;
            _sourceCamera.cullingMask = _savedCullingMask & ~(1 << _layer);
            _maskSaved = true;
        }

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

        // 3) 渲染相机（只画 L2D 层，背景透明）
        var camGo = new GameObject("Camera", typeof(Camera));
        camGo.transform.SetParent(transform, false);

        _renderCamera = camGo.GetComponent<Camera>();
        _renderCamera.orthographic = true;
        _renderCamera.clearFlags = CameraClearFlags.SolidColor;
        _renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _renderCamera.cullingMask = 1 << _layer;
        _renderCamera.allowHDR = false;
        _renderCamera.allowMSAA = false;
        _renderCamera.useOcclusionCulling = false;
        _renderCamera.depth = 90f;

        var data = _renderCamera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.SetRenderer(1);

        EnsureRenderTexture(Mathf.RoundToInt(Screen.width * 0.5f), Mathf.RoundToInt(Screen.height * 0.5f));
        CopyCameraSettings();
    }

    private void LateUpdate()
    {
        CopyCameraSettings();
        EnsureRenderTexture(Mathf.RoundToInt(Screen.width * 0.5f), Mathf.RoundToInt(Screen.height * 0.5f));
    }

    private void CopyCameraSettings()
    {
        if (_renderCamera == null || _sourceCamera == null) return;

        _renderCamera.transform.SetPositionAndRotation(
            _sourceCamera.transform.position,
            _sourceCamera.transform.rotation);

        _renderCamera.orthographic = _sourceCamera.orthographic;
        _renderCamera.orthographicSize = _sourceCamera.orthographicSize;
        _renderCamera.fieldOfView = _sourceCamera.fieldOfView;
        _renderCamera.nearClipPlane = _sourceCamera.nearClipPlane;
        _renderCamera.farClipPlane = _sourceCamera.farClipPlane;
        _renderCamera.rect = _sourceCamera.rect;
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

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;

        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void OnDestroy()
    {
        if (_maskSaved && _sourceCamera != null)
        {
            _sourceCamera.cullingMask = _savedCullingMask;
        }

        if (_renderCamera != null) _renderCamera.targetTexture = null;

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }
}
