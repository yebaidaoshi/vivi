using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-rooms/")]
	public class ProCamera2DRooms : BasePC2D, IPositionOverrider, ISizeOverrider
	{
		public const string ExtensionName = "Rooms";

		private int _currentRoomIndex = -1;

		private int _previousRoomIndex = -1;

		public List<Room> Rooms = new List<Room>();

		public float UpdateInterval = 0.1f;

		public bool UseTargetsMidPoint = true;

		public Transform TriggerTarget;

		public bool TransitionInstanlyOnStart = true;

		public bool RestoreOnRoomExit;

		public float RestoreDuration = 1f;

		public EaseType RestoreEaseType;

		public bool AutomaticRoomActivation = true;

		public bool UseRelativePosition;

		public RoomEvent OnStartedTransition;

		public RoomEvent OnFinishedTransition;

		public UnityEvent OnExitedAllRooms;

		private ProCamera2DNumericBoundaries _numericBoundaries;

		private NumericBoundariesSettings _defaultNumericBoundariesSettings;

		private bool _transitioning;

		private Vector3 _newPos;

		private float _newSize;

		private Coroutine _transitionRoutine;

		private int _currentRoomID = -1;

		private int _poOrder = 1001;

		private int _soOrder = 3001;

		public int CurrentRoomIndex => _currentRoomIndex;

		public int PreviousRoomIndex => _previousRoomIndex;

		public Room CurrentRoom
		{
			get
			{
				if (_currentRoomIndex < 0 || _currentRoomIndex >= Rooms.Count)
				{
					return null;
				}
				return Rooms[_currentRoomIndex];
			}
		}

		public float OriginalSize { get; private set; }

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
			_numericBoundaries = base.ProCamera2D.GetComponent<ProCamera2DNumericBoundaries>();
			_defaultNumericBoundariesSettings = _numericBoundaries.Settings;
			OriginalSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			base.ProCamera2D.AddPositionOverrider(this);
			base.ProCamera2D.AddSizeOverrider(this);
		}

		private void Start()
		{
			int instanceID = GetInstanceID();
			int num = 0;
			foreach (Room room in Rooms)
			{
				room.InternalID = instanceID + num;
				num++;
			}
			StartCoroutine(TestRoomRoutine());
			if (TransitionInstanlyOnStart)
			{
				Vector3 targetPos = base.ProCamera2D.TargetsMidPoint;
				if (!UseTargetsMidPoint && TriggerTarget != null)
				{
					targetPos = TriggerTarget.position;
				}
				int num2 = ComputeCurrentRoom(targetPos);
				if (num2 != -1)
				{
					EnterRoom(num2, useTransition: false);
				}
			}
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
			if (_transitioning)
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
			if (_transitioning)
			{
				return _newSize;
			}
			return originalSize;
		}

		public void TestRoom()
		{
			Vector3 targetPos = base.ProCamera2D.TargetsMidPoint;
			if (!UseTargetsMidPoint && TriggerTarget != null)
			{
				targetPos = TriggerTarget.position;
			}
			int num = ComputeCurrentRoom(targetPos);
			if (num != -1 && _currentRoomIndex != num)
			{
				EnterRoom(num);
			}
			if (num == -1 && _currentRoomIndex != -1)
			{
				ExitRoom();
			}
		}

		public int ComputeCurrentRoom(Vector3 targetPos)
		{
			int result = -1;
			for (int i = 0; i < Rooms.Count; i++)
			{
				if (Utils.IsInsideRectangle(Rooms[i].Dimensions.x + (UseRelativePosition ? _transform.position.x : 0f), Rooms[i].Dimensions.y + (UseRelativePosition ? _transform.position.y : 0f), Rooms[i].Dimensions.width, Rooms[i].Dimensions.height, Vector3H(targetPos), Vector3V(targetPos)))
				{
					result = i;
				}
			}
			return result;
		}

		public void EnterRoom(int roomIndex, bool useTransition = true, bool forceEntrance = false)
		{
			if (roomIndex < 0 || roomIndex > Rooms.Count - 1)
			{
				throw new Exception("Can't find room with index: " + roomIndex);
			}
			if (forceEntrance || Rooms[roomIndex].InternalID != _currentRoomID)
			{
				_previousRoomIndex = _currentRoomIndex;
				_currentRoomIndex = roomIndex;
				_currentRoomID = Rooms[roomIndex].InternalID;
				TransitionToRoom(Rooms[_currentRoomIndex], useTransition);
				if (OnStartedTransition != null)
				{
					OnStartedTransition.Invoke(roomIndex, _previousRoomIndex);
				}
			}
		}

		public void EnterRoom(string roomID, bool useTransition = true, bool forceEntrance = false)
		{
			int num = Rooms.FindIndex((Room room) => room.ID == roomID);
			if (num < 0)
			{
				throw new Exception("Can't find room with ID: " + roomID);
			}
			EnterRoom(num, useTransition, forceEntrance);
		}

		public void ExitRoom()
		{
			_currentRoomIndex = -1;
			_currentRoomID = -1;
			if (RestoreOnRoomExit)
			{
				if (OnStartedTransition != null)
				{
					OnStartedTransition.Invoke(_currentRoomIndex, _previousRoomIndex);
				}
				if (_transitionRoutine != null)
				{
					StopCoroutine(_transitionRoutine);
				}
				_transitionRoutine = StartCoroutine(TransitionRoutine(_defaultNumericBoundariesSettings, OriginalSize, RestoreDuration, RestoreEaseType));
			}
			if (OnExitedAllRooms != null)
			{
				OnExitedAllRooms.Invoke();
			}
		}

		public void AddRoom(float roomX, float roomY, float roomWidth, float roomHeight, float transitionDuration = 1f, EaseType transitionEaseType = EaseType.EaseInOut, bool scaleToFit = false, bool zoom = false, float zoomScale = 1.5f, string id = "")
		{
			Room item = new Room
			{
				ID = id,
				Dimensions = new Rect(roomX, roomY, roomWidth, roomHeight),
				TransitionDuration = transitionDuration,
				TransitionEaseType = transitionEaseType,
				ScaleCameraToFit = scaleToFit,
				Zoom = zoom,
				ZoomScale = zoomScale
			};
			Rooms.Add(item);
		}

		public void RemoveRoom(string roomName)
		{
			Room room = Rooms.Find((Room obj) => obj.ID == roomName);
			if (room != null)
			{
				Rooms.Remove(room);
			}
			else
			{
				Debug.LogWarning(roomName + " not found in the Rooms list.");
			}
		}

		public void SetDefaultNumericBoundariesSettings(NumericBoundariesSettings settings)
		{
			_defaultNumericBoundariesSettings = settings;
		}

		public Room GetRoom(string roomID)
		{
			return Rooms.Find((Room obj) => obj.ID == roomID);
		}

		public float GetCameraSizeForRoom(Rect roomRect)
		{
			float num = roomRect.width / base.ProCamera2D.ScreenSizeInWorldCoordinates.x;
			float num2 = roomRect.height / base.ProCamera2D.ScreenSizeInWorldCoordinates.y;
			if (num < num2)
			{
				return roomRect.width / base.ProCamera2D.GameCamera.aspect / 2f;
			}
			return roomRect.height / 2f;
		}

		private IEnumerator TestRoomRoutine()
		{
			yield return new WaitForEndOfFrame();
			WaitForSeconds waitForSeconds = new WaitForSeconds(UpdateInterval);
			WaitForSecondsRealtime waitForSecondsRealtime = new WaitForSecondsRealtime(UpdateInterval);
			while (true)
			{
				if (AutomaticRoomActivation)
				{
					TestRoom();
				}
				if (base.ProCamera2D.IgnoreTimeScale)
				{
					yield return waitForSecondsRealtime;
				}
				else
				{
					yield return waitForSeconds;
				}
			}
		}

		private void TransitionToRoom(Room room, bool useTransition = true)
		{
			if (_transitionRoutine != null)
			{
				StopCoroutine(_transitionRoutine);
			}
			NumericBoundariesSettings numericBoundariesSettings = new NumericBoundariesSettings
			{
				UseNumericBoundaries = true,
				UseTopBoundary = true,
				TopBoundary = room.Dimensions.y + (UseRelativePosition ? _transform.position.y : 0f) + room.Dimensions.height / 2f,
				UseBottomBoundary = true,
				BottomBoundary = room.Dimensions.y + (UseRelativePosition ? _transform.position.y : 0f) - room.Dimensions.height / 2f,
				UseLeftBoundary = true,
				LeftBoundary = room.Dimensions.x + (UseRelativePosition ? _transform.position.x : 0f) - room.Dimensions.width / 2f,
				UseRightBoundary = true,
				RightBoundary = room.Dimensions.x + (UseRelativePosition ? _transform.position.x : 0f) + room.Dimensions.width / 2f
			};
			float num = base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			float cameraSizeForRoom = GetCameraSizeForRoom(room.Dimensions);
			if (room.ScaleCameraToFit)
			{
				num = cameraSizeForRoom;
			}
			else if (room.Zoom && OriginalSize * room.ZoomScale <= cameraSizeForRoom)
			{
				num = OriginalSize * room.ZoomScale;
			}
			else if (cameraSizeForRoom < num)
			{
				num = cameraSizeForRoom;
			}
			_transitionRoutine = StartCoroutine(TransitionRoutine(numericBoundariesSettings, num, useTransition ? room.TransitionDuration : 0f, room.TransitionEaseType));
		}

		private IEnumerator TransitionRoutine(NumericBoundariesSettings numericBoundariesSettings, float targetSize, float transitionDuration = 1f, EaseType transitionEaseType = EaseType.EaseOut)
		{
			_transitioning = true;
			_numericBoundaries.UseNumericBoundaries = false;
			float initialSize = base.ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			float initialCamPosH = Vector3H(base.ProCamera2D.LocalPosition);
			float initialCamPosV = Vector3V(base.ProCamera2D.LocalPosition);
			float t = 0f;
			while (t <= 1f)
			{
				if (transitionDuration < float.Epsilon)
				{
					t = 1.1f;
				}
				else if (base.ProCamera2D.DeltaTime > float.Epsilon)
				{
					t += base.ProCamera2D.DeltaTime / transitionDuration;
				}
				_newSize = Utils.EaseFromTo(initialSize, targetSize, t, transitionEaseType);
				float horizontalPos = base.ProCamera2D.CameraTargetPositionSmoothed.x;
				float verticalPos = base.ProCamera2D.CameraTargetPositionSmoothed.y;
				LimitToNumericBoundaries(ref horizontalPos, ref verticalPos, targetSize * base.ProCamera2D.GameCamera.aspect, targetSize, numericBoundariesSettings);
				float arg = Utils.EaseFromTo(initialCamPosH, horizontalPos, t, transitionEaseType);
				float arg2 = Utils.EaseFromTo(initialCamPosV, verticalPos, t, transitionEaseType);
				_newPos = VectorHVD(arg, arg2, 0f);
				yield return base.ProCamera2D.GetYield();
			}
			_transitioning = false;
			_numericBoundaries.Settings = numericBoundariesSettings;
			_transitionRoutine = null;
			if (OnFinishedTransition != null)
			{
				OnFinishedTransition.Invoke(_currentRoomIndex, _previousRoomIndex);
			}
			_previousRoomIndex = _currentRoomIndex;
		}

		private void LimitToNumericBoundaries(ref float horizontalPos, ref float verticalPos, float halfCameraWidth, float halfCameraHeight, NumericBoundariesSettings numericBoundaries)
		{
			if (numericBoundaries.UseLeftBoundary && horizontalPos - halfCameraWidth < numericBoundaries.LeftBoundary)
			{
				horizontalPos = numericBoundaries.LeftBoundary + halfCameraWidth;
			}
			else if (numericBoundaries.UseRightBoundary && horizontalPos + halfCameraWidth > numericBoundaries.RightBoundary)
			{
				horizontalPos = numericBoundaries.RightBoundary - halfCameraWidth;
			}
			if (numericBoundaries.UseBottomBoundary && verticalPos - halfCameraHeight < numericBoundaries.BottomBoundary)
			{
				verticalPos = numericBoundaries.BottomBoundary + halfCameraHeight;
			}
			else if (numericBoundaries.UseTopBoundary && verticalPos + halfCameraHeight > numericBoundaries.TopBoundary)
			{
				verticalPos = numericBoundaries.TopBoundary - halfCameraHeight;
			}
		}
	}
}
