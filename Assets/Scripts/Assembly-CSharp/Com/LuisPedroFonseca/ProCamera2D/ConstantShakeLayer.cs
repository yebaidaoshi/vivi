using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[Serializable]
	public struct ConstantShakeLayer
	{
		[MinMaxSlider(0.001f, 10f)]
		public Vector2 Frequency;

		[Range(0f, 100f)]
		public float AmplitudeHorizontal;

		[Range(0f, 100f)]
		public float AmplitudeVertical;

		[Range(0f, 100f)]
		public float AmplitudeDepth;
	}
}
