using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class EnemyPatrol : MonoBehaviour
	{
		public Transform PathContainer;

		public float WaypointOffset = 0.1f;

		public bool Loop = true;

		public bool IsPaused;

		private NavMeshAgent _navMeshAgent;

		private List<Transform> _path;

		private int _currentWaypoint;

		private bool _hasReachedDestination;

		private float _stopTime;

		private void Awake()
		{
			_navMeshAgent = GetComponentInChildren<NavMeshAgent>();
			_path = new List<Transform>();
			if (PathContainer != null)
			{
				foreach (Transform item in PathContainer)
				{
					_path.Add(item);
				}
				return;
			}
			Debug.LogWarning("No path set.");
		}

		private void Update()
		{
			if (IsPaused)
			{
				return;
			}
			if (_navMeshAgent.remainingDistance <= WaypointOffset && !_hasReachedDestination)
			{
				_hasReachedDestination = true;
				PatrolWaypoint component = _path[_currentWaypoint].GetComponent<PatrolWaypoint>();
				if (component != null)
				{
					_stopTime = Random.Range(component.StopDuration - component.StopDurationVariation, component.StopDuration + component.StopDurationVariation);
					if (Random.value >= component.StopProbability)
					{
						GoToNextWaypoint();
					}
				}
				else
				{
					GoToNextWaypoint();
				}
			}
			if (_hasReachedDestination)
			{
				_stopTime -= Time.deltaTime;
				if (_stopTime <= 0f)
				{
					GoToNextWaypoint();
				}
			}
		}

		public void StartPatrol()
		{
			GoToWaypoint(0);
		}

		public void PausePatrol()
		{
			IsPaused = true;
			_navMeshAgent.isStopped = true;
		}

		public void ResumePatrol()
		{
			GoToWaypoint(_currentWaypoint);
		}

		private void GoToNextWaypoint()
		{
			if (_currentWaypoint < _path.Count - 1)
			{
				_currentWaypoint++;
			}
			else if (Loop)
			{
				_currentWaypoint = 0;
			}
			else
			{
				Debug.Log("Path completed");
			}
			GoToWaypoint(_currentWaypoint);
		}

		private void GoToWaypoint(int waypoint)
		{
			IsPaused = false;
			_hasReachedDestination = false;
			_currentWaypoint = waypoint;
			Vector3 destination = new Vector3(_path[_currentWaypoint].position.x, _navMeshAgent.transform.position.y, _path[_currentWaypoint].position.z);
			_navMeshAgent.SetDestination(destination);
		}
	}
}
