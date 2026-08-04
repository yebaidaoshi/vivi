using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-zoom-to-fit/")]
	public class ProCamera2DZoomToFitTargets : BasePC2D, ISizeOverrider
	{
		public static string ExtensionName = "Zoom To Fit";

		public float ZoomOutBorder = 0.6f;

		public float ZoomInBorder = 0.4f;

		public float ZoomInSmoothness = 2f;

		public float ZoomOutSmoothness = 1f;

		public float MaxZoomInAmount = 2f;

		public float MaxZoomOutAmount = 4f;

		public bool DisableWhenOneTarget = true;

		public bool CompensateForCameraPosition;

		private float _zoomVelocity;

		private float _previousCamSize;

		private float _targetCamSize;

		private float _targetCamSizeSmoothed;

		private float _minCameraSize;

		private float _maxCameraSize;

		private int _soOrder;

		public int SOOrder
		{
			get
			{
				return _soOrder;
			}
			set
			{
				_soOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (!(base.ProCamera2D == null))
			{
				_targetCamSize = base.ProCamera2D.StartScreenSizeInWorldCoordinates.y * 0.5f;
				_targetCamSizeSmoothed = _targetCamSize;
				base.ProCamera2D.AddSizeOverrider(this);
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemoveSizeOverrider(this);
			}
		}

		public float OverrideSize(float deltaTime, float originalSize)
		{
			if (!base.enabled)
			{
				return originalSize;
			}
			_targetCamSizeSmoothed = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			if (DisableWhenOneTarget && base.ProCamera2D.CameraTargets.Count <= 1)
			{
				_targetCamSize = base.ProCamera2D.StartScreenSizeInWorldCoordinates.y * 0.5f;
			}
			else
			{
				if (_previousCamSize == base.ProCamera2D.ScreenSizeInWorldCoordinates.y)
				{
					_targetCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
					_targetCamSizeSmoothed = _targetCamSize;
					_zoomVelocity = 0f;
				}
				UpdateTargetCamSize();
			}
			_previousCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y;
			return _targetCamSizeSmoothed = Mathf.SmoothDamp(_targetCamSizeSmoothed, _targetCamSize, ref _zoomVelocity, (_targetCamSize < _targetCamSizeSmoothed) ? ZoomInSmoothness : ZoomOutSmoothness, float.MaxValue, deltaTime);
		}

		public override void OnReset()
		{
			_zoomVelocity = 0f;
			_previousCamSize = base.ProCamera2D.StartScreenSizeInWorldCoordinates.y * 0.5f;
			_targetCamSize = _previousCamSize;
			_targetCamSizeSmoothed = _previousCamSize;
		}

		private void UpdateTargetCamSize()
		{
			float num = float.NegativeInfinity;
			float num2 = float.PositiveInfinity;
			float num3 = float.NegativeInfinity;
			float num4 = float.PositiveInfinity;
			for (int i = 0; i < base.ProCamera2D.CameraTargets.Count; i++)
			{
				Vector2 vector = new Vector2(Vector3H(base.ProCamera2D.CameraTargets[i].TargetPosition) + base.ProCamera2D.CameraTargets[i].TargetOffset.x, Vector3V(base.ProCamera2D.CameraTargets[i].TargetPosition) + base.ProCamera2D.CameraTargets[i].TargetOffset.y);
				num = ((vector.x > num) ? vector.x : num);
				num2 = ((vector.x < num2) ? vector.x : num2);
				num3 = ((vector.y > num3) ? vector.y : num3);
				num4 = ((vector.y < num4) ? vector.y : num4);
			}
			float num5 = Mathf.Abs(num - num2);
			float num6 = Mathf.Abs(num3 - num4);
			if (CompensateForCameraPosition)
			{
				num5 += Mathf.Abs(Vector3H(base.ProCamera2D.TargetsMidPoint) - Vector3H(base.ProCamera2D.LocalPosition)) * 2f;
				num6 += Mathf.Abs(Vector3V(base.ProCamera2D.TargetsMidPoint) - Vector3V(base.ProCamera2D.LocalPosition)) * 2f;
			}
			num5 *= 0.5f;
			num6 *= 0.5f;
			if (num5 > base.ProCamera2D.ScreenSizeInWorldCoordinates.x * ZoomOutBorder * 0.5f || num6 > base.ProCamera2D.ScreenSizeInWorldCoordinates.y * ZoomOutBorder * 0.5f)
			{
				if (num5 / base.ProCamera2D.ScreenSizeInWorldCoordinates.x >= num6 / base.ProCamera2D.ScreenSizeInWorldCoordinates.y)
				{
					_targetCamSize = num5 / base.ProCamera2D.GameCamera.aspect / ZoomOutBorder;
				}
				else
				{
					_targetCamSize = num6 / ZoomOutBorder;
				}
			}
			else if (num5 < base.ProCamera2D.ScreenSizeInWorldCoordinates.x * ZoomInBorder * 0.5f && num6 < base.ProCamera2D.ScreenSizeInWorldCoordinates.y * ZoomInBorder * 0.5f)
			{
				if (num5 / base.ProCamera2D.ScreenSizeInWorldCoordinates.x >= num6 / base.ProCamera2D.ScreenSizeInWorldCoordinates.y)
				{
					_targetCamSize = num5 / base.ProCamera2D.GameCamera.aspect / ZoomInBorder;
				}
				else
				{
					_targetCamSize = num6 / ZoomInBorder;
				}
			}
			_minCameraSize = base.ProCamera2D.StartScreenSizeInWorldCoordinates.y * 0.5f / MaxZoomInAmount;
			_maxCameraSize = base.ProCamera2D.StartScreenSizeInWorldCoordinates.y * 0.5f * MaxZoomOutAmount;
			_targetCamSize = Mathf.Clamp(_targetCamSize, _minCameraSize, _maxCameraSize);
		}
	}
}
