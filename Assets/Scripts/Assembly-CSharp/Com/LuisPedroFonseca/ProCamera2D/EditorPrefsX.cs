using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public static class EditorPrefsX
	{
		private enum ArrayType
		{
			Float = 0,
			Int32 = 1,
			Bool = 2,
			String = 3,
			Vector2 = 4,
			Vector3 = 5,
			Quaternion = 6,
			Color = 7
		}

		private static int endianDiff1;

		private static int endianDiff2;

		private static int idx;

		private static byte[] byteBlock;

		public static bool SetBool(string name, bool value)
		{
			return true;
		}

		public static bool GetBool(string name)
		{
			return true;
		}

		public static bool GetBool(string name, bool defaultValue)
		{
			return true;
		}

		public static long GetLong(string key, long defaultValue)
		{
			return 0L;
		}

		public static long GetLong(string key)
		{
			return 0L;
		}

		private static void SplitLong(long input, out int lowBits, out int highBits)
		{
			lowBits = (int)input;
			highBits = (int)(input >> 32);
		}

		public static void SetLong(string key, long value)
		{
		}

		public static bool SetVector2(string key, Vector2 vector)
		{
			return SetFloatArray(key, new float[2] { vector.x, vector.y });
		}

		private static Vector2 GetVector2(string key)
		{
			float[] floatArray = GetFloatArray(key);
			if (floatArray.Length < 2)
			{
				return Vector2.zero;
			}
			return new Vector2(floatArray[0], floatArray[1]);
		}

		public static Vector2 GetVector2(string key, Vector2 defaultValue)
		{
			return Vector2.zero;
		}

		public static bool SetVector3(string key, Vector3 vector)
		{
			return SetFloatArray(key, new float[3] { vector.x, vector.y, vector.z });
		}

		public static Vector3 GetVector3(string key)
		{
			float[] floatArray = GetFloatArray(key);
			if (floatArray.Length < 3)
			{
				return Vector3.zero;
			}
			return new Vector3(floatArray[0], floatArray[1], floatArray[2]);
		}

		public static Vector3 GetVector3(string key, Vector3 defaultValue)
		{
			return Vector3.zero;
		}

		public static bool SetQuaternion(string key, Quaternion vector)
		{
			return SetFloatArray(key, new float[4] { vector.x, vector.y, vector.z, vector.w });
		}

		public static Quaternion GetQuaternion(string key)
		{
			float[] floatArray = GetFloatArray(key);
			if (floatArray.Length < 4)
			{
				return Quaternion.identity;
			}
			return new Quaternion(floatArray[0], floatArray[1], floatArray[2], floatArray[3]);
		}

		public static Quaternion GetQuaternion(string key, Quaternion defaultValue)
		{
			return Quaternion.identity;
		}

		public static bool SetColor(string key, Color color)
		{
			return SetFloatArray(key, new float[4] { color.r, color.g, color.b, color.a });
		}

		public static Color GetColor(string key)
		{
			float[] floatArray = GetFloatArray(key);
			if (floatArray.Length < 4)
			{
				return new Color(0f, 0f, 0f, 0f);
			}
			return new Color(floatArray[0], floatArray[1], floatArray[2], floatArray[3]);
		}

		public static Color GetColor(string key, Color defaultValue)
		{
			return Color.white;
		}

		public static bool SetBoolArray(string key, bool[] boolArray)
		{
			byte[] array = new byte[(boolArray.Length + 7) / 8 + 5];
			array[0] = Convert.ToByte(ArrayType.Bool);
			int num = 1;
			int num2 = 5;
			for (int i = 0; i < boolArray.Length; i++)
			{
				if (boolArray[i])
				{
					array[num2] |= (byte)num;
				}
				num <<= 1;
				if (num > 128)
				{
					num = 1;
					num2++;
				}
			}
			Initialize();
			ConvertInt32ToBytes(boolArray.Length, array);
			return SaveBytes(key, array);
		}

		public static bool[] GetBoolArray(string key)
		{
			if (PlayerPrefs.HasKey(key))
			{
				byte[] array = Convert.FromBase64String(PlayerPrefs.GetString(key));
				if (array.Length < 5)
				{
					Debug.LogError("Corrupt preference file for " + key);
					return new bool[0];
				}
				if (array[0] != 2)
				{
					Debug.LogError(key + " is not a boolean array");
					return new bool[0];
				}
				Initialize();
				bool[] array2 = new bool[ConvertBytesToInt32(array)];
				int num = 1;
				int num2 = 5;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i] = (array[num2] & (byte)num) != 0;
					num <<= 1;
					if (num > 128)
					{
						num = 1;
						num2++;
					}
				}
				return array2;
			}
			return new bool[0];
		}

		public static bool[] GetBoolArray(string key, bool defaultValue, int defaultSize)
		{
			return new bool[0];
		}

		public static bool SetStringArray(string key, string[] stringArray)
		{
			return true;
		}

		public static string[] GetStringArray(string key)
		{
			return new string[0];
		}

		public static string[] GetStringArray(string key, string defaultValue, int defaultSize)
		{
			return new string[0];
		}

		public static bool SetIntArray(string key, int[] intArray)
		{
			return SetValue(key, intArray, ArrayType.Int32, 1, ConvertFromInt);
		}

		public static bool SetFloatArray(string key, float[] floatArray)
		{
			return SetValue(key, floatArray, ArrayType.Float, 1, ConvertFromFloat);
		}

		public static bool SetVector2Array(string key, Vector2[] vector2Array)
		{
			return SetValue(key, vector2Array, ArrayType.Vector2, 2, ConvertFromVector2);
		}

		public static bool SetVector3Array(string key, Vector3[] vector3Array)
		{
			return SetValue(key, vector3Array, ArrayType.Vector3, 3, ConvertFromVector3);
		}

		public static bool SetQuaternionArray(string key, Quaternion[] quaternionArray)
		{
			return SetValue(key, quaternionArray, ArrayType.Quaternion, 4, ConvertFromQuaternion);
		}

		public static bool SetColorArray(string key, Color[] colorArray)
		{
			return SetValue(key, colorArray, ArrayType.Color, 4, ConvertFromColor);
		}

		private static bool SetValue<T>(string key, T array, ArrayType arrayType, int vectorNumber, Action<T, byte[], int> convert) where T : IList
		{
			byte[] array2 = new byte[4 * array.Count * vectorNumber + 1];
			array2[0] = Convert.ToByte(arrayType);
			Initialize();
			for (int i = 0; i < array.Count; i++)
			{
				convert(array, array2, i);
			}
			return SaveBytes(key, array2);
		}

		private static void ConvertFromInt(int[] array, byte[] bytes, int i)
		{
			ConvertInt32ToBytes(array[i], bytes);
		}

		private static void ConvertFromFloat(float[] array, byte[] bytes, int i)
		{
			ConvertFloatToBytes(array[i], bytes);
		}

		private static void ConvertFromVector2(Vector2[] array, byte[] bytes, int i)
		{
			ConvertFloatToBytes(array[i].x, bytes);
			ConvertFloatToBytes(array[i].y, bytes);
		}

		private static void ConvertFromVector3(Vector3[] array, byte[] bytes, int i)
		{
			ConvertFloatToBytes(array[i].x, bytes);
			ConvertFloatToBytes(array[i].y, bytes);
			ConvertFloatToBytes(array[i].z, bytes);
		}

		private static void ConvertFromQuaternion(Quaternion[] array, byte[] bytes, int i)
		{
			ConvertFloatToBytes(array[i].x, bytes);
			ConvertFloatToBytes(array[i].y, bytes);
			ConvertFloatToBytes(array[i].z, bytes);
			ConvertFloatToBytes(array[i].w, bytes);
		}

		private static void ConvertFromColor(Color[] array, byte[] bytes, int i)
		{
			ConvertFloatToBytes(array[i].r, bytes);
			ConvertFloatToBytes(array[i].g, bytes);
			ConvertFloatToBytes(array[i].b, bytes);
			ConvertFloatToBytes(array[i].a, bytes);
		}

		public static int[] GetIntArray(string key)
		{
			List<int> list = new List<int>();
			GetValue(key, list, ArrayType.Int32, 1, ConvertToInt);
			return list.ToArray();
		}

		public static int[] GetIntArray(string key, int defaultValue, int defaultSize)
		{
			return new int[0];
		}

		public static float[] GetFloatArray(string key)
		{
			List<float> list = new List<float>();
			GetValue(key, list, ArrayType.Float, 1, ConvertToFloat);
			return list.ToArray();
		}

		public static float[] GetFloatArray(string key, float defaultValue, int defaultSize)
		{
			return new float[0];
		}

		public static Vector2[] GetVector2Array(string key)
		{
			List<Vector2> list = new List<Vector2>();
			GetValue(key, list, ArrayType.Vector2, 2, ConvertToVector2);
			return list.ToArray();
		}

		public static Vector2[] GetVector2Array(string key, Vector2 defaultValue, int defaultSize)
		{
			return new Vector2[0];
		}

		public static Vector3[] GetVector3Array(string key)
		{
			List<Vector3> list = new List<Vector3>();
			GetValue(key, list, ArrayType.Vector3, 3, ConvertToVector3);
			return list.ToArray();
		}

		public static Vector3[] GetVector3Array(string key, Vector3 defaultValue, int defaultSize)
		{
			return new Vector3[0];
		}

		public static Quaternion[] GetQuaternionArray(string key)
		{
			List<Quaternion> list = new List<Quaternion>();
			GetValue(key, list, ArrayType.Quaternion, 4, ConvertToQuaternion);
			return list.ToArray();
		}

		public static Quaternion[] GetQuaternionArray(string key, Quaternion defaultValue, int defaultSize)
		{
			return new Quaternion[0];
		}

		public static Color[] GetColorArray(string key)
		{
			List<Color> list = new List<Color>();
			GetValue(key, list, ArrayType.Color, 4, ConvertToColor);
			return list.ToArray();
		}

		public static Color[] GetColorArray(string key, Color defaultValue, int defaultSize)
		{
			return new Color[0];
		}

		private static void GetValue<T>(string key, T list, ArrayType arrayType, int vectorNumber, Action<T, byte[]> convert) where T : IList
		{
		}

		private static void ConvertToInt(List<int> list, byte[] bytes)
		{
			list.Add(ConvertBytesToInt32(bytes));
		}

		private static void ConvertToFloat(List<float> list, byte[] bytes)
		{
			list.Add(ConvertBytesToFloat(bytes));
		}

		private static void ConvertToVector2(List<Vector2> list, byte[] bytes)
		{
			list.Add(new Vector2(ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes)));
		}

		private static void ConvertToVector3(List<Vector3> list, byte[] bytes)
		{
			list.Add(new Vector3(ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes)));
		}

		private static void ConvertToQuaternion(List<Quaternion> list, byte[] bytes)
		{
			list.Add(new Quaternion(ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes)));
		}

		private static void ConvertToColor(List<Color> list, byte[] bytes)
		{
			list.Add(new Color(ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes), ConvertBytesToFloat(bytes)));
		}

		public static void ShowArrayType(string key)
		{
		}

		private static void Initialize()
		{
			if (BitConverter.IsLittleEndian)
			{
				endianDiff1 = 0;
				endianDiff2 = 0;
			}
			else
			{
				endianDiff1 = 3;
				endianDiff2 = 1;
			}
			if (byteBlock == null)
			{
				byteBlock = new byte[4];
			}
			idx = 1;
		}

		private static bool SaveBytes(string key, byte[] bytes)
		{
			return true;
		}

		private static void ConvertFloatToBytes(float f, byte[] bytes)
		{
			byteBlock = BitConverter.GetBytes(f);
			ConvertTo4Bytes(bytes);
		}

		private static float ConvertBytesToFloat(byte[] bytes)
		{
			ConvertFrom4Bytes(bytes);
			return BitConverter.ToSingle(byteBlock, 0);
		}

		private static void ConvertInt32ToBytes(int i, byte[] bytes)
		{
			byteBlock = BitConverter.GetBytes(i);
			ConvertTo4Bytes(bytes);
		}

		private static int ConvertBytesToInt32(byte[] bytes)
		{
			ConvertFrom4Bytes(bytes);
			return BitConverter.ToInt32(byteBlock, 0);
		}

		private static void ConvertTo4Bytes(byte[] bytes)
		{
			bytes[idx] = byteBlock[endianDiff1];
			bytes[idx + 1] = byteBlock[1 + endianDiff2];
			bytes[idx + 2] = byteBlock[2 - endianDiff2];
			bytes[idx + 3] = byteBlock[3 - endianDiff1];
			idx += 4;
		}

		private static void ConvertFrom4Bytes(byte[] bytes)
		{
			byteBlock[endianDiff1] = bytes[idx];
			byteBlock[1 + endianDiff2] = bytes[idx + 1];
			byteBlock[2 - endianDiff2] = bytes[idx + 2];
			byteBlock[3 - endianDiff1] = bytes[idx + 3];
			idx += 4;
		}
	}
}
