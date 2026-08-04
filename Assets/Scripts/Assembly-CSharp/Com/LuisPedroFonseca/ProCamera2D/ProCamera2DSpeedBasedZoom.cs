using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-speed-based-zoom/")]
	public class ProCamera2DSpeedBasedZoom : BasePC2D, ISizeDeltaChanger
	{
		public static string ExtensionName = "Speed Based Zoom";

		[Tooltip("The speed at which the camera will reach it's max zoom out.")]
		public float CamVelocityForZoomOut = 5f;

		[Tooltip("Below this speed the camera zooms in. Above this speed the camera will start zooming out.")]
		public float CamVelocityForZoomIn = 2f;

		[Tooltip("Represents how smooth the zoom in of the camera should be. The lower the number the quickest the zoom is.")]
		[Range(0f, 3f)]
		public float ZoomInSmoothness = 1f;

		[Tooltip("Represents how smooth the zoom out of the camera should be. The lower the number the quickest the zoom is.")]
		[Range(0f, 3f)]
		public float ZoomOutSmoothness = 1f;

		[Tooltip("Represents the maximum amount the camera should zoom in when the camera speed is below SpeedForZoomIn")]
		public float MaxZoomInAmount = 2f;

		[Tooltip("Represents the maximum amount the camera should zoom out when the camera speed is equal to SpeedForZoomOut")]
		public float MaxZoomOutAmount = 2f;

		private float _zoomVelocity;

		private float _initialCamSize;

		private float _previousCamSize;

		private Vector3 _previousCameraPosition;

		[HideInInspector]
		public float CurrentVelocity;

		private int _sdcOrder = 1000;

		public int SDCOrder
		{
			get
			{
				return _sdcOrder;
			}
			set
			{
				_sdcOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (!(base.ProCamera2D == null))
			{
				_initialCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
				_previousCamSize = _initialCamSize;
				_previousCameraPosition = VectorHV(Vector3H(base.ProCamera2D.LocalPosition), Vector3V(base.ProCamera2D.LocalPosition));
				base.ProCamera2D.AddSizeDeltaChanger(this);
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemoveSizeDeltaChanger(this);
			}
		}

		public float AdjustSize(float deltaTime, float originalDelta)
		{
			if (!base.enabled)
			{
				return originalDelta;
			}
			if (_previousCamSize == base.ProCamera2D.ScreenSizeInWorldCoordinates.y)
			{
				_zoomVelocity = 0f;
			}
			CurrentVelocity = (_previousCameraPosition - VectorHV(Vector3H(base.ProCamera2D.LocalPosition), Vector3V(base.ProCamera2D.LocalPosition))).magnitude / deltaTime;
			_previousCameraPosition = VectorHV(Vector3H(base.ProCamera2D.LocalPosition), Vector3V(base.ProCamera2D.LocalPosition));
			float num = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			float num2 = num;
			if (CurrentVelocity > CamVelocityForZoomIn)
			{
				float value = (CurrentVelocity - CamVelocityForZoomIn) / (CamVelocityForZoomOut - CamVelocityForZoomIn);
				float num3 = _initialCamSize * (1f + MaxZoomOutAmount - 1f) * Mathf.Clamp01(value);
				if (num3 > num)
				{
					num2 = num3;
				}
			}
			else
			{
				float num4 = (1f - CurrentVelocity / CamVelocityForZoomIn).Remap(0f, 1f, 0.5f, 1f);
				float num5 = _initialCamSize / (MaxZoomInAmount * num4);
				if (num5 < num)
				{
					num2 = num5;
				}
			}
			if (Mathf.Abs(num - num2) > 0.0001f)
			{
				float smoothTime = ((num2 < num) ? ZoomInSmoothness : ZoomOutSmoothness);
				num2 = Mathf.SmoothDamp(num, num2, ref _zoomVelocity, smoothTime, float.PositiveInfinity, deltaTime);
			}
			float num6 = num2 - base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			_previousCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y;
			return originalDelta + num6;
		}

		public override void OnReset()
		{
			_previousCamSize = _initialCamSize;
			_previousCameraPosition = VectorHV(Vector3H(base.ProCamera2D.LocalPosition), Vector3V(base.ProCamera2D.LocalPosition));
			_zoomVelocity = 0f;
		}
	}
}
