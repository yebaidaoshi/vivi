using System.Collections.Generic;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-repeater/")]
	public class ProCamera2DRepeater : BasePC2D, IPostMover
	{
		public static string ExtensionName = "Repeater";

		public Transform ObjectToRepeat;

		public Vector2 ObjectSize = new Vector2(2f, 2f);

		public Vector2 ObjectBottomLeft = Vector2.zero;

		public bool ObjectOnStage = true;

		public bool _repeatHorizontal = true;

		public bool _repeatVertical = true;

		public Camera CameraToUse;

		private Transform _cameraToUseTransform;

		private Vector3 _objStartPosition;

		private List<RepeatedObject> _allRepeatedObjects;

		private Queue<RepeatedObject> _inactiveRepeatedObjects;

		private IntPoint _prevStartIndex;

		private IntPoint _prevEndIndex;

		private Dictionary<IntPoint, bool> _occupiedIndices;

		private int _pmOrder = 2000;

		public bool RepeatHorizontal
		{
			get
			{
				return _repeatHorizontal;
			}
			set
			{
				_repeatHorizontal = value;
				Refresh();
			}
		}

		public bool RepeatVertical
		{
			get
			{
				return _repeatVertical;
			}
			set
			{
				_repeatVertical = value;
				Refresh();
			}
		}

		public int PMOrder
		{
			get
			{
				return _pmOrder;
			}
			set
			{
				_pmOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			SetRepeatingObject(ObjectToRepeat, ObjectOnStage);
			_cameraToUseTransform = CameraToUse.transform;
			base.ProCamera2D.AddPostMover(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePostMover(this);
			}
		}

		public void PostMove(float deltaTime)
		{
			if (base.enabled)
			{
				Vector2 screenSizeInWorldCoords = Utils.GetScreenSizeInWorldCoords(CameraToUse, Vector3D(base.ProCamera2D.LocalPosition - _objStartPosition));
				Vector3 position = _cameraToUseTransform.position;
				Vector2 vector = new Vector2(Vector3H(position) - screenSizeInWorldCoords.x / 2f, Vector3V(position) - screenSizeInWorldCoords.y / 2f);
				Vector2 vector2 = new Vector2(vector.x - _objStartPosition.x - ObjectBottomLeft.x, vector.y - _objStartPosition.y - ObjectBottomLeft.y);
				IntPoint intPoint = new IntPoint(Mathf.FloorToInt(vector2.x / ObjectSize.x), Mathf.FloorToInt(vector2.y / ObjectSize.y));
				IntPoint intPoint2 = new IntPoint(Mathf.CeilToInt(screenSizeInWorldCoords.x / ObjectSize.x), Mathf.CeilToInt(screenSizeInWorldCoords.y / ObjectSize.y));
				IntPoint intPoint3 = new IntPoint(intPoint.X + intPoint2.X, intPoint.Y + intPoint2.Y);
				if (!intPoint.Equals(_prevStartIndex) || !intPoint3.Equals(_prevEndIndex))
				{
					FreeOutOfRangeObjects(intPoint, intPoint3);
					FillGrid(intPoint, intPoint3);
				}
				_prevStartIndex = intPoint;
				_prevEndIndex = intPoint3;
			}
		}

		public void SetRepeatingObject(Transform objectToRepeat, bool isExistingObject)
		{
			ObjectToRepeat = objectToRepeat;
			if (ObjectToRepeat == null)
			{
				Debug.LogWarning("ProCamera2D Repeater extension - No ObjectToRepeat defined!");
				return;
			}
			_objStartPosition = new Vector3(Vector3H(ObjectToRepeat.position), Vector3V(ObjectToRepeat.position), Vector3D(ObjectToRepeat.position));
			if (_allRepeatedObjects != null && _allRepeatedObjects.Count > 0)
			{
				for (int i = 0; i < _allRepeatedObjects.Count; i++)
				{
					Object.Destroy(_allRepeatedObjects[i].Transform.gameObject);
				}
			}
			_allRepeatedObjects = new List<RepeatedObject>();
			_inactiveRepeatedObjects = new Queue<RepeatedObject>();
			_occupiedIndices = new Dictionary<IntPoint, bool>();
			_prevStartIndex = default(IntPoint);
			_prevEndIndex = default(IntPoint);
			if (isExistingObject)
			{
				InitCopy(ObjectToRepeat);
			}
		}

		private void FreeOutOfRangeObjects(IntPoint startIndex, IntPoint endIndex)
		{
			for (int i = 0; i < _allRepeatedObjects.Count; i++)
			{
				RepeatedObject repeatedObject = _allRepeatedObjects[i];
				if ((repeatedObject.GridPos.X != int.MaxValue && (repeatedObject.GridPos.X < startIndex.X || repeatedObject.GridPos.X > endIndex.X)) || (repeatedObject.GridPos.Y != int.MaxValue && (repeatedObject.GridPos.Y < startIndex.Y || repeatedObject.GridPos.Y > endIndex.Y)))
				{
					_occupiedIndices.Remove(repeatedObject.GridPos);
					_inactiveRepeatedObjects.Enqueue(repeatedObject);
					PositionObject(repeatedObject, IntPoint.MaxValue);
				}
			}
		}

		private void FillGrid(IntPoint startIndex, IntPoint endIndex)
		{
			if (!_repeatHorizontal)
			{
				startIndex.X = 0;
				endIndex.X = 0;
			}
			if (!_repeatVertical)
			{
				startIndex.Y = 0;
				endIndex.Y = 0;
			}
			for (int i = startIndex.X; i <= endIndex.X; i++)
			{
				for (int j = startIndex.Y; j <= endIndex.Y; j++)
				{
					IntPoint intPoint = new IntPoint(i, j);
					bool value = false;
					if (!_occupiedIndices.TryGetValue(intPoint, out value))
					{
						if (_inactiveRepeatedObjects.Count == 0)
						{
							InitCopy(Object.Instantiate(ObjectToRepeat), positionOffscreen: false);
						}
						_occupiedIndices[intPoint] = true;
						RepeatedObject obj = _inactiveRepeatedObjects.Dequeue();
						PositionObject(obj, intPoint);
					}
				}
			}
		}

		private void InitCopy(Transform newCopy, bool positionOffscreen = true)
		{
			RepeatedObject repeatedObject = new RepeatedObject
			{
				Transform = newCopy
			};
			repeatedObject.Transform.parent = ObjectToRepeat.parent;
			_allRepeatedObjects.Add(repeatedObject);
			_inactiveRepeatedObjects.Enqueue(repeatedObject);
			if (positionOffscreen)
			{
				PositionObject(repeatedObject, IntPoint.MaxValue);
			}
		}

		private void PositionObject(RepeatedObject obj, IntPoint index)
		{
			obj.GridPos = index;
			obj.Transform.position = VectorHVD(_objStartPosition.x + (float)index.X * ObjectSize.x, _objStartPosition.y + (float)index.Y * ObjectSize.y, _objStartPosition.z);
		}

		private void Refresh()
		{
			FreeOutOfRangeObjects(IntPoint.MaxValue, IntPoint.MaxValue);
			FillGrid(_prevStartIndex, _prevEndIndex);
		}
	}
}
