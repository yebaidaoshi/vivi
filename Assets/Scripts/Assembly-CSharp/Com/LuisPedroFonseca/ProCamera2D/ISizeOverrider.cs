namespace Com.LuisPedroFonseca.ProCamera2D
{
	public interface ISizeOverrider
	{
		int SOOrder { get; set; }

		float OverrideSize(float deltaTime, float originalSize);
	}
}
