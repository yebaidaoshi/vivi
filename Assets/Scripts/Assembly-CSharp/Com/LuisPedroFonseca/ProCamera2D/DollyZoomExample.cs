using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public class DollyZoomExample : MonoBehaviour
	{
		[Range(0.1f, 179.9f)]
		public float TargetFOV = 30f;

		[Range(0f, 10f)]
		public float Duration = 2f;

		public EaseType EaseType;

		[Range(-1f, 1f)]
		public float ZoomAmount = -0.2f;

		private void OnGUI()
		{
			GUI.Label(new Rect(5f, 5f, 100f, 30f), "Target FOV", new GUIStyle());
			TargetFOV = GUI.HorizontalSlider(new Rect(100f, 5f, 100f, 30f), TargetFOV, 0.1f, 179.9f);
			GUI.Label(new Rect(5f, 35f, 100f, 30f), "Duration", new GUIStyle());
			Duration = GUI.HorizontalSlider(new Rect(100f, 35f, 100f, 30f), Duration, 0f, 10f);
			GUI.Label(new Rect(5f, 65f, 100f, 30f), "Zoom Amount", new GUIStyle());
			ZoomAmount = GUI.HorizontalSlider(new Rect(100f, 65f, 100f, 30f), ZoomAmount, -1f, 1f);
			if (GUI.Button(new Rect(5f, 95f, 150f, 30f), "Dolly Zoom"))
			{
				ProCamera2D.Instance.DollyZoom(TargetFOV, Duration, EaseType);
				ProCamera2D.Instance.Zoom(ZoomAmount, Duration, EaseType);
			}
		}
	}
}
