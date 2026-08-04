using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class ObjectMove : MonoBehaviour
	{
		public float Amplitude = 1f;

		public float Frequency = 1f;

		private Transform _transform;

		private void Awake()
		{
			_transform = base.transform;
		}

		private void LateUpdate()
		{
			_transform.position += Amplitude * (Mathf.Sin((float)Math.PI * 2f * Frequency * Time.time) - Mathf.Sin((float)Math.PI * 2f * Frequency * (Time.time - Time.deltaTime))) * Vector3.up;
		}
	}
}
