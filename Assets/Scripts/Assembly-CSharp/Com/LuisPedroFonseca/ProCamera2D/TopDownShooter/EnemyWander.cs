using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class EnemyWander : MonoBehaviour
	{
		public float WanderDuration = 10f;

		public float WaypointOffset = 0.1f;

		public float WanderRadius = 10f;

		private NavMeshAgent _navMeshAgent;

		private bool _hasReachedDestination;

		private Vector3 _startingPos;

		private float _startingTime;

		private void Awake()
		{
			_navMeshAgent = GetComponentInChildren<NavMeshAgent>();
			_startingPos = _navMeshAgent.transform.position;
		}

		public void StartWandering()
		{
			_startingTime = Time.time;
			GoToWaypoint();
			StartCoroutine(CheckAgentPosition());
		}

		public void StopWandering()
		{
			StopAllCoroutines();
		}

		private IEnumerator CheckAgentPosition()
		{
			while (true)
			{
				if (_navMeshAgent.remainingDistance <= WaypointOffset && !_hasReachedDestination)
				{
					_hasReachedDestination = true;
					if (Time.time - _startingTime >= WanderDuration && WanderDuration > 0f)
					{
						Debug.Log("Stopped wandering");
					}
					else
					{
						GoToWaypoint();
					}
				}
				yield return null;
			}
		}

		private void GoToWaypoint()
		{
			NavMeshPath navMeshPath = new NavMeshPath();
			Vector3 vector = Vector3.zero;
			while (navMeshPath.status == NavMeshPathStatus.PathPartial || navMeshPath.status == NavMeshPathStatus.PathInvalid)
			{
				Vector3 vector2 = Random.insideUnitSphere * WanderRadius;
				vector2.y = _startingPos.y;
				vector = _startingPos + vector2;
				_navMeshAgent.CalculatePath(vector, navMeshPath);
			}
			_navMeshAgent.SetDestination(vector);
			_hasReachedDestination = false;
		}
	}
}
