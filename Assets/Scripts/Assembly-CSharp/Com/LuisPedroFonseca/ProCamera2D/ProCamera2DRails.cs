using System.Collections.Generic;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-rails/")]
	public class ProCamera2DRails : BasePC2D, IPreMover
	{
		public static string ExtensionName = "Rails";

		[HideInInspector]
		public List<Vector3> RailNodes = new List<Vector3>();

		public FollowMode FollowMode;

		public List<CameraTarget> CameraTargets = new List<CameraTarget>();

		private Dictionary<CameraTarget, Transform> _cameraTargetsOnRails = new Dictionary<CameraTarget, Transform>();

		private List<CameraTarget> _tempCameraTargets = new List<CameraTarget>();

		private KDTree _kdTree;

		private int _prmOrder = 1000;

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
			_kdTree = KDTree.MakeFromPoints(RailNodes.ToArray());
			for (int i = 0; i < CameraTargets.Count; i++)
			{
				Transform transform = new GameObject(CameraTargets[i].TargetTransform.name + "_OnRails").transform;
				_cameraTargetsOnRails.Add(CameraTargets[i], transform);
				base.ProCamera2D.AddCameraTarget(transform).TargetOffset = CameraTargets[i].TargetOffset;
			}
			if (CameraTargets.Count == 0)
			{
				base.enabled = false;
			}
			base.ProCamera2D.AddPreMover(this);
			Step();
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
			if (base.enabled)
			{
				Step();
			}
		}

		private void Step()
		{
			Vector3 pos = Vector3.zero;
			for (int i = 0; i < CameraTargets.Count; i++)
			{
				switch (FollowMode)
				{
				case FollowMode.HorizontalAxis:
					pos = VectorHVD(Vector3H(CameraTargets[i].TargetPosition) * CameraTargets[i].TargetInfluenceH, Vector3V(base.ProCamera2D.LocalPosition), 0f);
					break;
				case FollowMode.VerticalAxis:
					pos = VectorHVD(Vector3H(base.ProCamera2D.LocalPosition), Vector3V(CameraTargets[i].TargetPosition) * CameraTargets[i].TargetInfluenceV, 0f);
					break;
				case FollowMode.BothAxis:
					pos = VectorHVD(Vector3H(CameraTargets[i].TargetPosition) * CameraTargets[i].TargetInfluenceH, Vector3V(CameraTargets[i].TargetPosition) * CameraTargets[i].TargetInfluenceV, 0f);
					break;
				}
				_cameraTargetsOnRails[CameraTargets[i]].position = GetPositionOnRail(pos);
			}
		}

		public void AddRailsTarget(Transform targetTransform, float targetInfluenceH = 1f, float targetInfluenceV = 1f, Vector2 targetOffset = default(Vector2))
		{
			if (GetRailsTarget(targetTransform) == null)
			{
				CameraTarget cameraTarget = new CameraTarget
				{
					TargetTransform = targetTransform,
					TargetInfluenceH = targetInfluenceH,
					TargetInfluenceV = targetInfluenceV,
					TargetOffset = targetOffset
				};
				CameraTargets.Add(cameraTarget);
				Transform transform = new GameObject(targetTransform.name + "_OnRails").transform;
				_cameraTargetsOnRails.Add(cameraTarget, transform);
				base.ProCamera2D.AddCameraTarget(transform);
				base.enabled = true;
			}
		}

		public void RemoveRailsTarget(Transform targetTransform)
		{
			CameraTarget railsTarget = GetRailsTarget(targetTransform);
			if (railsTarget != null)
			{
				CameraTargets.Remove(railsTarget);
				base.ProCamera2D.RemoveCameraTarget(_cameraTargetsOnRails[railsTarget]);
			}
		}

		public CameraTarget GetRailsTarget(Transform targetTransform)
		{
			for (int i = 0; i < CameraTargets.Count; i++)
			{
				if (CameraTargets[i].TargetTransform.GetInstanceID() == targetTransform.GetInstanceID())
				{
					return CameraTargets[i];
				}
			}
			return null;
		}

		public void DisableTargets(float transitionDuration = 0f)
		{
			if (_tempCameraTargets.Count == 0)
			{
				for (int i = 0; i < _cameraTargetsOnRails.Count; i++)
				{
					base.ProCamera2D.RemoveCameraTarget(_cameraTargetsOnRails[CameraTargets[i]], transitionDuration);
					_tempCameraTargets.Add(base.ProCamera2D.AddCameraTarget(CameraTargets[i].TargetTransform, CameraTargets[i].TargetInfluenceH, CameraTargets[i].TargetInfluenceV, transitionDuration, CameraTargets[i].TargetOffset));
				}
			}
		}

		public void EnableTargets(float transitionDuration = 0f)
		{
			for (int i = 0; i < _tempCameraTargets.Count; i++)
			{
				base.ProCamera2D.RemoveCameraTarget(_tempCameraTargets[i].TargetTransform, transitionDuration);
				base.ProCamera2D.AddCameraTarget(_cameraTargetsOnRails[CameraTargets[i]], 1f, 1f, transitionDuration);
			}
			_tempCameraTargets.Clear();
		}

		private Vector3 GetPositionOnRail(Vector3 pos)
		{
			int num = _kdTree.FindNearest(pos);
			if (num == 0)
			{
				return GetPositionOnRailSegment(RailNodes[0], RailNodes[1], pos);
			}
			if (num == RailNodes.Count - 1)
			{
				return GetPositionOnRailSegment(RailNodes[RailNodes.Count - 1], RailNodes[RailNodes.Count - 2], pos);
			}
			Vector3 positionOnRailSegment = GetPositionOnRailSegment(RailNodes[num - 1], RailNodes[num], pos);
			Vector3 positionOnRailSegment2 = GetPositionOnRailSegment(RailNodes[num + 1], RailNodes[num], pos);
			if ((pos - positionOnRailSegment).sqrMagnitude <= (pos - positionOnRailSegment2).sqrMagnitude)
			{
				return positionOnRailSegment;
			}
			return positionOnRailSegment2;
		}

		private Vector3 GetPositionOnRailSegment(Vector3 node1, Vector3 node2, Vector3 pos)
		{
			Vector3 rhs = pos - node1;
			Vector3 normalized = (node2 - node1).normalized;
			float num = Vector3.Dot(normalized, rhs);
			if (num < 0f)
			{
				return node1;
			}
			if (num * num > (node2 - node1).sqrMagnitude)
			{
				return node2;
			}
			Vector3 vector = normalized * num;
			return node1 + vector;
		}
	}
}
