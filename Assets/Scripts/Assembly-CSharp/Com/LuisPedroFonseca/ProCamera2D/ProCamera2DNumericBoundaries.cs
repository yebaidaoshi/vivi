using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-numeric-boundaries/")]
	public class ProCamera2DNumericBoundaries : BasePC2D, IPositionDeltaChanger, ISizeOverrider
	{
		public static string ExtensionName = "Numeric Boundaries";

		public Action OnBoundariesTransitionStarted;

		public Action OnBoundariesTransitionFinished;

		public bool UseNumericBoundaries = true;

		public bool UseTopBoundary;

		public float TopBoundary = 10f;

		public float TargetTopBoundary;

		public bool UseBottomBoundary = true;

		public float BottomBoundary = -10f;

		public float TargetBottomBoundary;

		public bool UseLeftBoundary;

		public float LeftBoundary = -10f;

		public float TargetLeftBoundary;

		public bool UseRightBoundary;

		public float RightBoundary = 10f;

		public float TargetRightBoundary;

		public bool IsCameraPositionHorizontallyBounded;

		public bool IsCameraPositionVerticallyBounded;

		public Coroutine TopBoundaryAnimRoutine;

		public Coroutine BottomBoundaryAnimRoutine;

		public Coroutine LeftBoundaryAnimRoutine;

		public Coroutine RightBoundaryAnimRoutine;

		public ProCamera2DTriggerBoundaries CurrentBoundariesTrigger;

		public Coroutine MoveCameraToTargetRoutine;

		public bool HasFiredTransitionStarted;

		public bool HasFiredTransitionFinished;

		public bool UseSoftBoundaries = true;

		[Range(0f, 4f)]
		public float Softness = 0.5f;

		[Range(0f, 0.5f)]
		public float SoftAreaSize = 0.1f;

		private float _smoothnessVelX;

		private float _smoothnessVelY;

		private int _pdcOrder = 4000;

		private int _soOrder = 2000;

		public NumericBoundariesSettings Settings
		{
			get
			{
				return new NumericBoundariesSettings
				{
					UseNumericBoundaries = UseNumericBoundaries,
					UseTopBoundary = UseTopBoundary,
					TopBoundary = TopBoundary,
					UseBottomBoundary = UseBottomBoundary,
					BottomBoundary = BottomBoundary,
					UseLeftBoundary = UseLeftBoundary,
					LeftBoundary = LeftBoundary,
					UseRightBoundary = UseRightBoundary,
					RightBoundary = RightBoundary
				};
			}
			set
			{
				UseNumericBoundaries = value.UseNumericBoundaries;
				UseTopBoundary = value.UseTopBoundary;
				TopBoundary = value.TopBoundary;
				UseBottomBoundary = value.UseBottomBoundary;
				BottomBoundary = value.BottomBoundary;
				UseLeftBoundary = value.UseLeftBoundary;
				LeftBoundary = value.LeftBoundary;
				UseRightBoundary = value.UseRightBoundary;
				RightBoundary = value.RightBoundary;
			}
		}

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
			base.ProCamera2D.AddPositionDeltaChanger(this);
			base.ProCamera2D.AddSizeOverrider(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (!(base.ProCamera2D == null))
			{
				base.ProCamera2D.RemovePositionDeltaChanger(this);
				base.ProCamera2D.RemoveSizeOverrider(this);
			}
		}

		public Vector3 AdjustDelta(float deltaTime, Vector3 originalDelta)
		{
			if (!base.enabled || !UseNumericBoundaries)
			{
				return originalDelta;
			}
			IsCameraPositionHorizontallyBounded = false;
			base.ProCamera2D.IsCameraPositionLeftBounded = false;
			base.ProCamera2D.IsCameraPositionRightBounded = false;
			IsCameraPositionVerticallyBounded = false;
			base.ProCamera2D.IsCameraPositionTopBounded = false;
			base.ProCamera2D.IsCameraPositionBottomBounded = false;
			float num = Vector3H(base.ProCamera2D.LocalPosition) + Vector3H(originalDelta);
			float num2 = Vector3V(base.ProCamera2D.LocalPosition) + Vector3V(originalDelta);
			float num3 = base.ProCamera2D.ScreenSizeInWorldCoordinates.x * 0.5f;
			float num4 = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			float num5 = (UseSoftBoundaries ? (base.ProCamera2D.ScreenSizeInWorldCoordinates.x * SoftAreaSize) : 0f);
			float num6 = (UseSoftBoundaries ? (base.ProCamera2D.ScreenSizeInWorldCoordinates.y * SoftAreaSize) : 0f);
			if (UseLeftBoundary && num - num3 < LeftBoundary + num5)
			{
				if (UseSoftBoundaries)
				{
					num = ((!(Vector3H(originalDelta) <= 0f)) ? Mathf.Max(LeftBoundary + num3, num) : Mathf.SmoothDamp(Mathf.Max(LeftBoundary + num3, Vector3H(base.ProCamera2D.LocalPosition)), Mathf.Max(LeftBoundary + num3, num), ref _smoothnessVelX, (LeftBoundary + num3 - Vector3H(base.ProCamera2D.LocalPosition) + num5) / num5 * Softness));
				}
				else if (!UseSoftBoundaries)
				{
					num = LeftBoundary + num3;
				}
				IsCameraPositionHorizontallyBounded = true;
				base.ProCamera2D.IsCameraPositionLeftBounded = true;
			}
			if (UseRightBoundary && num + num3 > RightBoundary - num5)
			{
				if (UseSoftBoundaries)
				{
					num = ((!(Vector3H(originalDelta) >= 0f)) ? Mathf.Min(RightBoundary - num3, num) : Mathf.SmoothDamp(Mathf.Min(RightBoundary - num3, Vector3H(base.ProCamera2D.LocalPosition)), Mathf.Min(RightBoundary - num3, num), ref _smoothnessVelX, (Vector3H(base.ProCamera2D.LocalPosition) - (RightBoundary - num3) + num5) / num5 * Softness));
				}
				else if (!UseSoftBoundaries)
				{
					num = RightBoundary - num3;
				}
				IsCameraPositionHorizontallyBounded = true;
				base.ProCamera2D.IsCameraPositionRightBounded = true;
			}
			if (UseBottomBoundary && num2 - num4 < BottomBoundary + num6)
			{
				if (UseSoftBoundaries)
				{
					num2 = ((!(Vector3V(originalDelta) <= 0f)) ? Mathf.Max(BottomBoundary + num4, num2) : Mathf.SmoothDamp(Mathf.Max(BottomBoundary + num4, Vector3V(base.ProCamera2D.LocalPosition)), Mathf.Max(BottomBoundary + num4, num2), ref _smoothnessVelY, (BottomBoundary + num4 + num6 - Vector3V(base.ProCamera2D.LocalPosition)) / num5 * Softness));
				}
				else if (!UseSoftBoundaries)
				{
					num2 = BottomBoundary + num4;
				}
				IsCameraPositionVerticallyBounded = true;
				base.ProCamera2D.IsCameraPositionBottomBounded = true;
			}
			if (UseTopBoundary && num2 + num4 > TopBoundary - num6)
			{
				if (UseSoftBoundaries)
				{
					num2 = ((!(Vector3V(originalDelta) >= 0f)) ? Mathf.Min(TopBoundary - num4, num2) : Mathf.SmoothDamp(Mathf.Min(TopBoundary - num4, Vector3V(base.ProCamera2D.LocalPosition)), Mathf.Min(TopBoundary - num4, num2), ref _smoothnessVelY, (Vector3V(base.ProCamera2D.LocalPosition) - (TopBoundary - num4) + num6) / num5 * Softness));
				}
				else if (!UseSoftBoundaries)
				{
					num2 = TopBoundary - num4;
				}
				IsCameraPositionVerticallyBounded = true;
				base.ProCamera2D.IsCameraPositionTopBounded = true;
			}
			return VectorHV(num - Vector3H(base.ProCamera2D.LocalPosition), num2 - Vector3V(base.ProCamera2D.LocalPosition));
		}

		public float OverrideSize(float deltaTime, float originalSize)
		{
			if (!base.enabled || !UseNumericBoundaries)
			{
				return originalSize;
			}
			float num = originalSize;
			Vector2 vector = new Vector2(RightBoundary - LeftBoundary, TopBoundary - BottomBoundary);
			if (UseRightBoundary && UseLeftBoundary && originalSize * base.ProCamera2D.GameCamera.aspect * 2f > vector.x)
			{
				num = vector.x / base.ProCamera2D.GameCamera.aspect * 0.5f;
			}
			if (UseTopBoundary && UseBottomBoundary && num * 2f > vector.y)
			{
				num = vector.y * 0.5f;
			}
			return num;
		}
	}
}
