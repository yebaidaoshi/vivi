using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public interface IPositionDeltaChanger
	{
		int PDCOrder { get; set; }

		Vector3 AdjustDelta(float deltaTime, Vector3 originalDelta);
	}
}
