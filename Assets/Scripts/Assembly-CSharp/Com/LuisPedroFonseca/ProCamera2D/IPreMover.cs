namespace Com.LuisPedroFonseca.ProCamera2D
{
	public interface IPreMover
	{
		int PrMOrder { get; set; }

		void PreMove(float deltaTime);
	}
}
