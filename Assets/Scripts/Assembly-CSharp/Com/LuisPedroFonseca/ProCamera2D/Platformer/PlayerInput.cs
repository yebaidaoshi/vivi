using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.Platformer
{
	[RequireComponent(typeof(CharacterController))]
	public class PlayerInput : MonoBehaviour
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

		private CharacterController _characterController;

		private void Start()
		{
			_characterController = GetComponent<CharacterController>();
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
			if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) && totalJumps < jumpsAllowed)
			{
				totalJumps++;
				amountToMove.y = jumpHeight;
			}
			float target = Input.GetAxis("Horizontal") * runSpeed;
			currentSpeed = IncrementTowards(currentSpeed, target, acceleration);
			if (base.transform.position.z != 0f)
			{
				amountToMove.z = 0f - base.transform.position.z;
			}
			amountToMove.x = currentSpeed;
			if (amountToMove.x != 0f)
			{
				Body.localScale = new Vector2(Mathf.Sign(amountToMove.x) * Mathf.Abs(Body.localScale.x), Body.localScale.y);
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
