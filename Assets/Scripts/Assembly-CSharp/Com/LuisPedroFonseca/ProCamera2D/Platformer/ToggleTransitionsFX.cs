using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D.Platformer
{
	public class ToggleTransitionsFX : MonoBehaviour
	{
		private void OnGUI()
		{
			if (GUI.Button(new Rect(5f, 5f, 180f, 30f), "Transition Enter"))
			{
				ProCamera2DTransitionsFX.Instance.TransitionEnter();
			}
			if (GUI.Button(new Rect(5f, 45f, 180f, 30f), "Transition Exit"))
			{
				ProCamera2DTransitionsFX.Instance.TransitionExit();
			}
		}
	}
}
