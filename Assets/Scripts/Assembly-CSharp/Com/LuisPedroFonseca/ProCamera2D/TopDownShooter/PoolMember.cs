using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class PoolMember : MonoBehaviour
	{
		public Pool pool;

		private void OnDisable()
		{
			pool.nextThing = base.gameObject;
		}
	}
}
