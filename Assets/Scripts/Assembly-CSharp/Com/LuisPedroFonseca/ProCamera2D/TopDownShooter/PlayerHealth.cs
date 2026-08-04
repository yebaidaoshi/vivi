using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class PlayerHealth : MonoBehaviour
	{
		public int Health = 100;

		private Renderer[] _renderers;

		private Color _originalColor;

		private void Awake()
		{
			_renderers = GetComponentsInChildren<Renderer>();
			_originalColor = _renderers[0].material.color;
		}

		private void Hit(int damage)
		{
			Health -= damage;
			StartCoroutine(HitAnim());
			_ = Health;
			_ = 0;
		}

		private IEnumerator HitAnim()
		{
			ProCamera2DShake.Instance.Shake("PlayerHit");
			for (int i = 0; i < _renderers.Length; i++)
			{
				_renderers[i].material.color = Color.white;
			}
			yield return new WaitForSeconds(0.05f);
			for (int j = 0; j < _renderers.Length; j++)
			{
				_renderers[j].material.color = _originalColor;
			}
		}
	}
}
