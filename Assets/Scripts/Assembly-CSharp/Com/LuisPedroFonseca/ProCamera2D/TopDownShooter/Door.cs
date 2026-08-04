using System.Collections;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class Door : MonoBehaviour
	{
		private bool _isOpen;

		public DoorDirection DoorDirection;

		public float MovementRange = 5f;

		public float AnimDuration = 1f;

		public float OpenDelay;

		public float CloseDelay;

		private Vector3 _origPos;

		private Coroutine _moveCoroutine;

		public bool IsOpen => _isOpen;

		private void Awake()
		{
			_origPos = base.transform.position;
		}

		public void OpenDoor(float openDelay = -1f)
		{
			if (openDelay == -1f)
			{
				openDelay = OpenDelay;
			}
			_isOpen = true;
			switch (DoorDirection)
			{
			case DoorDirection.Up:
				Move(_origPos + new Vector3(0f, 0f, MovementRange), AnimDuration, openDelay);
				break;
			case DoorDirection.Down:
				Move(_origPos - new Vector3(0f, 0f, MovementRange), AnimDuration, openDelay);
				break;
			case DoorDirection.Left:
				Move(_origPos - new Vector3(MovementRange, 0f, 0f), AnimDuration, openDelay);
				break;
			case DoorDirection.Right:
				Move(_origPos + new Vector3(MovementRange, 0f, 0f), AnimDuration, openDelay);
				break;
			}
		}

		public void CloseDoor()
		{
			_isOpen = false;
			Move(_origPos, AnimDuration, CloseDelay);
		}

		private void Move(Vector3 newPos, float duration, float delay)
		{
			if (_moveCoroutine != null)
			{
				StopCoroutine(_moveCoroutine);
			}
			_moveCoroutine = StartCoroutine(MoveRoutine(newPos, duration, delay));
		}

		private IEnumerator MoveRoutine(Vector3 newPos, float duration, float delay)
		{
			yield return new WaitForSeconds(delay);
			Vector3 origPos = base.transform.position;
			float t = 0f;
			while (t <= 1f)
			{
				t += Time.deltaTime / duration;
				base.transform.position = new Vector3(Utils.EaseFromTo(origPos.x, newPos.x, t, EaseType.EaseOut), Utils.EaseFromTo(origPos.y, newPos.y, t, EaseType.EaseOut), Utils.EaseFromTo(origPos.z, newPos.z, t, EaseType.EaseOut));
				yield return null;
			}
		}
	}
}
