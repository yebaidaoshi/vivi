using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-camera-window/")]
	public class ProCamera2DCameraWindow : BasePC2D, IPositionDeltaChanger
	{
		public static string ExtensionName = "Camera Window";

		public Rect CameraWindowRect = new Rect(0f, 0f, 0.3f, 0.3f);

		private Rect _cameraWindowRectInWorldCoords;

		public bool IsRelativeSizeAndPosition = true;

		private int _pdcOrder;

		public int PDCOrder
		{
			get
			{
				return _pdcOrder;
			}
			set
			{
				_pdcOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			base.ProCamera2D.AddPositionDeltaChanger(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePositionDeltaChanger(this);
			}
		}

		public Vector3 AdjustDelta(float deltaTime, Vector3 originalDelta)
		{
			if (!base.enabled)
			{
				return originalDelta;
			}
			_cameraWindowRectInWorldCoords = GetRectAroundTransf(CameraWindowRect, base.ProCamera2D.ScreenSizeInWorldCoordinates, _transform, IsRelativeSizeAndPosition);
			float arg = 0f;
			if (base.ProCamera2D.CameraTargetPositionSmoothed.x >= _cameraWindowRectInWorldCoords.x + _cameraWindowRectInWorldCoords.width)
			{
				arg = base.ProCamera2D.CameraTargetPositionSmoothed.x - (Vector3H(_transform.localPosition) + _cameraWindowRectInWorldCoords.width / 2f + CameraWindowRect.x * (IsRelativeSizeAndPosition ? base.ProCamera2D.ScreenSizeInWorldCoordinates.x : 1f));
			}
			else if (base.ProCamera2D.CameraTargetPositionSmoothed.x <= _cameraWindowRectInWorldCoords.x)
			{
				arg = base.ProCamera2D.CameraTargetPositionSmoothed.x - (Vector3H(_transform.localPosition) - _cameraWindowRectInWorldCoords.width / 2f + CameraWindowRect.x * (IsRelativeSizeAndPosition ? base.ProCamera2D.ScreenSizeInWorldCoordinates.x : 1f));
			}
			float arg2 = 0f;
			if (base.ProCamera2D.CameraTargetPositionSmoothed.y >= _cameraWindowRectInWorldCoords.y + _cameraWindowRectInWorldCoords.height)
			{
				arg2 = base.ProCamera2D.CameraTargetPositionSmoothed.y - (Vector3V(_transform.localPosition) + _cameraWindowRectInWorldCoords.height / 2f + CameraWindowRect.y * (IsRelativeSizeAndPosition ? base.ProCamera2D.ScreenSizeInWorldCoordinates.y : 1f));
			}
			else if (base.ProCamera2D.CameraTargetPositionSmoothed.y <= _cameraWindowRectInWorldCoords.y)
			{
				arg2 = base.ProCamera2D.CameraTargetPositionSmoothed.y - (Vector3V(_transform.localPosition) - _cameraWindowRectInWorldCoords.height / 2f + CameraWindowRect.y * (IsRelativeSizeAndPosition ? base.ProCamera2D.ScreenSizeInWorldCoordinates.y : 1f));
			}
			return VectorHV(arg, arg2);
		}

		private Rect GetRectAroundTransf(Rect rectNormalized, Vector2 rectSize, Transform transf, bool isRelative)
		{
			Vector2 vector = Vector2.Scale(new Vector2(rectNormalized.width, rectNormalized.height), isRelative ? rectSize : Vector2.one);
			float x = Vector3H(transf.localPosition) - vector.x / 2f + rectNormalized.x * (isRelative ? rectSize.x : 1f);
			float y = Vector3V(transf.localPosition) - vector.y / 2f + rectNormalized.y * (isRelative ? rectSize.y : 1f);
			return new Rect(x, y, vector.x, vector.y);
		}
	}
}
