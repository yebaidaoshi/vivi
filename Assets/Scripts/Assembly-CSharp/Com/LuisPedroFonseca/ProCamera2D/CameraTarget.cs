using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[Serializable]
	public class CameraTarget
	{
		public Transform TargetTransform;

		[Range(0f, 1f)]
		public float TargetInfluenceH = 1f;

		[Range(0f, 1f)]
		public float TargetInfluenceV = 1f;

		public Vector2 TargetOffset;

		private Vector3 _targetPosition;

		public float TargetInfluence
		{
			set
			{
				TargetInfluenceH = value;
				TargetInfluenceV = value;
			}
		}

		public Vector3 TargetPosition
		{
			get
			{
				if (TargetTransform != null)
				{
					return _targetPosition = TargetTransform.position;
				}
				return _targetPosition;
			}
		}
	}
}
