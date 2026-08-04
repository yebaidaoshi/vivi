using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public class ShakeExample : MonoBehaviour
	{
		private bool _constantShaking;

		private void OnGUI()
		{
			if (GUI.Button(new Rect(5f, 5f, 150f, 30f), "Shake"))
			{
				ShakePreset shakePreset = ProCamera2DShake.Instance.ShakePresets[Random.Range(0, ProCamera2DShake.Instance.ShakePresets.Count)];
				Debug.Log("Shake: " + shakePreset.name);
				ProCamera2DShake.Instance.Shake(shakePreset);
			}
			if (GUI.Button(new Rect(5f, 45f, 150f, 30f), _constantShaking ? "Stop Constant Shake" : "Constant Shake"))
			{
				if (_constantShaking)
				{
					_constantShaking = false;
					ProCamera2DShake.Instance.StopConstantShaking();
					return;
				}
				_constantShaking = true;
				ConstantShakePreset constantShakePreset = ProCamera2DShake.Instance.ConstantShakePresets[Random.Range(0, ProCamera2DShake.Instance.ConstantShakePresets.Count)];
				Debug.Log("ConstantShake: " + constantShakePreset.name);
				ProCamera2DShake.Instance.ConstantShake(constantShakePreset);
			}
		}
	}
}
