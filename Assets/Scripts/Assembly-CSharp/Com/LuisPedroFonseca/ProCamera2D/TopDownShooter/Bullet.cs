using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class Bullet : MonoBehaviour
	{
		public float BulletDuration = 1f;

		public float BulletSpeed = 50f;

		public float SkinWidth = 0.1f;

		public LayerMask CollisionMask;

		public float BulletDamage = 10f;

		private Transform _transform;

		private RaycastHit _raycastHit;

		private Vector2 _collisionPoint;

		private float _startTime;

		private bool _exploding;

		private Vector3 _lastPos;

		private void Awake()
		{
			_transform = base.transform;
		}

		private void OnEnable()
		{
			_exploding = false;
			_startTime = Time.time;
		}

		private void Update()
		{
			if (!_exploding)
			{
				_lastPos = _transform.position;
				_transform.Translate(Vector3.right * BulletSpeed * Time.deltaTime);
				if (Physics.Raycast(_lastPos, _transform.position - _lastPos, out _raycastHit, (_lastPos - _transform.position).magnitude + SkinWidth, CollisionMask))
				{
					_collisionPoint = _raycastHit.point;
					_transform.up = _raycastHit.normal;
					Collide();
				}
				if (Time.time - _startTime > BulletDuration)
				{
					Disable();
				}
			}
		}

		private void Collide()
		{
			_exploding = true;
			_transform.position = _collisionPoint;
			_raycastHit.collider.SendMessageUpwards("Hit", BulletDamage, SendMessageOptions.DontRequireReceiver);
			Disable();
		}

		private void Disable()
		{
			base.gameObject.SetActive(value: false);
		}
	}
}
