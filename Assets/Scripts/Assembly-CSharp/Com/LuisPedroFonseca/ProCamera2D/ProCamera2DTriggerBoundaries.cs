using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/trigger-boundaries/")]
	public class ProCamera2DTriggerBoundaries : BaseTrigger, IPositionOverrider
	{
		public static string TriggerName = "Boundaries Trigger";

		public ProCamera2DNumericBoundaries NumericBoundaries;

		public bool AreBoundariesRelative = true;

		public bool UseTopBoundary = true;

		public float TopBoundary = 10f;

		public bool UseBottomBoundary = true;

		public float BottomBoundary = -10f;

		public bool UseLeftBoundary = true;

		public float LeftBoundary = -10f;

		public bool UseRightBoundary = true;

		public float RightBoundary = 10f;

		public float TransitionDuration = 1f;

		public EaseType TransitionEaseType;

		public bool ChangeZoom;

		public float TargetZoom = 1.5f;

		public float ZoomSmoothness = 1f;

		public bool _setAsStartingBoundaries;

		private float _initialCamSize;

		private BoundariesAnimator _boundsAnim;

		private float _targetTopBoundary;

		private float _targetBottomBoundary;

		private float _targetLeftBoundary;

		private float _targetRightBoundary;

		private bool _transitioning;

		private Vector3 _newPos;

		private int _poOrder = 1000;

		public bool IsCurrentTrigger => NumericBoundaries.CurrentBoundariesTrigger._instanceID == _instanceID;

		public bool SetAsStartingBoundaries
		{
			get
			{
				return _setAsStartingBoundaries;
			}
			set
			{
				if (value && !_setAsStartingBoundaries)
				{
					UnityEngine.Object[] array = UnityEngine.Object.FindObjectsOfType(typeof(ProCamera2DTriggerBoundaries));
					for (int i = 0; i < array.Length; i++)
					{
						((ProCamera2DTriggerBoundaries)array[i]).SetAsStartingBoundaries = false;
					}
				}
				_setAsStartingBoundaries = value;
			}
		}

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

		private void Start()
		{
			if (base.ProCamera2D == null)
			{
				return;
			}
			if (NumericBoundaries == null)
			{
				ProCamera2DNumericBoundaries proCamera2DNumericBoundaries = UnityEngine.Object.FindObjectOfType<ProCamera2DNumericBoundaries>();
				NumericBoundaries = ((proCamera2DNumericBoundaries == null) ? base.ProCamera2D.gameObject.AddComponent<ProCamera2DNumericBoundaries>() : proCamera2DNumericBoundaries);
			}
			_boundsAnim = new BoundariesAnimator(base.ProCamera2D, NumericBoundaries);
			BoundariesAnimator boundsAnim = _boundsAnim;
			boundsAnim.OnTransitionStarted = (Action)Delegate.Combine(boundsAnim.OnTransitionStarted, (Action)delegate
			{
				if (NumericBoundaries.OnBoundariesTransitionStarted != null)
				{
					NumericBoundaries.OnBoundariesTransitionStarted();
				}
			});
			BoundariesAnimator boundsAnim2 = _boundsAnim;
			boundsAnim2.OnTransitionFinished = (Action)Delegate.Combine(boundsAnim2.OnTransitionFinished, (Action)delegate
			{
				if (NumericBoundaries.OnBoundariesTransitionFinished != null)
				{
					NumericBoundaries.OnBoundariesTransitionFinished();
				}
			});
			GetTargetBoundaries();
			if (SetAsStartingBoundaries)
			{
				SetBoundaries();
			}
			_initialCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
		}

		public Vector3 OverridePosition(float deltaTime, Vector3 originalPosition)
		{
			if (!base.enabled)
			{
				return originalPosition;
			}
			if (_transitioning)
			{
				return _newPos;
			}
			return originalPosition;
		}

		protected override void EnteredTrigger()
		{
			base.EnteredTrigger();
			if (NumericBoundaries.CurrentBoundariesTrigger != null)
			{
				StartCoroutine(TurnOffPreviousTrigger(NumericBoundaries.CurrentBoundariesTrigger));
			}
			if ((NumericBoundaries.CurrentBoundariesTrigger != null && NumericBoundaries.CurrentBoundariesTrigger._instanceID != _instanceID) || NumericBoundaries.CurrentBoundariesTrigger == null)
			{
				NumericBoundaries.CurrentBoundariesTrigger = this;
				StartCoroutine(Transition());
			}
		}

		private IEnumerator TurnOffPreviousTrigger(ProCamera2DTriggerBoundaries trigger)
		{
			yield return new WaitForEndOfFrame();
			trigger._transitioning = false;
		}

		public void SetBoundaries()
		{
			if (NumericBoundaries != null)
			{
				NumericBoundaries.CurrentBoundariesTrigger = this;
				NumericBoundaries.UseLeftBoundary = UseLeftBoundary;
				if (UseLeftBoundary)
				{
					NumericBoundaries.LeftBoundary = (NumericBoundaries.TargetLeftBoundary = _targetLeftBoundary);
				}
				NumericBoundaries.UseRightBoundary = UseRightBoundary;
				if (UseRightBoundary)
				{
					NumericBoundaries.RightBoundary = (NumericBoundaries.TargetRightBoundary = _targetRightBoundary);
				}
				NumericBoundaries.UseTopBoundary = UseTopBoundary;
				if (UseTopBoundary)
				{
					NumericBoundaries.TopBoundary = (NumericBoundaries.TargetTopBoundary = _targetTopBoundary);
				}
				NumericBoundaries.UseBottomBoundary = UseBottomBoundary;
				if (UseBottomBoundary)
				{
					NumericBoundaries.BottomBoundary = (NumericBoundaries.TargetBottomBoundary = _targetBottomBoundary);
				}
			}
		}

		private void GetTargetBoundaries()
		{
			if (AreBoundariesRelative)
			{
				_targetTopBoundary = Vector3V(base.transform.position) + TopBoundary;
				_targetBottomBoundary = Vector3V(base.transform.position) + BottomBoundary;
				_targetLeftBoundary = Vector3H(base.transform.position) + LeftBoundary;
				_targetRightBoundary = Vector3H(base.transform.position) + RightBoundary;
			}
			else
			{
				_targetTopBoundary = TopBoundary;
				_targetBottomBoundary = BottomBoundary;
				_targetLeftBoundary = LeftBoundary;
				_targetRightBoundary = RightBoundary;
			}
		}

		private IEnumerator Transition()
		{
			if (!UseTopBoundary && !UseBottomBoundary && !UseLeftBoundary && !UseRightBoundary)
			{
				NumericBoundaries.UseTopBoundary = false;
				NumericBoundaries.UseBottomBoundary = false;
				NumericBoundaries.UseLeftBoundary = false;
				NumericBoundaries.UseRightBoundary = false;
				yield break;
			}
			Vector3 position = base.transform.position;
			float num = (AreBoundariesRelative ? (position.y + TopBoundary) : TopBoundary);
			float num2 = (AreBoundariesRelative ? (position.y + BottomBoundary) : BottomBoundary);
			float num3 = (AreBoundariesRelative ? (position.x + LeftBoundary) : LeftBoundary);
			float num4 = (AreBoundariesRelative ? (position.x + RightBoundary) : RightBoundary);
			bool flag = true;
			if (UseTopBoundary && (Mathf.Abs(NumericBoundaries.TopBoundary - num) > 0.01f || !NumericBoundaries.UseTopBoundary))
			{
				flag = false;
			}
			if (flag && UseBottomBoundary && (Mathf.Abs(NumericBoundaries.BottomBoundary - num2) > 0.01f || !NumericBoundaries.UseBottomBoundary))
			{
				flag = false;
			}
			if (flag && UseLeftBoundary && (Mathf.Abs(NumericBoundaries.LeftBoundary - num3) > 0.01f || !NumericBoundaries.UseLeftBoundary))
			{
				flag = false;
			}
			if (flag && UseRightBoundary && (Mathf.Abs(NumericBoundaries.RightBoundary - num4) > 0.01f || !NumericBoundaries.UseRightBoundary))
			{
				flag = false;
			}
			if (flag)
			{
				yield break;
			}
			GetTargetBoundaries();
			_boundsAnim.UseTopBoundary = UseTopBoundary;
			_boundsAnim.TopBoundary = _targetTopBoundary;
			_boundsAnim.UseBottomBoundary = UseBottomBoundary;
			_boundsAnim.BottomBoundary = _targetBottomBoundary;
			_boundsAnim.UseLeftBoundary = UseLeftBoundary;
			_boundsAnim.LeftBoundary = _targetLeftBoundary;
			_boundsAnim.UseRightBoundary = UseRightBoundary;
			_boundsAnim.RightBoundary = _targetRightBoundary;
			_boundsAnim.TransitionDuration = TransitionDuration;
			_boundsAnim.TransitionEaseType = TransitionEaseType;
			if (ChangeZoom && _initialCamSize / TargetZoom != base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f)
			{
				base.ProCamera2D.UpdateScreenSize(_initialCamSize / TargetZoom, ZoomSmoothness, TransitionEaseType);
			}
			if (_boundsAnim.GetAnimsCount() > 1)
			{
				if (NumericBoundaries.MoveCameraToTargetRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.MoveCameraToTargetRoutine);
				}
				NumericBoundaries.MoveCameraToTargetRoutine = NumericBoundaries.StartCoroutine(MoveCameraToTarget());
			}
			yield return new WaitForEndOfFrame();
			_boundsAnim.Transition();
		}

		private IEnumerator MoveCameraToTarget()
		{
			float initialCamPosH = Vector3H(base.ProCamera2D.LocalPosition);
			float initialCamPosV = Vector3V(base.ProCamera2D.LocalPosition);
			_newPos = VectorHVD(initialCamPosH, initialCamPosV, 0f);
			_transitioning = true;
			float t = 0f;
			while (t <= 1f)
			{
				t += base.ProCamera2D.DeltaTime / TransitionDuration;
				float horizontalPos = Utils.EaseFromTo(initialCamPosH, base.ProCamera2D.CameraTargetPositionSmoothed.x, t, TransitionEaseType);
				float verticalPos = Utils.EaseFromTo(initialCamPosV, base.ProCamera2D.CameraTargetPositionSmoothed.y, t, TransitionEaseType);
				LimitToNumericBoundaries(ref horizontalPos, ref verticalPos);
				_newPos = VectorHVD(horizontalPos, verticalPos, 0f);
				yield return base.ProCamera2D.GetYield();
			}
			NumericBoundaries.MoveCameraToTargetRoutine = null;
			_transitioning = false;
		}

		private void LimitToNumericBoundaries(ref float horizontalPos, ref float verticalPos)
		{
			if (NumericBoundaries.UseLeftBoundary && horizontalPos - base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f < NumericBoundaries.LeftBoundary)
			{
				horizontalPos = NumericBoundaries.LeftBoundary + base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
			}
			else if (NumericBoundaries.UseRightBoundary && horizontalPos + base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f > NumericBoundaries.RightBoundary)
			{
				horizontalPos = NumericBoundaries.RightBoundary - base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
			}
			if (NumericBoundaries.UseBottomBoundary && verticalPos - base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f < NumericBoundaries.BottomBoundary)
			{
				verticalPos = NumericBoundaries.BottomBoundary + base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			}
			else if (NumericBoundaries.UseTopBoundary && verticalPos + base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f > NumericBoundaries.TopBoundary)
			{
				verticalPos = NumericBoundaries.TopBoundary - base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			}
		}
	}
}
