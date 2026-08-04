using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.Platformer
{
	[RequireComponent(typeof(CharacterController))]
	public class PlayerInputBot : MonoBehaviour
	{
		public Transform Body;

		public float gravity = 20f;

		public float runSpeed = 12f;

		public float acceleration = 30f;

		public float jumpHeight = 12f;

		public int jumpsAllowed = 2;

		private float currentSpeed;

		private Vector3 amountToMove;

		private int totalJumps;

		private bool _fakeInputJump;

		private float _fakeInputHorizontalAxis;

		private CharacterController _characterController;

		private void Start()
		{
			_characterController = GetComponent<CharacterController>();
			StartCoroutine(RandomInputJump());
			StartCoroutine(RandomInputSpeed());
		}

		private IEnumerator RandomInputJump()
		{
			WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
			while (true)
			{
				_fakeInputJump = true;
				yield return waitForEndOfFrame;
				yield return waitForEndOfFrame;
				_fakeInputJump = false;
				yield return new WaitForSeconds(Random.Range(0.2f, 1f));
			}
		}

		private IEnumerator RandomInputSpeed()
		{
			while (true)
			{
				_fakeInputHorizontalAxis = Random.Range(-1f, 1f);
				yield return new WaitForSeconds(Random.Range(1f, 3f));
			}
		}

		private void Update()
		{
			if ((_characterController.collisionFlags & CollisionFlags.Sides) != CollisionFlags.None)
			{
				currentSpeed = 0f;
			}
			if ((_characterController.collisionFlags & CollisionFlags.Below) != CollisionFlags.None)
			{
				amountToMove.y = -1f;
				totalJumps = 0;
			}
			else
			{
				amountToMove.y -= gravity * Time.deltaTime;
			}
			if (_fakeInputJump && totalJumps < jumpsAllowed)
			{
				totalJumps++;
				amountToMove.y = jumpHeight;
			}
			float target = _fakeInputHorizontalAxis * runSpeed;
			currentSpeed = IncrementTowards(currentSpeed, target, acceleration);
			if (base.transform.position.z != 0f)
			{
				amountToMove.z = 0f - base.transform.position.z;
			}
			amountToMove.x = currentSpeed;
			if (amountToMove.x != 0f)
			{
				Body.localScale = new Vector2(Mathf.Sign(amountToMove.x), 1f);
			}
			_characterController.Move(amountToMove * Time.deltaTime);
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
