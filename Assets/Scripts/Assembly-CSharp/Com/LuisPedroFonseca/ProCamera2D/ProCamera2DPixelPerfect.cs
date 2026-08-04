using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-pixel-perfect/")]
	public class ProCamera2DPixelPerfect : BasePC2D, IPositionOverrider
	{
		public static string ExtensionName = "Pixel Perfect";

		public float PixelsPerUnit = 32f;

		public AutoScaleMode ViewportAutoScale = AutoScaleMode.Round;

		public Vector2 TargetViewportSizeInPixels = new Vector2(80f, 50f);

		[Range(1f, 32f)]
		[SerializeField]
		private int _zoom = 1;

		public bool SnapMovementToGrid;

		public bool SnapCameraToGrid = true;

		public bool DrawGrid;

		public Color GridColor = new Color(1f, 0f, 0f, 0.1f);

		public float GridDensity;

		private float _pixelStep = -1f;

		private Transform _parent;

		private int _poOrder = 2000;

		public int Zoom
		{
			get
			{
				return _zoom;
			}
			set
			{
				_zoom = value;
				ResizeCameraToPixelPerfect();
			}
		}

		public float ViewportScale { get; private set; }

		public float PixelStep => _pixelStep;

		public int POOrder
		{
			get
			{
				return _poOrder;
			}
			set
			{
				_poOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (!base.ProCamera2D.GameCamera.orthographic)
			{
				base.enabled = false;
				return;
			}
			ResizeCameraToPixelPerfect();
			base.ProCamera2D.AddPositionOverrider(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePositionOverrider(this);
			}
		}

		public Vector3 OverridePosition(float deltaTime, Vector3 originalPosition)
		{
			if (!base.enabled)
			{
				return originalPosition;
			}
			float gridSize = _pixelStep;
			if (SnapMovementToGrid && !SnapCameraToGrid)
			{
				gridSize = 1f / (PixelsPerUnit * (ViewportScale + (float)Zoom - 1f));
			}
			_parent = _transform.parent;
			if (_parent != null && _parent.position != Vector3.zero)
			{
				_parent.position = VectorHVD(Utils.AlignToGrid(Vector3H(_parent.position), gridSize), Utils.AlignToGrid(Vector3V(_parent.position), gridSize), Vector3D(_parent.position));
			}
			return VectorHVD(Utils.AlignToGrid(Vector3H(originalPosition), gridSize), Utils.AlignToGrid(Vector3V(originalPosition), gridSize), 0f);
		}

		public void ResizeCameraToPixelPerfect()
		{
			ViewportScale = CalculateViewportScale();
			_pixelStep = CalculatePixelStep(ViewportScale);
			float newSize = (float)base.ProCamera2D.GameCamera.pixelHeight * 0.5f * (1f / PixelsPerUnit) / (ViewportScale + (float)Zoom - 1f);
			base.ProCamera2D.UpdateScreenSize(newSize);
		}

		public float CalculateViewportScale()
		{
			if (ViewportAutoScale == AutoScaleMode.None)
			{
				return 1f;
			}
			float num = (float)base.ProCamera2D.GameCamera.pixelWidth / TargetViewportSizeInPixels.x;
			float num2 = (float)base.ProCamera2D.GameCamera.pixelHeight / TargetViewportSizeInPixels.y;
			float num3 = ((num > num2) ? num2 : num);
			switch (ViewportAutoScale)
			{
			case AutoScaleMode.Floor:
				num3 = Mathf.Floor(num3);
				break;
			case AutoScaleMode.Ceil:
				num3 = Mathf.Ceil(num3);
				break;
			case AutoScaleMode.Round:
				num3 = Mathf.Round(num3);
				break;
			}
			if (num3 < 1f)
			{
				num3 = 1f;
			}
			return num3;
		}

		private float CalculatePixelStep(float viewportScale)
		{
			if (!SnapMovementToGrid)
			{
				return 1f / (PixelsPerUnit * (viewportScale + (float)Zoom - 1f));
			}
			return 1f / PixelsPerUnit;
		}
	}
}
