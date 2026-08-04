using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-content-fitter/")]
	public class ProCamera2DContentFitter : BasePC2D, ISizeOverrider
	{
		public static string ExtensionName = "Content Fitter";

		[SerializeField]
		private ContentFitterMode _contentFitterMode;

		[SerializeField]
		private bool _useLetterOrPillarboxing;

		[SerializeField]
		private float _targetHeight = 5.625f;

		[SerializeField]
		private float _targetWidth = 10f;

		[Range(0.1f, 3f)]
		[SerializeField]
		private float _targetAspectRatio = 1.7777778f;

		[Range(-1f, 1f)]
		public float VerticalAlignment;

		[Range(-1f, 1f)]
		public float HorizontalAlignment;

		private float _prevTargetHeight;

		private float _prevTargetWidth;

		private float _prevTargetAspectRatio;

		private float _prevAspectRatio;

		private float _prevVerticalAlignment;

		private float _prevHorizontalAlignment;

		private bool _prevUseLetterOrPillarboxing;

		private Camera _letterPillarboxingCamera;

		private int _soOrder = 5000;

		public ContentFitterMode ContentFitterMode
		{
			get
			{
				return _contentFitterMode;
			}
			set
			{
				_contentFitterMode = value;
				base.ProCamera2D.GameCamera.ResetProjectionMatrix();
				if (_contentFitterMode == ContentFitterMode.AspectRatio)
				{
					TargetWidth = TargetHeight * TargetAspectRatio;
				}
			}
		}

		public bool UseLetterOrPillarboxing
		{
			get
			{
				return _useLetterOrPillarboxing;
			}
			set
			{
				_useLetterOrPillarboxing = value;
				ToggleLetterPillarboxing(value);
			}
		}

		private static float ScreenAspectRatio => (float)Screen.width / (float)Screen.height;

		public float TargetHeight
		{
			get
			{
				return _targetHeight;
			}
			set
			{
				_targetHeight = value;
				_targetWidth = ((ContentFitterMode == ContentFitterMode.AspectRatio) ? (value * TargetAspectRatio) : (value * ScreenAspectRatio));
			}
		}

		public float TargetWidth
		{
			get
			{
				return _targetWidth;
			}
			set
			{
				_targetWidth = value;
				_targetHeight = ((ContentFitterMode == ContentFitterMode.AspectRatio) ? (value / TargetAspectRatio) : (value / ScreenAspectRatio));
			}
		}

		public float TargetAspectRatio
		{
			get
			{
				return _targetAspectRatio;
			}
			set
			{
				_targetAspectRatio = value;
				_targetWidth = _targetHeight * value;
			}
		}

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
			base.ProCamera2D.AddSizeOverrider(this);
		}

		private IEnumerator Start()
		{
			if (UseLetterOrPillarboxing)
			{
				CreateLetterPillarboxingCamera();
			}
			yield return null;
			if (ContentFitterMode == ContentFitterMode.AspectRatio)
			{
				UpdateCameraAlignment(base.ProCamera2D.GameCamera, TargetHeight * 0.5f > TargetWidth * 0.5f / base.ProCamera2D.GameCamera.aspect, TargetAspectRatio, HorizontalAlignment, VerticalAlignment);
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
			if (base.enabled)
			{
				return GetSize(ContentFitterMode);
			}
			return originalSize;
		}

		private float GetSize(ContentFitterMode mode)
		{
			switch (mode)
			{
			case ContentFitterMode.FixedHeight:
				return TargetHeight * 0.5f;
			case ContentFitterMode.FixedWidth:
				return TargetWidth * 0.5f / base.ProCamera2D.GameCamera.aspect;
			case ContentFitterMode.AspectRatio:
				if (_prevTargetWidth != TargetWidth || _prevTargetHeight != TargetHeight || _prevTargetAspectRatio != TargetAspectRatio || _prevAspectRatio != base.ProCamera2D.GameCamera.aspect || _prevVerticalAlignment != VerticalAlignment || _prevHorizontalAlignment != HorizontalAlignment || _prevUseLetterOrPillarboxing != _useLetterOrPillarboxing)
				{
					StartCoroutine(UpdateFixedAspectRatio());
				}
				_prevTargetWidth = TargetWidth;
				_prevTargetHeight = TargetHeight;
				_prevTargetAspectRatio = TargetAspectRatio;
				_prevAspectRatio = base.ProCamera2D.GameCamera.aspect;
				_prevVerticalAlignment = VerticalAlignment;
				_prevHorizontalAlignment = HorizontalAlignment;
				_prevUseLetterOrPillarboxing = _useLetterOrPillarboxing;
				return Mathf.Max(TargetHeight * 0.5f, TargetWidth * 0.5f / base.ProCamera2D.GameCamera.aspect);
			default:
				throw new ArgumentOutOfRangeException();
			}
		}

		private IEnumerator UpdateFixedAspectRatio()
		{
			bool isPillarbox = TargetHeight * 0.5f > TargetWidth * 0.5f / ScreenAspectRatio;
			if (_prevUseLetterOrPillarboxing != _useLetterOrPillarboxing)
			{
				ToggleLetterPillarboxing(_useLetterOrPillarboxing);
			}
			if (UseLetterOrPillarboxing)
			{
				UpdateLetterPillarbox(base.ProCamera2D.GameCamera, isPillarbox, TargetAspectRatio, HorizontalAlignment, VerticalAlignment);
			}
			yield return new WaitForEndOfFrame();
			UpdateCameraAlignment(base.ProCamera2D.GameCamera, isPillarbox, TargetAspectRatio, HorizontalAlignment, VerticalAlignment);
		}

		private static void UpdateCameraAlignment(Camera cam, bool isPillarbox, float targetAspectRatio, float horizontalAlignment, float verticalAlignment)
		{
			cam.ResetProjectionMatrix();
			float x = (isPillarbox ? ((-0.5f + targetAspectRatio / cam.aspect * 0.5f) * horizontalAlignment) : 0f);
			float y = ((!isPillarbox) ? ((-0.5f + cam.aspect / targetAspectRatio * 0.5f) * verticalAlignment) : 0f);
			cam.projectionMatrix = GetScissorRect(new Rect(x, y, 1f, 1f), cam.projectionMatrix);
		}

		private static Matrix4x4 GetScissorRect(Rect targetScissor, Matrix4x4 camProjectionMatrix)
		{
			Matrix4x4 matrix4x = Matrix4x4.TRS(new Vector3(1f / targetScissor.width - 1f, 1f / targetScissor.height - 1f, 0f), Quaternion.identity, new Vector3(1f / targetScissor.width, 1f / targetScissor.height, 1f));
			return Matrix4x4.TRS(new Vector3((0f - targetScissor.x) * 2f / targetScissor.width, (0f - targetScissor.y) * 2f / targetScissor.height, 0f), Quaternion.identity, Vector3.one) * matrix4x * camProjectionMatrix;
		}

		private static void UpdateLetterPillarbox(Camera cam, bool isPillarbox, float targetAspectRatio, float horizontalAlignment, float verticalAlignment)
		{
			if (isPillarbox)
			{
				float num = 1f - targetAspectRatio / ((float)Screen.width / (float)Screen.height);
				cam.rect = new Rect(num / 2f + num / 2f * horizontalAlignment, 0f, 1f - num, 1f);
			}
			else
			{
				float num2 = 1f - (float)Screen.width / (float)Screen.height / targetAspectRatio;
				cam.rect = new Rect(0f, num2 / 2f + num2 / 2f * verticalAlignment, 1f, 1f - num2);
			}
		}

		private void ToggleLetterPillarboxing(bool value)
		{
			if (value && _letterPillarboxingCamera == null)
			{
				CreateLetterPillarboxingCamera();
			}
			if (value)
			{
				_letterPillarboxingCamera.gameObject.SetActive(value: true);
				UpdateLetterPillarbox(base.ProCamera2D.GameCamera, TargetHeight * 0.5f > TargetWidth * 0.5f / ScreenAspectRatio, TargetAspectRatio, HorizontalAlignment, VerticalAlignment);
				return;
			}
			if (_letterPillarboxingCamera != null)
			{
				_letterPillarboxingCamera.gameObject.SetActive(value: false);
			}
			base.ProCamera2D.GameCamera.rect = new Rect(0f, 0f, 1f, 1f);
			UpdateCameraAlignment(base.ProCamera2D.GameCamera, TargetHeight * 0.5f > TargetWidth * 0.5f / ScreenAspectRatio, TargetAspectRatio, HorizontalAlignment, VerticalAlignment);
		}

		private void CreateLetterPillarboxingCamera()
		{
			_letterPillarboxingCamera = new GameObject("PC2DBackgroundCamera", typeof(Camera)).GetComponent<Camera>();
			_letterPillarboxingCamera.depth = -2.1474836E+09f;
			_letterPillarboxingCamera.clearFlags = CameraClearFlags.Color;
			_letterPillarboxingCamera.backgroundColor = Color.black;
			_letterPillarboxingCamera.cullingMask = 0;
			_letterPillarboxingCamera.transform.position = new Vector3(10000f, 10000f, 10000f);
			_letterPillarboxingCamera.gameObject.hideFlags = HideFlags.HideInHierarchy;
		}

		private Vector3[] DrawGizmoRectangle(float x, float y, float width, float height, Color fillColor, Color borderColor)
		{
			Rect rect = new Rect(x, y, width, height);
			rect.x -= rect.width / 2f;
			rect.y -= rect.height / 2f;
			return new Vector3[4]
			{
				VectorHVD(rect.position.x, rect.position.y, 0f),
				VectorHVD(rect.position.x + rect.width, rect.position.y, 0f),
				VectorHVD(rect.position.x + rect.width, rect.position.y + rect.height, 0f),
				VectorHVD(rect.position.x, rect.position.y + rect.height, 0f)
			};
		}
	}
}
