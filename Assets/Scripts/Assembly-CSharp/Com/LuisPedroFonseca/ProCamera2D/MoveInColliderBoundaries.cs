using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public class MoveInColliderBoundaries
	{
		private Func<Vector3, float> Vector3H;

		private Func<Vector3, float> Vector3V;

		private Func<float, float, Vector3> VectorHV;

		private const float Offset = 0.2f;

		private const float RaySizeCompensation = 0.2f;

		public Transform CameraTransform;

		public Vector2 CameraSize;

		public LayerMask CameraCollisionMask;

		public int TotalHorizontalRays = 3;

		public int TotalVerticalRays = 3;

		private RaycastOrigins _raycastOrigins;

		private CameraCollisionState _cameraCollisionState;

		private RaycastHit _raycastHit;

		private float _verticalDistanceBetweenRays;

		private float _horizontalDistanceBetweenRays;

		private ProCamera2D _proCamera2D;

		public RaycastOrigins RaycastOrigins => _raycastOrigins;

		public CameraCollisionState CameraCollisionState => _cameraCollisionState;

		public MoveInColliderBoundaries(ProCamera2D proCamera2D)
		{
			_proCamera2D = proCamera2D;
			switch (_proCamera2D.Axis)
			{
			case MovementAxis.XY:
				Vector3H = (Vector3 vector) => vector.x;
				Vector3V = (Vector3 vector) => vector.y;
				VectorHV = (float h, float v) => new Vector3(h, v, 0f);
				break;
			case MovementAxis.XZ:
				Vector3H = (Vector3 vector) => vector.x;
				Vector3V = (Vector3 vector) => vector.z;
				VectorHV = (float h, float v) => new Vector3(h, 0f, v);
				break;
			case MovementAxis.YZ:
				Vector3H = (Vector3 vector) => vector.z;
				Vector3V = (Vector3 vector) => vector.y;
				VectorHV = (float h, float v) => new Vector3(0f, v, h);
				break;
			}
		}

		public Vector3 Move(Vector3 deltaMovement)
		{
			UpdateRaycastOrigins();
			GetOffsetAndForceMovement(_raycastOrigins.BottomLeft, ref deltaMovement, ref _cameraCollisionState.HBottomLeft, ref _cameraCollisionState.VBottomLeft, -1f, -1f);
			GetOffsetAndForceMovement(_raycastOrigins.BottomRight, ref deltaMovement, ref _cameraCollisionState.HBottomRight, ref _cameraCollisionState.VBottomRight, 1f, -1f);
			GetOffsetAndForceMovement(_raycastOrigins.TopLeft, ref deltaMovement, ref _cameraCollisionState.HTopLeft, ref _cameraCollisionState.VTopLeft, -1f, 1f);
			GetOffsetAndForceMovement(_raycastOrigins.TopRight, ref deltaMovement, ref _cameraCollisionState.HTopRight, ref _cameraCollisionState.VTopRight, 1f, 1f);
			float arg = 0f;
			if (Vector3H(deltaMovement) != 0f)
			{
				arg = MoveInAxis(Vector3H(deltaMovement), isHorizontal: true);
			}
			float arg2 = 0f;
			if (Vector3V(deltaMovement) != 0f)
			{
				arg2 = MoveInAxis(Vector3V(deltaMovement), isHorizontal: false);
			}
			return VectorHV(arg, arg2);
		}

		private void UpdateRaycastOrigins()
		{
			_raycastOrigins.BottomRight = VectorHV(Vector3H(CameraTransform.localPosition) + CameraSize.x / 2f, Vector3V(CameraTransform.localPosition) - CameraSize.y / 2f);
			_raycastOrigins.BottomLeft = VectorHV(Vector3H(CameraTransform.localPosition) - CameraSize.x / 2f, Vector3V(CameraTransform.localPosition) - CameraSize.y / 2f);
			_raycastOrigins.TopRight = VectorHV(Vector3H(CameraTransform.localPosition) + CameraSize.x / 2f, Vector3V(CameraTransform.localPosition) + CameraSize.y / 2f);
			_raycastOrigins.TopLeft = VectorHV(Vector3H(CameraTransform.localPosition) - CameraSize.x / 2f, Vector3V(CameraTransform.localPosition) + CameraSize.y / 2f);
			_horizontalDistanceBetweenRays = CameraSize.x / (float)(TotalVerticalRays - 1);
			_verticalDistanceBetweenRays = CameraSize.y / (float)(TotalHorizontalRays - 1);
		}

		private void GetOffsetAndForceMovement(Vector3 rayTargetPos, ref Vector3 deltaMovement, ref bool horizontalCheck, ref bool verticalCheck, float hSign, float vSign)
		{
			Vector3 vector = VectorHV(Vector3H(CameraTransform.localPosition), Vector3V(CameraTransform.localPosition));
			Vector3 normalized = (rayTargetPos - vector).normalized;
			float num = (rayTargetPos - vector).magnitude + 0.01f + 0.5f;
			DrawRay(vector, normalized * num, Color.yellow);
			if (Physics.Raycast(vector, normalized, out _raycastHit, num, CameraCollisionMask))
			{
				if (Mathf.Abs(Vector3H(_raycastHit.normal)) > Mathf.Abs(Vector3V(_raycastHit.normal)))
				{
					horizontalCheck = !verticalCheck;
					if (Vector3H(deltaMovement) == 0f)
					{
						float arg = 0.1f * hSign;
						deltaMovement = VectorHV(arg, Vector3V(deltaMovement));
						float arg2 = MoveInAxis(Vector3H(deltaMovement), isHorizontal: true);
						deltaMovement = VectorHV(arg2, Vector3V(deltaMovement));
					}
				}
				else
				{
					verticalCheck = !horizontalCheck;
					if (Vector3V(deltaMovement) == 0f)
					{
						float arg3 = 0.1f * vSign;
						deltaMovement = VectorHV(Vector3H(deltaMovement), arg3);
						float arg4 = MoveInAxis(Vector3V(deltaMovement), isHorizontal: false);
						deltaMovement = VectorHV(Vector3H(deltaMovement), arg4);
					}
				}
			}
			else
			{
				horizontalCheck = false;
				verticalCheck = false;
			}
		}

		private float MoveInAxis(float deltaMovement, bool isHorizontal)
		{
			bool flag = deltaMovement > 0f;
			float num = Mathf.Abs(deltaMovement) + 0.2f;
			Vector3 vector;
			Vector3 arg;
			if (isHorizontal)
			{
				vector = (flag ? CameraTransform.right : (-CameraTransform.right));
				arg = (flag ? _raycastOrigins.BottomRight : _raycastOrigins.BottomLeft);
			}
			else
			{
				vector = (flag ? CameraTransform.up : (-CameraTransform.up));
				arg = (flag ? _raycastOrigins.TopLeft : _raycastOrigins.BottomLeft);
			}
			float num2 = float.NegativeInfinity;
			bool flag2 = false;
			int num3 = (isHorizontal ? TotalHorizontalRays : TotalVerticalRays);
			for (int i = 0; i < num3; i++)
			{
				float num4 = (isHorizontal ? Vector3H(arg) : (Vector3H(arg) + (float)i * _horizontalDistanceBetweenRays));
				float num5 = (isHorizontal ? (Vector3V(arg) + (float)i * _verticalDistanceBetweenRays) : Vector3V(arg));
				if (isHorizontal)
				{
					if ((flag && _cameraCollisionState.VBottomRight && i == 0) || (!flag && _cameraCollisionState.VBottomLeft && i == 0))
					{
						num5 += 0.2f;
					}
					if ((flag && _cameraCollisionState.VTopRight && i == num3 - 1) || (!flag && _cameraCollisionState.VTopLeft && i == num3 - 1))
					{
						num5 -= 0.2f;
					}
				}
				else
				{
					if ((flag && _cameraCollisionState.HTopLeft && i == 0) || (!flag && _cameraCollisionState.HBottomLeft && i == 0))
					{
						num4 += 0.2f;
					}
					if ((flag && _cameraCollisionState.HTopRight && i == num3 - 1) || (!flag && _cameraCollisionState.HBottomRight && i == num3 - 1))
					{
						num4 -= 0.2f;
					}
				}
				Vector3 vector2 = VectorHV(num4, num5);
				if (Physics.Raycast(vector2, vector, out _raycastHit, num, CameraCollisionMask))
				{
					DrawRay(vector2, vector * num, Color.red);
					if ((!(isHorizontal && flag2) || !flag || !(num2 <= Vector3H(_raycastHit.point))) && (flag || !(num2 >= Vector3H(_raycastHit.point))) && (!flag2 || !flag || !(num2 <= Vector3V(_raycastHit.point))) && (flag || !(num2 >= Vector3V(_raycastHit.point))))
					{
						flag2 = true;
						deltaMovement = (isHorizontal ? (Vector3H(_raycastHit.point) - Vector3H(vector2) + (flag ? (-0.2f) : 0.2f)) : (Vector3V(_raycastHit.point) - Vector3V(vector2) + (flag ? (-0.2f) : 0.2f)));
						num2 = (isHorizontal ? Vector3H(_raycastHit.point) : Vector3V(_raycastHit.point));
					}
				}
				else
				{
					DrawRay(vector2, vector * num, Color.cyan);
				}
			}
			return deltaMovement;
		}

		private void DrawRay(Vector3 start, Vector3 dir, Color color, float duration = 0f)
		{
			if (duration != 0f)
			{
				Debug.DrawRay(start, dir, color, duration);
			}
			else
			{
				Debug.DrawRay(start, dir, color);
			}
		}
	}
}
