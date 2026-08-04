using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[Serializable]
	[CreateAssetMenu(menuName = "ProCamera2D/Shake Preset")]
	public class ShakePreset : ScriptableObject
	{
		public Vector3 Strength = new Vector2(10f, 10f);

		[Range(0.02f, 3f)]
		public float Duration = 0.5f;

		[Range(1f, 100f)]
		public int Vibrato = 10;

		[Range(0f, 1f)]
		public float Randomness = 0.1f;

		[Range(0f, 0.5f)]
		public float Smoothness = 0.1f;

		public bool UseRandomInitialAngle = true;

		[Range(0f, 360f)]
		public float InitialAngle;

		public Vector3 Rotation;

		public bool IgnoreTimeScale;
	}
}
