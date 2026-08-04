using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-geometry-boundaries/")]
	public class ProCamera2DGeometryBoundaries : BasePC2D, IPositionDeltaChanger
	{
		public static string ExtensionName = "Geometry Boundaries";

		[Tooltip("The layer that contains the (3d) colliders that define the boundaries for the camera")]
		public LayerMask BoundariesLayerMask;

		public MoveInColliderBoundaries MoveInColliderBoundaries;

		private int _pdcOrder = 3000;

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
			MoveInColliderBoundaries = new MoveInColliderBoundaries(base.ProCamera2D);
			MoveInColliderBoundaries.CameraTransform = base.ProCamera2D.transform;
			MoveInColliderBoundaries.CameraCollisionMask = BoundariesLayerMask;
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
			MoveInColliderBoundaries.CameraSize = base.ProCamera2D.ScreenSizeInWorldCoordinates;
			return MoveInColliderBoundaries.Move(originalDelta);
		}
	}
}
