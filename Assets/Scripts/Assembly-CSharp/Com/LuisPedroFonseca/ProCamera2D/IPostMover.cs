namespace Com.LuisPedroFonseca.ProCamera2D
{
	public interface IPostMover
	{
		int PMOrder { get; set; }

		void PostMove(float deltaTime);
	}
}
