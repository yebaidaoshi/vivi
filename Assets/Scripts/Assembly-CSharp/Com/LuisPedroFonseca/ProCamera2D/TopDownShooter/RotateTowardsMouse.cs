using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class RotateTowardsMouse : MonoBehaviour
	{
		public float Ease = 0.15f;

		private Transform _transform;

		private void Awake()
		{
			_transform = base.transform;
		}

		private void Update()
		{
			Vector3 mousePosition = Input.mousePosition;
			Vector3 vector = Camera.main.WorldToScreenPoint(_transform.localPosition);
			Vector2 vector2 = new Vector2(mousePosition.x - vector.x, mousePosition.y - vector.y);
			float num = Mathf.Atan2(vector2.y, vector2.x) * 57.29578f;
			_transform.rotation = Quaternion.Slerp(_transform.rotation, Quaternion.Euler(0f, 0f - num, 0f), Ease);
		}
	}
}
