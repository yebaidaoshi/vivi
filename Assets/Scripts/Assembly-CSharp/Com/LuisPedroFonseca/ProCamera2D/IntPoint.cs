using System;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	internal struct IntPoint : IEquatable<IntPoint>
	{
		public static IntPoint MaxValue = new IntPoint
		{
			X = int.MaxValue,
			Y = int.MaxValue
		};

		public int X;

		public int Y;

		public IntPoint(int x, int y)
		{
			X = x;
			Y = y;
		}

		public bool IsEqual(IntPoint other)
		{
			if (other.X == X)
			{
				return other.Y == Y;
			}
			return false;
		}

		public override string ToString()
		{
			return string.Format("X: " + X + " - Y: " + Y);
		}

		public bool Equals(IntPoint other)
		{
			if (other.X == X)
			{
				return other.Y == Y;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return ((0 ^ X) * 397) ^ Y;
		}
	}
}
