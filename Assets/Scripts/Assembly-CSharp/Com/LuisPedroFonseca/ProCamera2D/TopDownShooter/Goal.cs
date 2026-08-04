using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class Goal : MonoBehaviour
	{
		public GameOver GameOverScreen;

		private void OnTriggerEnter(Collider other)
		{
			GameOverScreen.ShowScreen();
		}
	}
}
