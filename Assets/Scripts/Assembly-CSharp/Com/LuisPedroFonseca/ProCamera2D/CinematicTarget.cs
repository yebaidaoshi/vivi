using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[Serializable]
	public class CinematicTarget
	{
		public Transform TargetTransform;

		public float EaseInDuration = 1f;

		public float HoldDuration = 1f;

		public float Zoom = 1f;

		public EaseType EaseType = EaseType.EaseOut;

		public string SendMessageName;

		public string SendMessageParam;
	}
}
