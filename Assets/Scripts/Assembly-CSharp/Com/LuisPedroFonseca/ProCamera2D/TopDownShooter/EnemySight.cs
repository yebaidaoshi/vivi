using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class EnemySight : MonoBehaviour
	{
		public Action<Transform> OnPlayerInSight;

		public Action OnPlayerOutOfSight;

		public float RefreshRate = 1f;

		public float fieldOfViewAngle = 110f;

		public float ViewDistance = 30f;

		public bool playerInSight;

		public Transform player;

		public LayerMask LayerMask;

		private RaycastHit _hit;

		private void Awake()
		{
			RefreshRate += UnityEngine.Random.Range((0f - RefreshRate) * 0.2f, RefreshRate * 0.2f);
		}

		private IEnumerator Start()
		{
			while (true)
			{
				Vector3 vector = player.position - base.transform.position;
				if (Vector3.Angle(vector, base.transform.forward) < fieldOfViewAngle * 0.5f && Physics.Raycast(base.transform.position + base.transform.up, vector.normalized, out _hit, ViewDistance, LayerMask) && _hit.collider.transform.GetInstanceID() == player.GetInstanceID())
				{
					if (!playerInSight)
					{
						playerInSight = true;
						if (OnPlayerInSight != null)
						{
							OnPlayerInSight(_hit.collider.transform);
						}
					}
				}
				else if (playerInSight)
				{
					playerInSight = false;
					if (OnPlayerOutOfSight != null)
					{
						OnPlayerOutOfSight();
					}
				}
				yield return new WaitForSeconds(RefreshRate);
			}
		}
	}
}
