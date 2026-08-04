using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class DoorKey : MonoBehaviour
	{
		public Door Door;

		public string PickupTag = "Player";

		public ProCamera2DCinematics Cinematics;

		private void OnTriggerEnter(Collider other)
		{
			if (other.transform.CompareTag(PickupTag) && !Door.IsOpen)
			{
				Door.OpenDoor();
				if (Cinematics != null)
				{
					Cinematics.Play();
				}
				Object.Destroy(base.gameObject);
			}
		}
	}
}
