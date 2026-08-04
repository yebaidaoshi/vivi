using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	[RequireComponent(typeof(EnemySight))]
	[RequireComponent(typeof(EnemyAttack))]
	[RequireComponent(typeof(EnemyWander))]
	public class EnemyFSM : MonoBehaviour
	{
		public int Health = 100;

		public Color AttackColor = Color.red;

		public DoorKey Key;

		private EnemySight _sight;

		private EnemyAttack _attack;

		private EnemyWander _wander;

		private Renderer[] _renderers;

		private Color _originalColor;

		private Color _currentColor;

		private void Awake()
		{
			_sight = GetComponent<EnemySight>();
			_attack = GetComponent<EnemyAttack>();
			_wander = GetComponent<EnemyWander>();
			_renderers = GetComponentsInChildren<Renderer>();
			_originalColor = _renderers[0].material.color;
			_currentColor = _originalColor;
			EnemySight sight = _sight;
			sight.OnPlayerInSight = (Action<Transform>)Delegate.Combine(sight.OnPlayerInSight, new Action<Transform>(OnPlayerInSight));
			EnemySight sight2 = _sight;
			sight2.OnPlayerOutOfSight = (Action)Delegate.Combine(sight2.OnPlayerOutOfSight, new Action(OnPlayerOutOfSight));
			if (Key != null)
			{
				Key.gameObject.SetActive(value: false);
			}
		}

		private void Start()
		{
			_wander.StartWandering();
		}

		private void OnDestroy()
		{
			EnemySight sight = _sight;
			sight.OnPlayerInSight = (Action<Transform>)Delegate.Remove(sight.OnPlayerInSight, new Action<Transform>(OnPlayerInSight));
			EnemySight sight2 = _sight;
			sight2.OnPlayerOutOfSight = (Action)Delegate.Remove(sight2.OnPlayerOutOfSight, new Action(OnPlayerOutOfSight));
		}

		private void Hit(int damage)
		{
			if (Health > 0)
			{
				Health -= damage;
				StartCoroutine(HitAnim());
				if (Health <= 0)
				{
					Die();
				}
			}
		}

		private IEnumerator HitAnim()
		{
			Colorize(Color.white);
			yield return new WaitForSeconds(0.05f);
			Colorize(_currentColor);
		}

		private void OnPlayerInSight(Transform obj)
		{
			_wander.StopWandering();
			_attack.Attack(obj);
			ProCamera2D.Instance.AddCameraTarget(base.transform);
			_currentColor = AttackColor;
			Colorize(_currentColor);
		}

		private void OnPlayerOutOfSight()
		{
			_wander.StartWandering();
			_attack.StopAttack();
			ProCamera2D.Instance.RemoveCameraTarget(base.transform, 2f);
			_currentColor = _originalColor;
			Colorize(_currentColor);
		}

		private void Colorize(Color color)
		{
			for (int i = 0; i < _renderers.Length; i++)
			{
				_renderers[i].material.color = color;
			}
		}

		private void DropLoot()
		{
			if (Key != null)
			{
				Key.gameObject.SetActive(value: true);
				Key.transform.position = base.transform.position + new Vector3(0f, 3f, 0f);
			}
		}

		private void Die()
		{
			ProCamera2DShake.Instance.Shake("SmallExplosion");
			OnPlayerOutOfSight();
			DropLoot();
			UnityEngine.Object.Destroy(base.gameObject, 0.2f);
		}
	}
}
