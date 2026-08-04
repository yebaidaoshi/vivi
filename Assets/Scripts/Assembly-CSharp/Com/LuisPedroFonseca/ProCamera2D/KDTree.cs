using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public class KDTree
	{
		public KDTree[] lr;

		public Vector3 pivot;

		public int pivotIndex;

		public int axis;

		private const int numDims = 3;

		public KDTree()
		{
			lr = new KDTree[2];
		}

		public static KDTree MakeFromPoints(params Vector3[] points)
		{
			int[] inds = Iota(points.Length);
			return MakeFromPointsInner(0, 0, points.Length - 1, points, inds);
		}

		private static KDTree MakeFromPointsInner(int depth, int stIndex, int enIndex, Vector3[] points, int[] inds)
		{
			KDTree kDTree = new KDTree();
			kDTree.axis = depth % 3;
			int num = FindPivotIndex(points, inds, stIndex, enIndex, kDTree.axis);
			kDTree.pivotIndex = inds[num];
			kDTree.pivot = points[kDTree.pivotIndex];
			int num2 = num - 1;
			if (num2 >= stIndex)
			{
				kDTree.lr[0] = MakeFromPointsInner(depth + 1, stIndex, num2, points, inds);
			}
			int num3 = num + 1;
			if (num3 <= enIndex)
			{
				kDTree.lr[1] = MakeFromPointsInner(depth + 1, num3, enIndex, points, inds);
			}
			return kDTree;
		}

		private static void SwapElements(int[] arr, int a, int b)
		{
			int num = arr[a];
			arr[a] = arr[b];
			arr[b] = num;
		}

		private static int FindSplitPoint(Vector3[] points, int[] inds, int stIndex, int enIndex, int axis)
		{
			float num = points[inds[stIndex]][axis];
			float num2 = points[inds[enIndex]][axis];
			int num3 = (stIndex + enIndex) / 2;
			float num4 = points[inds[num3]][axis];
			if (num > num2)
			{
				if (num4 > num)
				{
					return stIndex;
				}
				if (num2 > num4)
				{
					return enIndex;
				}
				return num3;
			}
			if (num > num4)
			{
				return stIndex;
			}
			if (num4 > num2)
			{
				return enIndex;
			}
			return num3;
		}

		public static int FindPivotIndex(Vector3[] points, int[] inds, int stIndex, int enIndex, int axis)
		{
			int num = FindSplitPoint(points, inds, stIndex, enIndex, axis);
			Vector3 vector = points[inds[num]];
			SwapElements(inds, stIndex, num);
			int num2 = stIndex + 1;
			int num3 = enIndex;
			while (num2 <= num3)
			{
				Vector3 vector2 = points[inds[num2]];
				if (vector2[axis] > vector[axis])
				{
					SwapElements(inds, num2, num3);
					num3--;
				}
				else
				{
					SwapElements(inds, num2 - 1, num2);
					num2++;
				}
			}
			return num2 - 1;
		}

		public static int[] Iota(int num)
		{
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = i;
			}
			return array;
		}

		public int FindNearest(Vector3 pt)
		{
			float bestSqSoFar = 1E+09f;
			int bestIndex = -1;
			Search(pt, ref bestSqSoFar, ref bestIndex);
			return bestIndex;
		}

		private void Search(Vector3 pt, ref float bestSqSoFar, ref int bestIndex)
		{
			float sqrMagnitude = (pivot - pt).sqrMagnitude;
			if (sqrMagnitude < bestSqSoFar)
			{
				bestSqSoFar = sqrMagnitude;
				bestIndex = pivotIndex;
			}
			float num = pt[axis] - pivot[axis];
			int num2 = ((!(num <= 0f)) ? 1 : 0);
			if (lr[num2] != null)
			{
				lr[num2].Search(pt, ref bestSqSoFar, ref bestIndex);
			}
			num2 = (num2 + 1) % 2;
			float num3 = num * num;
			if (lr[num2] != null && bestSqSoFar > num3)
			{
				lr[num2].Search(pt, ref bestSqSoFar, ref bestIndex);
			}
		}

		private float DistFromSplitPlane(Vector3 pt, Vector3 planePt, int axis)
		{
			return pt[axis] - planePt[axis];
		}

		public string Dump(int level)
		{
			string text = pivotIndex.ToString().PadLeft(level) + "\n";
			if (lr[0] != null)
			{
				text += lr[0].Dump(level + 2);
			}
			if (lr[1] != null)
			{
				text += lr[1].Dump(level + 2);
			}
			return text;
		}
	}
}
