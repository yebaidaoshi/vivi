namespace Com.LuisPedroFonseca.ProCamera2D
{
	public interface ISizeDeltaChanger
	{
		int SDCOrder { get; set; }

		float AdjustSize(float deltaTime, float originalDelta);
	}
}
