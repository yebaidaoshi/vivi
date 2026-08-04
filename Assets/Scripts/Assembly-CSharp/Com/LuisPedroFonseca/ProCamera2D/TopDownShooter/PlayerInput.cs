using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	[RequireComponent(typeof(CharacterController))]
	public class PlayerInput : MonoBehaviour
	{
		public float RunSpeed = 12f;

		public float Acceleration = 30f;

		private float _currentSpeedH;

		private float _currentSpeedV;

		private Vector3 _amountToMove;

		private int _totalJumps;

		private CharacterController _characterController;

		private bool _movementAllowed = true;

		private void Start()
		{
			_characterController = GetComponent<CharacterController>();
			ProCamera2DCinematics[] array = Object.FindObjectsOfType<ProCamera2DCinematics>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].OnCinematicStarted.AddListener(delegate
				{
					_movementAllowed = false;
					_currentSpeedH = 0f;
					_currentSpeedV = 0f;
				});
				array[i].OnCinematicFinished.AddListener(delegate
				{
					_movementAllowed = true;
				});
			}
		}

		private void Update()
		{
			if (_movementAllowed)
			{
				float target = Input.GetAxis("Horizontal") * RunSpeed;
				_currentSpeedH = IncrementTowards(_currentSpeedH, target, Acceleration);
				float target2 = Input.GetAxis("Vertical") * RunSpeed;
				_currentSpeedV = IncrementTowards(_currentSpeedV, target2, Acceleration);
				_amountToMove.x = _currentSpeedH;
				_amountToMove.z = _currentSpeedV;
				_characterController.Move(_amountToMove * Time.deltaTime);
			}
		}

		private float IncrementTowards(float n, float target, float a)
		{
			if (n == target)
			{
				return n;
			}
			float num = Mathf.Sign(target - n);
			n += a * Time.deltaTime * num;
			if (num != Mathf.Sign(target - n))
			{
				return target;
			}
			return n;
		}
	}
}
