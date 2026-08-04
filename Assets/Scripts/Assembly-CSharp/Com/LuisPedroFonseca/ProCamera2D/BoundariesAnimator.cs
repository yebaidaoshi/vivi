using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public class BoundariesAnimator
	{
		public Action OnTransitionStarted;

		public Action OnTransitionFinished;

		public bool UseTopBoundary;

		public float TopBoundary;

		public bool UseBottomBoundary;

		public float BottomBoundary;

		public bool UseLeftBoundary;

		public float LeftBoundary;

		public bool UseRightBoundary;

		public float RightBoundary;

		public float TransitionDuration = 1f;

		public EaseType TransitionEaseType;

		private ProCamera2D ProCamera2D;

		private ProCamera2DNumericBoundaries NumericBoundaries;

		private Func<Vector3, float> Vector3H;

		private Func<Vector3, float> Vector3V;

		public BoundariesAnimator(ProCamera2D proCamera2D, ProCamera2DNumericBoundaries numericBoundaries)
		{
			ProCamera2D = proCamera2D;
			NumericBoundaries = numericBoundaries;
			switch (ProCamera2D.Axis)
			{
			case MovementAxis.XY:
				Vector3H = (Vector3 vector) => vector.x;
				Vector3V = (Vector3 vector) => vector.y;
				break;
			case MovementAxis.XZ:
				Vector3H = (Vector3 vector) => vector.x;
				Vector3V = (Vector3 vector) => vector.z;
				break;
			case MovementAxis.YZ:
				Vector3H = (Vector3 vector) => vector.z;
				Vector3V = (Vector3 vector) => vector.y;
				break;
			}
		}

		public int GetAnimsCount()
		{
			int num = 0;
			if (UseLeftBoundary)
			{
				num++;
			}
			else if (!UseLeftBoundary && NumericBoundaries.UseLeftBoundary && UseRightBoundary && RightBoundary < NumericBoundaries.TargetLeftBoundary)
			{
				num++;
			}
			if (UseRightBoundary)
			{
				num++;
			}
			else if (!UseRightBoundary && NumericBoundaries.UseRightBoundary && UseLeftBoundary && LeftBoundary > NumericBoundaries.TargetRightBoundary)
			{
				num++;
			}
			if (UseTopBoundary)
			{
				num++;
			}
			else if (!UseTopBoundary && NumericBoundaries.UseTopBoundary && UseBottomBoundary && BottomBoundary > NumericBoundaries.TargetTopBoundary)
			{
				num++;
			}
			if (UseBottomBoundary)
			{
				num++;
			}
			else if (!UseBottomBoundary && NumericBoundaries.UseBottomBoundary && UseTopBoundary && TopBoundary < NumericBoundaries.TargetBottomBoundary)
			{
				num++;
			}
			return num;
		}

		public void Transition()
		{
			if (!NumericBoundaries.HasFiredTransitionStarted && OnTransitionStarted != null)
			{
				NumericBoundaries.HasFiredTransitionStarted = true;
				OnTransitionStarted();
			}
			NumericBoundaries.HasFiredTransitionFinished = false;
			NumericBoundaries.UseNumericBoundaries = true;
			if (UseLeftBoundary)
			{
				NumericBoundaries.UseLeftBoundary = true;
				if (NumericBoundaries.LeftBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.LeftBoundaryAnimRoutine);
				}
				NumericBoundaries.LeftBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(LeftTransitionRoutine(TransitionDuration));
			}
			else if (!UseLeftBoundary && NumericBoundaries.UseLeftBoundary && UseRightBoundary && RightBoundary < NumericBoundaries.TargetLeftBoundary)
			{
				NumericBoundaries.UseLeftBoundary = true;
				UseLeftBoundary = true;
				LeftBoundary = RightBoundary - ProCamera2D.ScreenSizeInWorldCoordinates.x * 100f;
				if (NumericBoundaries.LeftBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.LeftBoundaryAnimRoutine);
				}
				NumericBoundaries.LeftBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(LeftTransitionRoutine(TransitionDuration, turnOffBoundaryAfterwards: true));
			}
			else if (!UseLeftBoundary)
			{
				NumericBoundaries.UseLeftBoundary = false;
			}
			if (UseRightBoundary)
			{
				NumericBoundaries.UseRightBoundary = true;
				if (NumericBoundaries.RightBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.RightBoundaryAnimRoutine);
				}
				NumericBoundaries.RightBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(RightTransitionRoutine(TransitionDuration));
			}
			else if (!UseRightBoundary && NumericBoundaries.UseRightBoundary && UseLeftBoundary && LeftBoundary > NumericBoundaries.TargetRightBoundary)
			{
				NumericBoundaries.UseRightBoundary = true;
				UseRightBoundary = true;
				RightBoundary = LeftBoundary + ProCamera2D.ScreenSizeInWorldCoordinates.x * 100f;
				if (NumericBoundaries.RightBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.RightBoundaryAnimRoutine);
				}
				NumericBoundaries.RightBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(RightTransitionRoutine(TransitionDuration, turnOffBoundaryAfterwards: true));
			}
			else if (!UseRightBoundary)
			{
				NumericBoundaries.UseRightBoundary = false;
			}
			if (UseTopBoundary)
			{
				NumericBoundaries.UseTopBoundary = true;
				if (NumericBoundaries.TopBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.TopBoundaryAnimRoutine);
				}
				NumericBoundaries.TopBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(TopTransitionRoutine(TransitionDuration));
			}
			else if (!UseTopBoundary && NumericBoundaries.UseTopBoundary && UseBottomBoundary && BottomBoundary > NumericBoundaries.TargetTopBoundary)
			{
				NumericBoundaries.UseTopBoundary = true;
				UseTopBoundary = true;
				TopBoundary = BottomBoundary + ProCamera2D.ScreenSizeInWorldCoordinates.y * 100f;
				if (NumericBoundaries.TopBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.TopBoundaryAnimRoutine);
				}
				NumericBoundaries.TopBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(TopTransitionRoutine(TransitionDuration, turnOffBoundaryAfterwards: true));
			}
			else if (!UseTopBoundary)
			{
				NumericBoundaries.UseTopBoundary = false;
			}
			if (UseBottomBoundary)
			{
				NumericBoundaries.UseBottomBoundary = true;
				if (NumericBoundaries.BottomBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.BottomBoundaryAnimRoutine);
				}
				NumericBoundaries.BottomBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(BottomTransitionRoutine(TransitionDuration));
			}
			else if (!UseBottomBoundary && NumericBoundaries.UseBottomBoundary && UseTopBoundary && TopBoundary < NumericBoundaries.TargetBottomBoundary)
			{
				NumericBoundaries.UseBottomBoundary = true;
				UseBottomBoundary = true;
				BottomBoundary = TopBoundary - ProCamera2D.ScreenSizeInWorldCoordinates.y * 100f;
				if (NumericBoundaries.BottomBoundaryAnimRoutine != null)
				{
					NumericBoundaries.StopCoroutine(NumericBoundaries.BottomBoundaryAnimRoutine);
				}
				NumericBoundaries.BottomBoundaryAnimRoutine = NumericBoundaries.StartCoroutine(BottomTransitionRoutine(TransitionDuration, turnOffBoundaryAfterwards: true));
			}
			else if (!UseBottomBoundary)
			{
				NumericBoundaries.UseBottomBoundary = false;
			}
		}

		private IEnumerator LeftTransitionRoutine(float duration, bool turnOffBoundaryAfterwards = false)
		{
			float initialLeftBoundary = Vector3H(ProCamera2D.LocalPosition) - ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
			NumericBoundaries.TargetLeftBoundary = LeftBoundary;
			float t = 0f;
			while (t <= 1f)
			{
				t += ProCamera2D.DeltaTime / duration;
				if (UseLeftBoundary && UseRightBoundary && LeftBoundary < initialLeftBoundary)
				{
					NumericBoundaries.LeftBoundary = LeftBoundary;
				}
				else if (UseLeftBoundary)
				{
					NumericBoundaries.LeftBoundary = Utils.EaseFromTo(initialLeftBoundary, LeftBoundary, t, TransitionEaseType);
					float num = Vector3H(ProCamera2D.LocalPosition) - ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
					if (num < NumericBoundaries.TargetLeftBoundary && NumericBoundaries.LeftBoundary < num)
					{
						NumericBoundaries.LeftBoundary = num;
					}
				}
				yield return ProCamera2D.GetYield();
			}
			if (turnOffBoundaryAfterwards)
			{
				NumericBoundaries.UseLeftBoundary = false;
				UseLeftBoundary = false;
			}
			if (!NumericBoundaries.HasFiredTransitionFinished && OnTransitionFinished != null)
			{
				NumericBoundaries.HasFiredTransitionStarted = false;
				NumericBoundaries.HasFiredTransitionFinished = true;
				OnTransitionFinished();
			}
		}

		private IEnumerator RightTransitionRoutine(float duration, bool turnOffBoundaryAfterwards = false)
		{
			float initialRightBoundary = Vector3H(ProCamera2D.LocalPosition) + ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
			NumericBoundaries.TargetRightBoundary = RightBoundary;
			float t = 0f;
			while (t <= 1f)
			{
				t += ProCamera2D.DeltaTime / duration;
				if (UseRightBoundary && UseLeftBoundary && RightBoundary > initialRightBoundary)
				{
					NumericBoundaries.RightBoundary = RightBoundary;
				}
				else if (UseRightBoundary)
				{
					NumericBoundaries.RightBoundary = Utils.EaseFromTo(initialRightBoundary, RightBoundary, t, TransitionEaseType);
					float num = Vector3H(ProCamera2D.LocalPosition) + ProCamera2D.ScreenSizeInWorldCoordinates.x / 2f;
					if (num > NumericBoundaries.TargetRightBoundary && NumericBoundaries.RightBoundary > num)
					{
						NumericBoundaries.RightBoundary = num;
					}
				}
				yield return ProCamera2D.GetYield();
			}
			if (turnOffBoundaryAfterwards)
			{
				NumericBoundaries.UseRightBoundary = false;
				UseRightBoundary = false;
			}
			if (!NumericBoundaries.HasFiredTransitionFinished && OnTransitionFinished != null)
			{
				NumericBoundaries.HasFiredTransitionStarted = false;
				NumericBoundaries.HasFiredTransitionFinished = true;
				OnTransitionFinished();
			}
		}

		private IEnumerator TopTransitionRoutine(float duration, bool turnOffBoundaryAfterwards = false)
		{
			float initialTopBoundary = Vector3V(ProCamera2D.LocalPosition) + ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			NumericBoundaries.TargetTopBoundary = TopBoundary;
			float t = 0f;
			while (t <= 1f)
			{
				t += ProCamera2D.DeltaTime / duration;
				if (UseTopBoundary && UseBottomBoundary && TopBoundary > initialTopBoundary)
				{
					NumericBoundaries.TopBoundary = TopBoundary;
				}
				else if (UseTopBoundary)
				{
					NumericBoundaries.TopBoundary = Utils.EaseFromTo(initialTopBoundary, TopBoundary, t, TransitionEaseType);
					float num = Vector3V(ProCamera2D.LocalPosition) + ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
					if (num > NumericBoundaries.TargetTopBoundary && NumericBoundaries.TopBoundary > num)
					{
						NumericBoundaries.TopBoundary = num;
					}
				}
				yield return ProCamera2D.GetYield();
			}
			if (turnOffBoundaryAfterwards)
			{
				NumericBoundaries.UseTopBoundary = false;
				UseTopBoundary = false;
			}
			if (!NumericBoundaries.HasFiredTransitionFinished && OnTransitionFinished != null)
			{
				NumericBoundaries.HasFiredTransitionStarted = false;
				NumericBoundaries.HasFiredTransitionFinished = true;
				OnTransitionFinished();
			}
		}

		private IEnumerator BottomTransitionRoutine(float duration, bool turnOffBoundaryAfterwards = false)
		{
			float initialBottomBoundary = Vector3V(ProCamera2D.LocalPosition) - ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
			NumericBoundaries.TargetBottomBoundary = BottomBoundary;
			float t = 0f;
			while (t <= 1f)
			{
				t += ProCamera2D.DeltaTime / duration;
				if (UseBottomBoundary && UseTopBoundary && BottomBoundary < initialBottomBoundary)
				{
					NumericBoundaries.BottomBoundary = BottomBoundary;
				}
				else if (UseBottomBoundary)
				{
					NumericBoundaries.BottomBoundary = Utils.EaseFromTo(initialBottomBoundary, BottomBoundary, t, TransitionEaseType);
					float num = Vector3V(ProCamera2D.LocalPosition) - ProCamera2D.ScreenSizeInWorldCoordinates.y / 2f;
					if (num < NumericBoundaries.TargetBottomBoundary && NumericBoundaries.BottomBoundary < num)
					{
						NumericBoundaries.BottomBoundary = num;
					}
				}
				yield return ProCamera2D.GetYield();
			}
			if (turnOffBoundaryAfterwards)
			{
				NumericBoundaries.UseBottomBoundary = false;
				UseBottomBoundary = false;
			}
			if (!NumericBoundaries.HasFiredTransitionFinished && OnTransitionFinished != null)
			{
				NumericBoundaries.HasFiredTransitionStarted = false;
				NumericBoundaries.HasFiredTransitionFinished = true;
				OnTransitionFinished();
			}
		}
	}
}
