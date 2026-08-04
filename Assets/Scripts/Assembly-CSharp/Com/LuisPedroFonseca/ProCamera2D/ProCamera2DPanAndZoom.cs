using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-pan-and-zoom/")]
	public class ProCamera2DPanAndZoom : BasePC2D, IPreMover
	{
		public enum MouseButton
		{
			Left = 0,
			Right = 1,
			Middle = 2
		}

		public static string ExtensionName = "Pan And Zoom";

		public Action OnPanStarted;

		public Action OnPanFinished;

		public bool AutomaticInputDetection = true;

		public bool UseMouseInput;

		public bool UseTouchInput;

		public bool DisableOverUGUI = true;

		public bool AllowZoom = true;

		public float MouseZoomSpeed = 10f;

		public float PinchZoomSpeed = 50f;

		[Range(0f, 2f)]
		public float ZoomSmoothness = 0.2f;

		public float MaxZoomInAmount = 2f;

		public float MaxZoomOutAmount = 2f;

		public bool ZoomToInputCenter = true;

		[HideInInspector]
		public bool IsZooming;

		private float _zoomAmount;

		private float _initialCamSize;

		private bool _zoomStarted;

		private float _origFollowSmoothnessX;

		private float _origFollowSmoothnessY;

		private float _prevZoomAmount;

		private float _zoomVelocity;

		private Vector3 _zoomPoint;

		private float _touchZoomTime;

		public bool AllowPan = true;

		public bool UsePanByDrag = true;

		[Range(0f, 1f)]
		public float StopSpeedOnDragStart = 0.95f;

		public Rect DraggableAreaRect = new Rect(0f, 0f, 1f, 1f);

		public Vector2 DragPanSpeedMultiplier = new Vector2(1f, 1f);

		public bool UsePanByMoveToEdges;

		public Vector2 EdgesPanSpeed = new Vector2(2f, 2f);

		[Range(0f, 0.99f)]
		public float TopPanEdge = 0.9f;

		[Range(0f, 0.99f)]
		public float BottomPanEdge = 0.9f;

		[Range(0f, 0.99f)]
		public float LeftPanEdge = 0.9f;

		[Range(0f, 0.99f)]
		public float RightPanEdge = 0.9f;

		public MouseButton PanMouseButton;

		public float MinPanAmount = 0.05f;

		[HideInInspector]
		public bool ResetPrevPanPoint;

		[HideInInspector]
		public bool IsPanning;

		private Vector2 _panDelta;

		private Transform _panTarget;

		private Vector3 _prevMousePosition;

		private Vector3 _prevTouchPosition;

		private int _prevTouchId;

		private bool _onMaxZoom;

		private bool _onMinZoom;

		private EventSystem _eventSystem;

		private bool _skip;

		private Vector3 _startPanWorldPos;

		private int _prmOrder;

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
			if (AutomaticInputDetection)
			{
				UseMouseInput = !Input.touchSupported;
				UseTouchInput = Input.touchSupported;
			}
			UpdateCurrentFollowSmoothness();
			_eventSystem = EventSystem.current;
			_panTarget = new GameObject("PC2DPanTarget").transform;
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

		private IEnumerator Start()
		{
			_initialCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			yield return null;
			if (base.gameObject.scene.buildIndex == -1)
			{
				UnityEngine.Object.DontDestroyOnLoad(_panTarget.gameObject);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			_initialCamSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;
			base.ProCamera2D.AddCameraTarget(_panTarget);
			CenterPanTargetOnCamera();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			ResetPrevPanPoint = true;
			_onMaxZoom = false;
			_onMinZoom = false;
			base.ProCamera2D.RemoveCameraTarget(_panTarget);
		}

		public void PreMove(float deltaTime)
		{
			if (UseTouchInput)
			{
				_skip = DisableOverUGUI && (bool)_eventSystem;
				if (_skip)
				{
					_skip = false;
					for (int i = 0; i < Input.touchCount; i++)
					{
						if (_eventSystem.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
						{
							_skip = true;
							break;
						}
					}
				}
				if (_skip)
				{
					_prevTouchPosition = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
					CancelZoom();
				}
			}
			if (UseMouseInput)
			{
				_skip = DisableOverUGUI && (bool)_eventSystem && _eventSystem.IsPointerOverGameObject();
				if (_skip)
				{
					_prevMousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
					CancelZoom();
				}
			}
			IsZooming = false;
			if (base.enabled && AllowPan && !_skip)
			{
				Pan(deltaTime);
			}
			if (base.enabled && AllowZoom && !_skip)
			{
				Zoom(deltaTime);
			}
		}

		private void Pan(float deltaTime)
		{
			_panDelta = Vector2.zero;
			if (UseTouchInput)
			{
				if (Time.time - _touchZoomTime < 0.1f)
				{
					if (Input.touchCount > 0)
					{
						_prevTouchPosition = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
					}
					return;
				}
				if ((AllowZoom && Input.touchCount == 1) || (!AllowZoom && Input.touchCount > 0))
				{
					Touch touch = Input.GetTouch(Input.touchCount - 1);
					if (touch.phase == TouchPhase.Began)
					{
						_prevTouchId = touch.fingerId;
						_prevTouchPosition = new Vector3(touch.position.x, touch.position.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
						_startPanWorldPos = base.ProCamera2D.GameCamera.ScreenToWorldPoint(_prevTouchPosition);
					}
					if (touch.fingerId != _prevTouchId || touch.phase != TouchPhase.Moved)
					{
						return;
					}
					Vector3 vector = new Vector3(touch.position.x, touch.position.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
					Vector2 normalizedInput = new Vector2(touch.position.x / (float)base.ProCamera2D.GameCamera.pixelWidth, touch.position.y / (float)base.ProCamera2D.GameCamera.pixelHeight);
					if (base.ProCamera2D.GameCamera.pixelRect.Contains(vector) && InsideDraggableArea(normalizedInput))
					{
						Vector3 vector2 = base.ProCamera2D.GameCamera.ScreenToWorldPoint(_prevTouchPosition);
						Vector3 vector3 = base.ProCamera2D.GameCamera.ScreenToWorldPoint(vector);
						if (IsPanning)
						{
							if (ResetPrevPanPoint)
							{
								vector2 = base.ProCamera2D.GameCamera.ScreenToWorldPoint(vector);
								ResetPrevPanPoint = false;
							}
							Vector3 arg = vector2 - vector3;
							_panDelta = new Vector2(Vector3H(arg), Vector3V(arg));
						}
						else
						{
							float num = (base.ProCamera2D.ScreenSizeInWorldCoordinates.x + base.ProCamera2D.ScreenSizeInWorldCoordinates.y) / 2f;
							if (Vector3.Distance(vector3, _startPanWorldPos) / num > MinPanAmount)
							{
								CenterPanTargetOnCamera(StopSpeedOnDragStart);
								StartPanning();
							}
						}
					}
					_prevTouchPosition = vector;
				}
				if (IsPanning && Input.touchCount == 0)
				{
					StopPanning();
				}
			}
			Vector2 vector4 = DragPanSpeedMultiplier;
			if (UseMouseInput)
			{
				Vector3 vector5 = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
				if (Input.GetMouseButtonDown((int)PanMouseButton))
				{
					_startPanWorldPos = base.ProCamera2D.GameCamera.ScreenToWorldPoint(vector5);
				}
				if (UsePanByDrag && Input.GetMouseButton((int)PanMouseButton) && !IsPanning)
				{
					Vector3 a = base.ProCamera2D.GameCamera.ScreenToWorldPoint(vector5);
					float num2 = (base.ProCamera2D.ScreenSizeInWorldCoordinates.x + base.ProCamera2D.ScreenSizeInWorldCoordinates.y) / 2f;
					if (Vector3.Distance(a, _startPanWorldPos) / num2 > MinPanAmount)
					{
						CenterPanTargetOnCamera(StopSpeedOnDragStart);
						StartPanning();
					}
				}
				if (IsPanning && UsePanByDrag && Input.GetMouseButton((int)PanMouseButton))
				{
					Vector2 normalizedInput2 = new Vector2(Input.mousePosition.x / (float)base.ProCamera2D.GameCamera.pixelWidth, Input.mousePosition.y / (float)base.ProCamera2D.GameCamera.pixelHeight);
					if (base.ProCamera2D.GameCamera.pixelRect.Contains(vector5) && InsideDraggableArea(normalizedInput2))
					{
						Vector3 vector6 = base.ProCamera2D.GameCamera.ScreenToWorldPoint(_prevMousePosition);
						if (ResetPrevPanPoint)
						{
							vector6 = base.ProCamera2D.GameCamera.ScreenToWorldPoint(vector5);
							ResetPrevPanPoint = false;
						}
						Vector3 arg2 = vector6 - base.ProCamera2D.GameCamera.ScreenToWorldPoint(vector5);
						_panDelta = new Vector2(Vector3H(arg2), Vector3V(arg2));
					}
				}
				else if (UsePanByMoveToEdges && !Input.GetMouseButton((int)PanMouseButton))
				{
					float num3 = ((float)(-Screen.width) * 0.5f + Input.mousePosition.x) / (float)Screen.width;
					float num4 = ((float)(-Screen.height) * 0.5f + Input.mousePosition.y) / (float)Screen.height;
					if (num3 < 0f)
					{
						num3 = num3.Remap(-0.5f, (0f - LeftPanEdge) * 0.5f, -0.5f, 0f);
					}
					else if (num3 > 0f)
					{
						num3 = num3.Remap(RightPanEdge * 0.5f, 0.5f, 0f, 0.5f);
					}
					if (num4 < 0f)
					{
						num4 = num4.Remap(-0.5f, (0f - BottomPanEdge) * 0.5f, -0.5f, 0f);
					}
					else if (num4 > 0f)
					{
						num4 = num4.Remap(TopPanEdge * 0.5f, 0.5f, 0f, 0.5f);
					}
					_panDelta = new Vector2(num3, num4) * deltaTime;
					if (_panDelta != Vector2.zero)
					{
						vector4 = EdgesPanSpeed;
					}
				}
				if (IsPanning && UsePanByDrag && !Input.GetMouseButton((int)PanMouseButton))
				{
					StopPanning();
				}
				_prevMousePosition = vector5;
			}
			if (_panDelta != Vector2.zero)
			{
				Vector3 translation = VectorHV(_panDelta.x * vector4.x, _panDelta.y * vector4.y);
				_panTarget.Translate(translation);
			}
			if ((base.ProCamera2D.IsCameraPositionLeftBounded && Vector3H(_panTarget.position) < Vector3H(base.ProCamera2D.LocalPosition)) || (base.ProCamera2D.IsCameraPositionRightBounded && Vector3H(_panTarget.position) > Vector3H(base.ProCamera2D.LocalPosition)))
			{
				_panTarget.position = VectorHVD(Vector3H(base.ProCamera2D.LocalPosition) - base.ProCamera2D.GetOffsetX() * 0.9999f, Vector3V(_panTarget.position), Vector3D(_panTarget.position));
			}
			if ((base.ProCamera2D.IsCameraPositionBottomBounded && Vector3V(_panTarget.position) < Vector3V(base.ProCamera2D.LocalPosition)) || (base.ProCamera2D.IsCameraPositionTopBounded && Vector3V(_panTarget.position) > Vector3V(base.ProCamera2D.LocalPosition)))
			{
				_panTarget.position = VectorHVD(Vector3H(_panTarget.position), Vector3V(base.ProCamera2D.LocalPosition) - base.ProCamera2D.GetOffsetY() * 0.9999f, Vector3D(_panTarget.position));
			}
		}

		private void StartPanning()
		{
			IsPanning = true;
			RestoreFollowSmoothness();
			if (OnPanStarted != null)
			{
				OnPanStarted();
			}
		}

		private void StopPanning()
		{
			IsPanning = false;
			if (OnPanFinished != null)
			{
				OnPanFinished();
			}
		}

		private void Zoom(float deltaTime)
		{
			float num = 0f;
			if (UseTouchInput)
			{
				if (Input.touchCount == 2)
				{
					Touch touch = Input.GetTouch(0);
					Touch touch2 = Input.GetTouch(1);
					Vector2 vector = touch.position - new Vector2(touch.deltaPosition.x / (float)Screen.width, touch.deltaPosition.y / (float)Screen.height);
					Vector2 vector2 = touch2.position - new Vector2(touch2.deltaPosition.x / (float)Screen.width, touch2.deltaPosition.y / (float)Screen.height);
					float magnitude = (vector - vector2).magnitude;
					float magnitude2 = (touch.position - touch2.position).magnitude;
					num = magnitude - magnitude2;
					Vector2 vector3 = Vector2.Lerp(touch.position, touch2.position, 0.5f);
					_zoomPoint = new Vector3(vector3.x, vector3.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
					if (!_zoomStarted)
					{
						_zoomStarted = true;
						_panTarget.position = base.ProCamera2D.LocalPosition - base.ProCamera2D.InfluencesSum;
						UpdateCurrentFollowSmoothness();
						RemoveFollowSmoothness();
					}
					_touchZoomTime = Time.time;
				}
				else if (_zoomStarted && Mathf.Abs(_zoomAmount) < 0.001f)
				{
					RestoreFollowSmoothness();
					_zoomStarted = false;
				}
			}
			if (UseMouseInput)
			{
				num = Input.GetAxis("Mouse ScrollWheel");
				_zoomPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Mathf.Abs(Vector3D(base.ProCamera2D.LocalPosition)));
			}
			if (!base.ProCamera2D.GameCamera.pixelRect.Contains(_zoomPoint))
			{
				return;
			}
			float num2 = (UseTouchInput ? (PinchZoomSpeed * 10f) : MouseZoomSpeed);
			if ((_onMaxZoom && num * num2 < 0f) || (_onMinZoom && num * num2 > 0f))
			{
				CancelZoom();
				return;
			}
			_zoomAmount = Mathf.SmoothDamp(_prevZoomAmount, num * num2 * deltaTime, ref _zoomVelocity, ZoomSmoothness, float.MaxValue, deltaTime);
			if (UseMouseInput)
			{
				if (Mathf.Abs(_zoomAmount) <= 0.0001f)
				{
					if (_zoomStarted)
					{
						RestoreFollowSmoothness();
					}
					_zoomStarted = false;
					_prevZoomAmount = 0f;
					return;
				}
				if (!_zoomStarted)
				{
					_zoomStarted = true;
					_panTarget.position = base.ProCamera2D.LocalPosition - base.ProCamera2D.InfluencesSum;
					UpdateCurrentFollowSmoothness();
					RemoveFollowSmoothness();
				}
			}
			float num3 = base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f + _zoomAmount;
			float num4 = _initialCamSize / MaxZoomInAmount;
			float num5 = MaxZoomOutAmount * _initialCamSize;
			_onMaxZoom = false;
			_onMinZoom = false;
			if (num3 < num4)
			{
				_zoomAmount -= num3 - num4;
				_onMaxZoom = true;
			}
			else if (num3 > num5)
			{
				_zoomAmount -= num3 - num5;
				_onMinZoom = true;
			}
			_prevZoomAmount = _zoomAmount;
			if (ZoomToInputCenter && _zoomAmount != 0f)
			{
				float num6 = _zoomAmount / (base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f);
				_panTarget.position += (_panTarget.position - base.ProCamera2D.GameCamera.ScreenToWorldPoint(_zoomPoint)) * num6;
			}
			base.ProCamera2D.Zoom(_zoomAmount);
			IsZooming = true;
		}

		public void UpdateCurrentFollowSmoothness()
		{
			_origFollowSmoothnessX = base.ProCamera2D.HorizontalFollowSmoothness;
			_origFollowSmoothnessY = base.ProCamera2D.VerticalFollowSmoothness;
		}

		public void CenterPanTargetOnCamera(float interpolant = 1f)
		{
			if (_panTarget != null)
			{
				_panTarget.position = Vector3.Lerp(_panTarget.position, VectorHV(Vector3H(base.ProCamera2D.LocalPosition) - base.ProCamera2D.GetOffsetX(), Vector3V(base.ProCamera2D.LocalPosition) - base.ProCamera2D.GetOffsetY()), interpolant);
			}
		}

		private void CancelZoom()
		{
			_zoomAmount = 0f;
			_prevZoomAmount = 0f;
			_zoomVelocity = 0f;
		}

		private void RestoreFollowSmoothness()
		{
			base.ProCamera2D.HorizontalFollowSmoothness = _origFollowSmoothnessX;
			base.ProCamera2D.VerticalFollowSmoothness = _origFollowSmoothnessY;
		}

		private void RemoveFollowSmoothness()
		{
			base.ProCamera2D.HorizontalFollowSmoothness = 0f;
			base.ProCamera2D.VerticalFollowSmoothness = 0f;
		}

		private bool InsideDraggableArea(Vector2 normalizedInput)
		{
			if (DraggableAreaRect.x == 0f && DraggableAreaRect.y == 0f && DraggableAreaRect.width == 1f && DraggableAreaRect.height == 1f)
			{
				return true;
			}
			if (normalizedInput.x > DraggableAreaRect.x + (1f - DraggableAreaRect.width) / 2f && normalizedInput.x < DraggableAreaRect.x + DraggableAreaRect.width + (1f - DraggableAreaRect.width) / 2f && normalizedInput.y > DraggableAreaRect.y + (1f - DraggableAreaRect.height) / 2f && normalizedInput.y < DraggableAreaRect.y + DraggableAreaRect.height + (1f - DraggableAreaRect.height) / 2f)
			{
				return true;
			}
			return false;
		}
	}
}
