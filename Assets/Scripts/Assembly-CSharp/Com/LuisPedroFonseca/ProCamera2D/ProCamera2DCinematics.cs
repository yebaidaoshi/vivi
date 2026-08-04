using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-cinematics/")]
	public class ProCamera2DCinematics : BasePC2D, IPositionOverrider, ISizeOverrider
	{
		public static string ExtensionName = "Cinematics";

		public UnityEvent OnCinematicStarted;

		public CinematicEvent OnCinematicTargetReached;

		public UnityEvent OnCinematicFinished;

		private bool _isPlaying;

		public List<CinematicTarget> CinematicTargets = new List<CinematicTarget>();

		public float EndDuration = 1f;

		public EaseType EndEaseType = EaseType.EaseOut;

		public bool UseNumericBoundaries;

		public bool UseLetterbox = true;

		[Range(0f, 0.5f)]
		public float LetterboxAmount = 0.1f;

		public float LetterboxAnimDuration = 1f;

		public Color LetterboxColor = Color.black;

		private float _initialCameraSize;

		private ProCamera2DNumericBoundaries _numericBoundaries;

		private bool _numericBoundariesPreviousState;

		private ProCamera2DLetterbox _letterbox;

		private Coroutine _startCinematicRoutine;

		private Coroutine _goToCinematicRoutine;

		private Coroutine _endCinematicRoutine;

		private bool _skipTarget;

		private Vector3 _newPos;

		private Vector3 _originalPos;

		private Vector3 _startPos;

		private float _newSize;

		private bool _paused;

		private int _poOrder;

		private int _soOrder = 3000;

		public bool IsPlaying => _isPlaying;

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
			if (UseLetterbox)
			{
				SetupLetterbox();
			}
			base.ProCamera2D.AddPositionOverrider(this);
			base.ProCamera2D.AddSizeOverrider(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (!(base.ProCamera2D == null))
			{
				base.ProCamera2D.RemovePositionOverrider(this);
				base.ProCamera2D.RemoveSizeOverrider(this);
			}
		}

		public Vector3 OverridePosition(float deltaTime, Vector3 originalPosition)
		{
			if (!base.enabled)
			{
				return originalPosition;
			}
			_originalPos = originalPosition;
			if (_isPlaying)
			{
				return _newPos;
			}
			return originalPosition;
		}

		public float OverrideSize(float deltaTime, float originalSize)
		{
			if (!base.enabled)
			{
				return originalSize;
			}
			if (_isPlaying)
			{
				return _newSize;
			}
			return originalSize;
		}

		public void Play()
		{
			if (_isPlaying)
			{
				return;
			}
			_paused = false;
			if (CinematicTargets.Count == 0)
			{
				Debug.LogWarning("No cinematic targets added to the list");
				return;
			}
			_initialCameraSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			if (_numericBoundaries == null)
			{
				_numericBoundaries = base.ProCamera2D.GetComponentInChildren<ProCamera2DNumericBoundaries>();
			}
			if (_numericBoundaries == null)
			{
				_numericBoundaries = Object.FindObjectOfType<ProCamera2DNumericBoundaries>();
			}
			if (_numericBoundaries == null)
			{
				UseNumericBoundaries = false;
			}
			else
			{
				_numericBoundariesPreviousState = _numericBoundaries.enabled;
				_numericBoundaries.enabled = false;
			}
			_isPlaying = true;
			if (_endCinematicRoutine != null)
			{
				StopCoroutine(_endCinematicRoutine);
				_endCinematicRoutine = null;
			}
			if (_startCinematicRoutine == null)
			{
				_startCinematicRoutine = StartCoroutine(StartCinematicRoutine());
			}
		}

		public void Stop()
		{
			if (_isPlaying)
			{
				if (_startCinematicRoutine != null)
				{
					StopCoroutine(_startCinematicRoutine);
					_startCinematicRoutine = null;
				}
				if (_goToCinematicRoutine != null)
				{
					StopCoroutine(_goToCinematicRoutine);
					_goToCinematicRoutine = null;
				}
				if (_endCinematicRoutine == null)
				{
					_endCinematicRoutine = StartCoroutine(EndCinematicRoutine());
				}
			}
		}

		public void Toggle()
		{
			if (_isPlaying)
			{
				Stop();
			}
			else
			{
				Play();
			}
		}

		public void GoToNextTarget()
		{
			_skipTarget = true;
		}

		public void Pause()
		{
			_paused = true;
		}

		public void Unpause()
		{
			_paused = false;
		}

		public void AddCinematicTarget(Transform targetTransform, float easeInDuration = 1f, float holdDuration = 1f, float zoom = 1f, EaseType easeType = EaseType.EaseOut, string sendMessageName = "", string sendMessageParam = "", int index = -1)
		{
			CinematicTarget item = new CinematicTarget
			{
				TargetTransform = targetTransform,
				EaseInDuration = easeInDuration,
				HoldDuration = holdDuration,
				Zoom = zoom,
				EaseType = easeType,
				SendMessageName = sendMessageName,
				SendMessageParam = sendMessageParam
			};
			if (index == -1 || index > CinematicTargets.Count)
			{
				CinematicTargets.Add(item);
			}
			else
			{
				CinematicTargets.Insert(index, item);
			}
		}

		public void RemoveCinematicTarget(Transform targetTransform)
		{
			for (int i = 0; i < CinematicTargets.Count; i++)
			{
				if (CinematicTargets[i].TargetTransform.GetInstanceID() == targetTransform.GetInstanceID())
				{
					CinematicTargets.Remove(CinematicTargets[i]);
				}
			}
		}

		private IEnumerator StartCinematicRoutine()
		{
			if (OnCinematicStarted != null)
			{
				OnCinematicStarted.Invoke();
			}
			_startPos = base.ProCamera2D.LocalPosition;
			_newPos = base.ProCamera2D.LocalPosition;
			_newSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			if (UseLetterbox)
			{
				if (_letterbox == null)
				{
					SetupLetterbox();
				}
				_letterbox.Color = LetterboxColor;
				_letterbox.TweenTo(LetterboxAmount, LetterboxAnimDuration);
			}
			int count = -1;
			while (count < CinematicTargets.Count - 1)
			{
				count++;
				_skipTarget = false;
				_goToCinematicRoutine = StartCoroutine(GoToCinematicTargetRoutine(CinematicTargets[count], count));
				yield return _goToCinematicRoutine;
			}
			Stop();
		}

		private IEnumerator GoToCinematicTargetRoutine(CinematicTarget cinematicTarget, int targetIndex)
		{
			if (cinematicTarget.TargetTransform == null)
			{
				yield break;
			}
			float initialPosH = Vector3H(base.ProCamera2D.LocalPosition);
			float initialPosV = Vector3V(base.ProCamera2D.LocalPosition);
			float currentCameraSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			float t = 0f;
			if (cinematicTarget.EaseInDuration > 0f)
			{
				while (t <= 1f)
				{
					if (!_paused)
					{
						t += base.ProCamera2D.DeltaTime / cinematicTarget.EaseInDuration;
						float horizontalPos = Utils.EaseFromTo(initialPosH, Vector3H(cinematicTarget.TargetTransform.position) - Vector3H(base.ProCamera2D.ParentPosition), t, cinematicTarget.EaseType);
						float verticalPos = Utils.EaseFromTo(initialPosV, Vector3V(cinematicTarget.TargetTransform.position) - Vector3V(base.ProCamera2D.ParentPosition), t, cinematicTarget.EaseType);
						if (UseNumericBoundaries)
						{
							LimitToNumericBoundaries(ref horizontalPos, ref verticalPos);
						}
						_newPos = VectorHVD(horizontalPos, verticalPos, 0f);
						_newSize = Utils.EaseFromTo(currentCameraSize, _initialCameraSize / cinematicTarget.Zoom, t, cinematicTarget.EaseType);
						if (_skipTarget)
						{
							yield break;
						}
					}
					yield return base.ProCamera2D.GetYield();
				}
			}
			else
			{
				float arg = Vector3H(cinematicTarget.TargetTransform.position) - Vector3H(base.ProCamera2D.ParentPosition);
				float arg2 = Vector3V(cinematicTarget.TargetTransform.position) - Vector3V(base.ProCamera2D.ParentPosition);
				_newPos = VectorHVD(arg, arg2, 0f);
				_newSize = _initialCameraSize / cinematicTarget.Zoom;
			}
			if (OnCinematicTargetReached != null)
			{
				OnCinematicTargetReached.Invoke(targetIndex);
			}
			if (!string.IsNullOrEmpty(cinematicTarget.SendMessageName))
			{
				cinematicTarget.TargetTransform.SendMessage(cinematicTarget.SendMessageName, cinematicTarget.SendMessageParam, SendMessageOptions.DontRequireReceiver);
			}
			t = 0f;
			while (cinematicTarget.HoldDuration < 0f || t <= cinematicTarget.HoldDuration)
			{
				if (!_paused)
				{
					t += base.ProCamera2D.DeltaTime;
					float horizontalPos2 = Vector3H(cinematicTarget.TargetTransform.position) - Vector3H(base.ProCamera2D.ParentPosition);
					float verticalPos2 = Vector3V(cinematicTarget.TargetTransform.position) - Vector3V(base.ProCamera2D.ParentPosition);
					if (UseNumericBoundaries)
					{
						LimitToNumericBoundaries(ref horizontalPos2, ref verticalPos2);
					}
					_newPos = VectorHVD(horizontalPos2, verticalPos2, 0f);
					if (_skipTarget)
					{
						break;
					}
				}
				yield return base.ProCamera2D.GetYield();
			}
		}

		private IEnumerator EndCinematicRoutine()
		{
			if (_letterbox != null && LetterboxAmount > 0f)
			{
				_letterbox.TweenTo(0f, LetterboxAnimDuration);
			}
			float initialPosH = Vector3H(_newPos);
			float initialPosV = Vector3V(_newPos);
			float currentCameraSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			float t = 0f;
			while (t <= 1f)
			{
				if (!_paused)
				{
					t += base.ProCamera2D.DeltaTime / EndDuration;
					float end = Vector3H(_originalPos);
					float end2 = Vector3V(_originalPos);
					float horizontalPos;
					float verticalPos;
					if (base.ProCamera2D.CameraTargets.Count > 0)
					{
						horizontalPos = Utils.EaseFromTo(initialPosH, end, t, EndEaseType);
						verticalPos = Utils.EaseFromTo(initialPosV, end2, t, EndEaseType);
					}
					else
					{
						horizontalPos = Utils.EaseFromTo(initialPosH, Vector3H(_startPos), t, EndEaseType);
						verticalPos = Utils.EaseFromTo(initialPosV, Vector3V(_startPos), t, EndEaseType);
					}
					if (_numericBoundariesPreviousState)
					{
						LimitToNumericBoundaries(ref horizontalPos, ref verticalPos);
					}
					_newPos = VectorHVD(horizontalPos, verticalPos, 0f);
					_newSize = Utils.EaseFromTo(currentCameraSize, _initialCameraSize, t, EndEaseType);
				}
				yield return base.ProCamera2D.GetYield();
			}
			_isPlaying = false;
			if (_numericBoundaries != null)
			{
				_numericBoundaries.enabled = _numericBoundariesPreviousState;
			}
			if (OnCinematicFinished != null)
			{
				OnCinematicFinished.Invoke();
			}
			if (base.ProCamera2D.CameraTargets.Count == 0)
			{
				base.ProCamera2D.Reset();
			}
		}

		private void SetupLetterbox()
		{
			ProCamera2DLetterbox componentInChildren = base.ProCamera2D.gameObject.GetComponentInChildren<ProCamera2DLetterbox>();
			if (componentInChildren == null)
			{
				(from c in base.ProCamera2D.gameObject.GetComponentsInChildren<Camera>()
					orderby c.depth descending
					select c).ToArray()[0].gameObject.AddComponent<ProCamera2DLetterbox>();
			}
			_letterbox = componentInChildren;
		}

		private void LimitToNumericBoundaries(ref float horizontalPos, ref float verticalPos)
		{
			if (_numericBoundaries.UseLeftBoundary && horizontalPos - base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f < _numericBoundaries.LeftBoundary)
			{
				horizontalPos = _numericBoundaries.LeftBoundary + base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
			}
			else if (_numericBoundaries.UseRightBoundary && horizontalPos + base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f > _numericBoundaries.RightBoundary)
			{
				horizontalPos = _numericBoundaries.RightBoundary - base.ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
			}
			if (_numericBoundaries.UseBottomBoundary && verticalPos - base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f < _numericBoundaries.BottomBoundary)
			{
				verticalPos = _numericBoundaries.BottomBoundary + base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			}
			else if (_numericBoundaries.UseTopBoundary && verticalPos + base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f > _numericBoundaries.TopBoundary)
			{
				verticalPos = _numericBoundaries.TopBoundary - base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			}
		}
	}
}
