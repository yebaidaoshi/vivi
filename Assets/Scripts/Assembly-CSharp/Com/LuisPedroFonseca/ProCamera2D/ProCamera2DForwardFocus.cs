using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-forward-focus/")]
	public class ProCamera2DForwardFocus : BasePC2D, IPreMover
	{
		public static string ExtensionName = "Forward Focus";

		private const float EPSILON = 0.001f;

		public bool Progressive = true;

		public float SpeedMultiplier = 1f;

		public float TransitionSmoothness = 0.5f;

		public bool MaintainInfluenceOnStop = true;

		public Vector2 MovementThreshold = Vector2.zero;

		[Range(0.001f, 0.5f)]
		public float LeftFocus = 0.25f;

		[Range(0.001f, 0.5f)]
		public float RightFocus = 0.25f;

		[Range(0.001f, 0.5f)]
		public float TopFocus = 0.25f;

		[Range(0.001f, 0.5f)]
		public float BottomFocus = 0.25f;

		private float _hVel;

		private float _hVelSmooth;

		private float _vVel;

		private float _vVelSmooth;

		private float _targetHVel;

		private float _targetVVel;

		private bool _isFirstHorizontalCameraMovement;

		private bool _isFirstVerticalCameraMovement;

		private bool __enabled;

		private int _prmOrder = 2000;

		public int PrMOrder
		{
			get
			{
				return _prmOrder;
			}
			set
			{
				_prmOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			StartCoroutine(Enable());
			base.ProCamera2D.AddPreMover(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePreMover(this);
			}
		}

		public void PreMove(float deltaTime)
		{
			if (__enabled && base.enabled)
			{
				ApplyInfluence(deltaTime);
			}
		}

		public override void OnReset()
		{
			_hVel = 0f;
			_hVelSmooth = 0f;
			_vVel = 0f;
			_vVelSmooth = 0f;
			_targetHVel = 0f;
			_targetVVel = 0f;
			_isFirstHorizontalCameraMovement = false;
			_isFirstVerticalCameraMovement = false;
			__enabled = false;
			StartCoroutine(Enable());
		}

		private IEnumerator Enable()
		{
			yield return new WaitForEndOfFrame();
			__enabled = true;
		}

		private void ApplyInfluence(float deltaTime)
		{
			float num = Vector3H(base.ProCamera2D.TargetsMidPoint) - Vector3H(base.ProCamera2D.PreviousTargetsMidPoint);
			num = ((!(Mathf.Abs(num) < MovementThreshold.x)) ? (num / deltaTime) : 0f);
			float num2 = Vector3V(base.ProCamera2D.TargetsMidPoint) - Vector3V(base.ProCamera2D.PreviousTargetsMidPoint);
			num2 = ((!(Mathf.Abs(num2) < MovementThreshold.y)) ? (num2 / deltaTime) : 0f);
			if (Progressive)
			{
				num = Mathf.Clamp(num * SpeedMultiplier, (0f - LeftFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.x, RightFocus * base.ProCamera2D.ScreenSizeInWorldCoordinates.x);
				num2 = Mathf.Clamp(num2 * SpeedMultiplier, (0f - BottomFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.y, TopFocus * base.ProCamera2D.ScreenSizeInWorldCoordinates.y);
				if (MaintainInfluenceOnStop)
				{
					if ((Mathf.Sign(num) == 1f && num < _hVel) || (Mathf.Sign(num) == -1f && num > _hVel) || Mathf.Abs(num) < 0.001f)
					{
						num = _hVel;
					}
					if ((Mathf.Sign(num2) == 1f && num2 < _vVel) || (Mathf.Sign(num2) == -1f && num2 > _vVel) || Mathf.Abs(num2) < 0.001f)
					{
						num2 = _vVel;
					}
				}
			}
			else if (!MaintainInfluenceOnStop)
			{
				num = ((Mathf.Abs(num) < 0.001f) ? 0f : (((num < 0f) ? (0f - LeftFocus) : RightFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.x));
				num2 = ((Mathf.Abs(num2) < 0.001f) ? 0f : (((num2 < 0f) ? (0f - BottomFocus) : TopFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.y));
			}
			else
			{
				bool flag;
				if (!_isFirstHorizontalCameraMovement && !(Mathf.Abs(num) < 0.001f))
				{
					_isFirstHorizontalCameraMovement = true;
					flag = true;
				}
				else
				{
					flag = Mathf.Sign(num) != Mathf.Sign(_targetHVel);
				}
				if (!(Mathf.Abs(num) < 0.001f) && flag)
				{
					_targetHVel = ((num < 0f) ? (0f - LeftFocus) : RightFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.x;
				}
				num = _targetHVel;
				bool flag2;
				if (!_isFirstVerticalCameraMovement && !(Mathf.Abs(num2) < 0.001f))
				{
					_isFirstVerticalCameraMovement = true;
					flag2 = true;
				}
				else
				{
					flag2 = Mathf.Sign(num2) != Mathf.Sign(_targetVVel);
				}
				if (!(Mathf.Abs(num2) < 0.001f) && flag2)
				{
					_targetVVel = ((num2 < 0f) ? (0f - BottomFocus) : TopFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.y;
				}
				num2 = _targetVVel;
			}
			num = Mathf.Clamp(num, (0f - LeftFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.x, RightFocus * base.ProCamera2D.ScreenSizeInWorldCoordinates.x);
			num2 = Mathf.Clamp(num2, (0f - BottomFocus) * base.ProCamera2D.ScreenSizeInWorldCoordinates.y, TopFocus * base.ProCamera2D.ScreenSizeInWorldCoordinates.y);
			_hVel = Mathf.SmoothDamp(_hVel, num, ref _hVelSmooth, TransitionSmoothness, float.MaxValue, deltaTime);
			_vVel = Mathf.SmoothDamp(_vVel, num2, ref _vVelSmooth, TransitionSmoothness, float.MaxValue, deltaTime);
			base.ProCamera2D.ApplyInfluence(new Vector2(_hVel, _vVel));
		}
	}
}
