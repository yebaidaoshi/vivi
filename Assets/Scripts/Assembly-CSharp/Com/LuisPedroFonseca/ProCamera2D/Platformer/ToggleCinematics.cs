using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.Platformer
{
	public class ToggleCinematics : MonoBehaviour
	{
		public ProCamera2DCinematics Cinematics;

		private void OnGUI()
		{
			if (GUI.Button(new Rect(5f, 5f, 180f, 30f), (Cinematics.IsPlaying ? "Stop" : "Start") + " Cinematics"))
			{
				if (Cinematics.IsPlaying)
				{
					Cinematics.Stop();
				}
				else
				{
					Cinematics.Play();
				}
			}
			if (Cinematics.IsPlaying && GUI.Button(new Rect(195f, 5f, 40f, 30f), ">"))
			{
				Cinematics.GoToNextTarget();
			}
		}
	}
}
