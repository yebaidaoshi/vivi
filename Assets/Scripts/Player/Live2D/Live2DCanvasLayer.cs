using System.Collections.Generic;
using Live2D.Cubism.Rendering;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 对话用 Live2D 的"屏幕层"：把角色渲染到 RenderTexture，再用一张全屏 RawImage 贴回屏幕。
/// 淡入淡出作用在整张图的 alpha 上，不会出现 Cubism 逐 drawable 透明导致的"重叠部位透底"。
///
/// 隔离：这个 SDK 的 Cubism 渲染 pass 不按相机 cullingMask 过滤模型，所以每台 RT 相机都会
/// 把所有 L2D 角色画进去。这里在每台 RT 相机的 OnPreCull 里临时关掉"其它角色"的
/// CubismRenderController，OnPostRender 再恢复 —— 动画更新在 LateUpdate 已经跑完，不受影响。
/// </summary>
public class Live2DCanvasLayer : MonoBehaviour
{
    private const string HudLayerName = "L2D";

    [Tooltip("UI 排序层：比对话 UI 小一点，保证立绘在对话框下面")]
    public int canvasSortingOrder = -1;

    private static readonly List<Live2DCanvasLayer> All = new List<Live2DCanvasLayer>();

    private Camera _renderCamera;
    private Camera _sourceCamera;
    private RenderTexture _rt;
    private RawImage _image;
    private CubismRenderController _modelController;
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

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    /// <summary>只让"自己"这一层参与渲染（其它层临时关掉）。</summary>
    internal void SetRendering(bool on)
    {
        if (_modelController != null) _modelController.enabled = on;
    }

    /// <summary>只让 owner 这一层参与渲染（其它层关掉）。</summary>
    internal static void PrepareForOwner(Live2DCanvasLayer owner)
    {
        for (var i = 0; i < All.Count; i++)
        {
            var layer = All[i];

            if (layer != null) layer.SetRendering(layer == owner);
        }
    }

    /// <summary>所有屏幕层角色都不参与渲染（主相机渲染时用）。</summary>
    internal static void HideAll()
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i] != null) All[i].SetRendering(false);
        }
    }

    private void Build(GameObject modelRoot, Camera sourceCamera)
    {
        _sourceCamera = sourceCamera;
        _modelController = modelRoot.GetComponent<CubismRenderController>();

        // 默认关掉：主相机的 Cubism pass 不看 cullingMask，只有关掉 controller 才能不让主相机画到它。
        // 具体哪台相机能画，由 Live2DCameraIsolation 的 OnPreCull/OnPostRender 控制。
        if (_modelController != null) _modelController.enabled = false;

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

        // 隔离：这台相机渲染时，别的角色先关掉
        var isolation = camGo.AddComponent<Live2DCameraIsolation>();
        isolation.Owner = this;
        isolation.TargetCamera = _renderCamera;

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
        if (_modelController != null) _modelController.enabled = true;

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

/// <summary>
/// 挂在屏幕层的 RT 相机上：URP 不会回调 OnPreCull/OnPostRender，
/// 所以这里用 RenderPipelineManager.beginCameraRendering / endCameraRendering 来切换渲染权。
/// </summary>
public sealed class Live2DCameraIsolation : MonoBehaviour
{
    public Live2DCanvasLayer Owner;
    public Camera TargetCamera;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
    }

    private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == TargetCamera)
        {
            Live2DCanvasLayer.PrepareForOwner(Owner);
        }
        else
        {
            // 其它相机（主相机等）渲染时，对话立绘一个都不许被 pass 画出来
            Live2DCanvasLayer.HideAll();
        }
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == TargetCamera)
        {
            Live2DCanvasLayer.HideAll();
        }
    }
}
