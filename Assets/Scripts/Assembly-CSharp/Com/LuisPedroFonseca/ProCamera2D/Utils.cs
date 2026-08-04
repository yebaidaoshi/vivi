using System;
using System.Collections.Generic;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public static class Utils
	{
		public static float EaseFromTo(float start, float end, float value, EaseType type = EaseType.EaseInOut)
		{
			value = Mathf.Clamp01(value);
			switch (type)
			{
			case EaseType.EaseInOut:
				return Mathf.Lerp(start, end, value * value * (3f - 2f * value));
			case EaseType.EaseOut:
				return Mathf.Lerp(start, end, Mathf.Sin(value * (float)Math.PI * 0.5f));
			case EaseType.EaseIn:
				return Mathf.Lerp(start, end, 1f - Mathf.Cos(value * (float)Math.PI * 0.5f));
			default:
				return Mathf.Lerp(start, end, value);
			}
		}

		public static float SmoothApproach(float pastPosition, float pastTargetPosition, float targetPosition, float speed, float deltaTime)
		{
			float num = deltaTime * speed;
			float num2 = (targetPosition - pastTargetPosition) / num;
			float num3 = pastPosition - pastTargetPosition + num2;
			return targetPosition - num2 + num3 * Mathf.Exp(0f - num);
		}

		public static float Remap(this float value, float from1, float to1, float from2, float to2)
		{
			return Mathf.Clamp((value - from1) / (to1 - from1) * (to2 - from2) + from2, from2, to2);
		}

		public static void DrawArrowForGizmo(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
		{
			Gizmos.DrawRay(pos, direction);
			DrawArrowEnd(gizmos: true, pos, direction, Gizmos.color, arrowHeadLength, arrowHeadAngle);
		}

		public static void DrawArrowForGizmo(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
		{
			Gizmos.DrawRay(pos, direction);
			DrawArrowEnd(gizmos: true, pos, direction, color, arrowHeadLength, arrowHeadAngle);
		}

		public static void DrawArrowForDebug(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
		{
			Debug.DrawRay(pos, direction);
			DrawArrowEnd(gizmos: false, pos, direction, Gizmos.color, arrowHeadLength, arrowHeadAngle);
		}

		public static void DrawArrowForDebug(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
		{
			Debug.DrawRay(pos, direction, color);
			DrawArrowEnd(gizmos: false, pos, direction, color, arrowHeadLength, arrowHeadAngle);
		}

		private static void DrawArrowEnd(bool gizmos, Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
		{
			if (!(direction == Vector3.zero))
			{
				Vector3 vector = Quaternion.LookRotation(direction) * Quaternion.Euler(arrowHeadAngle, 0f, 0f) * Vector3.back;
				Vector3 vector2 = Quaternion.LookRotation(direction) * Quaternion.Euler(0f - arrowHeadAngle, 0f, 0f) * Vector3.back;
				Vector3 vector3 = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, arrowHeadAngle, 0f) * Vector3.back;
				Vector3 vector4 = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f - arrowHeadAngle, 0f) * Vector3.back;
				if (gizmos)
				{
					Gizmos.color = color;
					Gizmos.DrawRay(pos + direction, vector * arrowHeadLength);
					Gizmos.DrawRay(pos + direction, vector2 * arrowHeadLength);
					Gizmos.DrawRay(pos + direction, vector3 * arrowHeadLength);
					Gizmos.DrawRay(pos + direction, vector4 * arrowHeadLength);
				}
				else
				{
					Debug.DrawRay(pos + direction, vector * arrowHeadLength, color);
					Debug.DrawRay(pos + direction, vector2 * arrowHeadLength, color);
					Debug.DrawRay(pos + direction, vector3 * arrowHeadLength, color);
					Debug.DrawRay(pos + direction, vector4 * arrowHeadLength, color);
				}
			}
		}

		public static bool AreNearlyEqual(float a, float b, float tolerance = 0.02f)
		{
			return Mathf.Abs(a - b) < tolerance;
		}

		public static Vector2 GetScreenSizeInWorldCoords(Camera gameCamera, float distance = 10f)
		{
			float num = 0f;
			float num2 = 0f;
			if (gameCamera.orthographic)
			{
				if (gameCamera.orthographicSize <= 0.001f)
				{
					return Vector2.zero;
				}
				Vector3 vector = gameCamera.ViewportToWorldPoint(new Vector3(0f, 0f, gameCamera.nearClipPlane));
				Vector3 vector2 = gameCamera.ViewportToWorldPoint(new Vector3(1f, 0f, gameCamera.nearClipPlane));
				Vector3 vector3 = gameCamera.ViewportToWorldPoint(new Vector3(1f, 1f, gameCamera.nearClipPlane));
				num = (vector2 - vector).magnitude;
				num2 = (vector3 - vector2).magnitude;
			}
			else
			{
				num2 = 2f * Mathf.Abs(distance) * Mathf.Tan(gameCamera.fieldOfView * 0.5f * ((float)Math.PI / 180f));
				num = num2 * gameCamera.aspect;
			}
			return new Vector2(num, num2);
		}

		public static Vector3 GetVectorsSum(IList<Vector3> input)
		{
			Vector3 zero = Vector3.zero;
			for (int i = 0; i < input.Count; i++)
			{
				zero += input[i];
			}
			return zero;
		}

		public static float AlignToGrid(float input, float gridSize)
		{
			return Mathf.Round(Mathf.Round(input / gridSize) * gridSize / gridSize) * gridSize;
		}

		public static bool IsInsideRectangle(float x, float y, float width, float height, float pointX, float pointY)
		{
			if (pointX >= x - width * 0.5f && pointX <= x + width * 0.5f && pointY >= y - height * 0.5f && pointY <= y + height * 0.5f)
			{
				return true;
			}
			return false;
		}

		public static bool IsInsideCircle(float x, float y, float radius, float pointX, float pointY)
		{
			return (pointX - x) * (pointX - x) + (pointY - y) * (pointY - y) < radius * radius;
		}
	}
}
