using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class ObjectRotate : MonoBehaviour
	{
		public Vector3 Rotation = Vector3.one;

		private Transform _transform;

		private void Awake()
		{
			_transform = base.transform;
		}

		private void LateUpdate()
		{
			_transform.Rotate(Rotation);
		}
	}
}
