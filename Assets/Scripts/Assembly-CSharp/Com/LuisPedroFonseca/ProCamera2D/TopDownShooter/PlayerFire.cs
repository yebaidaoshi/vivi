using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class PlayerFire : MonoBehaviour
	{
		public Pool BulletPool;

		public Transform WeaponTip;

		public float FireRate = 0.3f;

		public float FireShakeForce = 0.2f;

		public float FireShakeDuration = 0.05f;

		private Transform _transform;

		private void Awake()
		{
			_transform = base.transform;
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
			{
				StartCoroutine(Fire());
			}
		}

		private IEnumerator Fire()
		{
			while (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
			{
				GameObject nextThing = BulletPool.nextThing;
				nextThing.transform.position = WeaponTip.position;
				nextThing.transform.rotation = _transform.rotation;
				float f = (_transform.rotation.eulerAngles.y - 90f) * ((float)Math.PI / 180f);
				Vector2 vector = new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * FireShakeForce;
				ProCamera2DShake.Instance.ApplyShakesTimed(new Vector2[1] { vector }, new Vector3[1] { Vector3.zero }, new float[1] { FireShakeDuration });
				yield return new WaitForSeconds(FireRate);
			}
		}
	}
}
