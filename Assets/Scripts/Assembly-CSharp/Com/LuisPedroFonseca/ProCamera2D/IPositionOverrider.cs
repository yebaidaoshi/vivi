using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public interface IPositionOverrider
	{
		int POOrder { get; set; }

		Vector3 OverridePosition(float deltaTime, Vector3 originalPosition);
	}
}
