using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.TopDownShooter
{
	public class SwitchCameraProjection : MonoBehaviour
	{
		public string _cameraMode;

		private void Awake()
		{
			Switch();
		}

		private void OnGUI()
		{
			if (GUI.Button(new Rect(Screen.width - 210, 10f, 200f, 30f), "Switch to " + _cameraMode + " mode"))
			{
				PlayerPrefs.SetInt("orthoCamera", (!Camera.main.orthographic) ? 1 : 0);
				Switch();
			}
		}

		private void Switch()
		{
			Camera.main.orthographic = PlayerPrefs.GetInt("orthoCamera", 0) == 1;
			_cameraMode = (Camera.main.orthographic ? "perspective" : "orthographic");
		}
	}
}
