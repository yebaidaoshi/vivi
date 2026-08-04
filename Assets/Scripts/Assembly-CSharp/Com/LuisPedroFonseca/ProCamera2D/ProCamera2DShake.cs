using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-shake/")]
	public class ProCamera2DShake : BasePC2D
	{
		public static string ExtensionName = "Shake";

		private static ProCamera2DShake _instance;

		public Action OnShakeCompleted;

		public List<ShakePreset> ShakePresets = new List<ShakePreset>();

		public List<ConstantShakePreset> ConstantShakePresets = new List<ConstantShakePreset>();

		public ConstantShakePreset StartConstantShakePreset;

		public ConstantShakePreset CurrentConstantShakePreset;

		private Transform _shakeParent;

		private List<Coroutine> _applyInfluencesCoroutines = new List<Coroutine>();

		private List<Coroutine> _shakeTimedCoroutines = new List<Coroutine>();

		private Coroutine _shakeCoroutine;

		private Vector3 _shakeVelocity;

		private List<Vector3> _shakePositions = new List<Vector3>();

		private Quaternion _rotationTarget;

		private Quaternion _originalRotation;

		private float _rotationTime;

		private float _rotationVelocity;

		private List<Vector3> _influences = new List<Vector3>();

		private Vector3 _influencesSum = Vector3.zero;

		private Vector3[] _constantShakePositions;

		private Vector3 _constantShakePosition;

		private bool _isConstantShaking;

		public static ProCamera2DShake Instance
		{
			get
			{
				if (object.Equals(_instance, null))
				{
					_instance = UnityEngine.Object.FindObjectOfType(typeof(ProCamera2DShake)) as ProCamera2DShake;
					if (object.Equals(_instance, null))
					{
						throw new UnityException("ProCamera2D does not have a Shake extension.");
					}
				}
				return _instance;
			}
		}

		public static bool Exists => _instance != null;

		protected override void Awake()
		{
			base.Awake();
			_instance = this;
			if (base.ProCamera2D.transform.parent != null)
			{
				_shakeParent = new GameObject("ProCamera2DShakeContainer").transform;
				_shakeParent.parent = base.ProCamera2D.transform.parent;
				_shakeParent.localPosition = Vector3.zero;
				base.ProCamera2D.transform.parent = _shakeParent;
			}
			else
			{
				Transform shakeParent = (base.ProCamera2D.transform.parent = new GameObject("ProCamera2DShakeContainer").transform);
				_shakeParent = shakeParent;
			}
			_originalRotation = _transform.localRotation;
		}

		private void Start()
		{
			if (StartConstantShakePreset != null)
			{
				ConstantShake(StartConstantShakePreset);
			}
		}

		private void Update()
		{
			_influencesSum = Vector3.zero;
			if (_influences.Count > 0)
			{
				_influencesSum = Utils.GetVectorsSum(_influences);
				_influences.Clear();
				_shakeParent.localPosition = _influencesSum;
			}
		}

		public void Shake(float duration, Vector2 strength, int vibrato = 10, float randomness = 0.1f, float initialAngle = -1f, Vector3 rotation = default(Vector3), float smoothness = 0.1f, bool ignoreTimeScale = false)
		{
			if (!base.enabled)
			{
				return;
			}
			vibrato++;
			if (vibrato < 2)
			{
				vibrato = 2;
			}
			float[] array = new float[vibrato];
			float num = 0f;
			for (int i = 0; i < vibrato; i++)
			{
				float num2 = (float)(i + 1) / (float)vibrato;
				float num3 = duration * num2;
				num += num3;
				array[i] = num3;
			}
			float num4 = duration / num;
			for (int j = 0; j < vibrato; j++)
			{
				array[j] *= num4;
			}
			float num5 = strength.magnitude;
			float num6 = num5 / (float)vibrato;
			float num7 = ((initialAngle != -1f) ? initialAngle : UnityEngine.Random.Range(0f, 360f));
			Vector2[] array2 = new Vector2[vibrato];
			array2[vibrato - 1] = Vector2.zero;
			Quaternion[] array3 = new Quaternion[vibrato];
			array3[vibrato - 1] = _originalRotation;
			Quaternion a = Quaternion.Euler(rotation);
			for (int k = 0; k < vibrato - 1; k++)
			{
				if (k > 0)
				{
					num7 = num7 - 180f + (float)UnityEngine.Random.Range(-90, 90) * randomness;
				}
				Quaternion quaternion = Quaternion.AngleAxis((float)UnityEngine.Random.Range(-90, 90) * randomness, Vector3.up);
				float f = num7 * ((float)Math.PI / 180f);
				Vector3 vector = new Vector3(num5 * Mathf.Cos(f), num5 * Mathf.Sin(f), 0f);
				Vector2 vector2 = quaternion * vector;
				vector2.x = Vector2.ClampMagnitude(vector2, strength.x).x;
				vector2.y = Vector2.ClampMagnitude(vector2, strength.y).y;
				array2[k] = vector2;
				num5 -= num6;
				strength = Vector2.ClampMagnitude(strength, num5);
				int num8 = ((k % 2 == 0) ? 1 : (-1));
				float t = (float)k / (float)(vibrato - 1);
				array3[k] = ((num8 == 1) ? (Quaternion.Lerp(a, Quaternion.identity, t) * _originalRotation) : (Quaternion.Inverse(Quaternion.Lerp(a, Quaternion.identity, t)) * _originalRotation));
			}
			_applyInfluencesCoroutines.Add(ApplyShakesTimed(array2, array3, array, smoothness, ignoreTimeScale));
		}

		public void Shake(int presetIndex)
		{
			if (presetIndex <= ShakePresets.Count - 1)
			{
				Shake(ShakePresets[presetIndex].Duration, ShakePresets[presetIndex].Strength, ShakePresets[presetIndex].Vibrato, ShakePresets[presetIndex].Randomness, ShakePresets[presetIndex].UseRandomInitialAngle ? (-1f) : ShakePresets[presetIndex].InitialAngle, ShakePresets[presetIndex].Rotation, ShakePresets[presetIndex].Smoothness, ShakePresets[presetIndex].IgnoreTimeScale);
			}
			else
			{
				Debug.LogWarning("Could not find a shake preset with the index: " + presetIndex);
			}
		}

		public void Shake(string presetName)
		{
			for (int i = 0; i < ShakePresets.Count; i++)
			{
				if (ShakePresets[i].name == presetName)
				{
					Shake(i);
					return;
				}
			}
			Debug.LogWarning("Could not find a shake preset with the name: " + presetName);
		}

		public void Shake(ShakePreset preset)
		{
			Shake(preset.Duration, preset.Strength, preset.Vibrato, preset.Randomness, preset.UseRandomInitialAngle ? (-1f) : preset.InitialAngle, preset.Rotation, preset.Smoothness, preset.IgnoreTimeScale);
		}

		public void StopShaking()
		{
			for (int i = 0; i < _applyInfluencesCoroutines.Count; i++)
			{
				StopCoroutine(_applyInfluencesCoroutines[i]);
			}
			for (int j = 0; j < _shakeTimedCoroutines.Count; j++)
			{
				StopCoroutine(_shakeTimedCoroutines[j]);
			}
			if (_shakeCoroutine != null)
			{
				StopCoroutine(_shakeCoroutine);
				_shakeCoroutine = null;
			}
			_shakePositions.Clear();
			_shakeVelocity = Vector3.zero;
			ShakeCompleted();
		}

		public void ConstantShake(ConstantShakePreset preset)
		{
			if (CurrentConstantShakePreset != null)
			{
				StopConstantShaking(0f);
			}
			CurrentConstantShakePreset = preset;
			_isConstantShaking = true;
			_constantShakePositions = new Vector3[preset.Layers.Count];
			for (int i = 0; i < preset.Layers.Count; i++)
			{
				StartCoroutine(CalculateConstantShakePosition(i, preset.Layers[i].Frequency.x, preset.Layers[i].Frequency.y, preset.Layers[i].AmplitudeHorizontal, preset.Layers[i].AmplitudeVertical, preset.Layers[i].AmplitudeDepth));
			}
			StartCoroutine(ConstantShakeRoutine(preset.Intensity));
		}

		public void ConstantShake(string presetName)
		{
			for (int i = 0; i < ConstantShakePresets.Count; i++)
			{
				if (ConstantShakePresets[i].name == presetName)
				{
					ConstantShake(ConstantShakePresets[i]);
					return;
				}
			}
			Debug.LogWarning("Could not find a ConstantShakePreset with the name: " + presetName + ". Remember you need to add it to the ConstantShakePresets list first.");
		}

		public void ConstantShake(int presetIndex)
		{
			if (presetIndex <= ConstantShakePresets.Count - 1)
			{
				ConstantShake(ConstantShakePresets[presetIndex]);
			}
			else
			{
				Debug.LogWarning("Could not find a ConstantShakePreset with the index: " + presetIndex + ". Remember you need to add it to the ConstantShakePresets list first.");
			}
		}

		public void StopConstantShaking(float duration = 0.3f)
		{
			CurrentConstantShakePreset = null;
			_isConstantShaking = false;
			if (duration > 0f)
			{
				StartCoroutine(StopConstantShakeRoutine(duration));
				return;
			}
			StopAllCoroutines();
			_constantShakePosition = Vector3.zero;
			_influences.Clear();
			_influences.Add(_constantShakePosition);
		}

		public Coroutine ApplyShakesTimed(Vector2[] shakes, Vector3[] rotations, float[] durations, float smoothness = 0.1f, bool ignoreTimeScale = false)
		{
			if (!base.enabled)
			{
				return null;
			}
			Quaternion[] array = new Quaternion[rotations.Length];
			for (int i = 0; i < rotations.Length; i++)
			{
				array[i] = Quaternion.Euler(rotations[i]) * _originalRotation;
			}
			return ApplyShakesTimed(shakes, array, durations);
		}

		public void ApplyInfluenceIgnoringBoundaries(Vector2 influence)
		{
			if (!(Time.deltaTime < 0.0001f) && !float.IsNaN(influence.x) && !float.IsNaN(influence.y))
			{
				_influences.Add(VectorHV(influence.x, influence.y));
			}
		}

		private Coroutine ApplyShakesTimed(Vector2[] shakes, Quaternion[] rotations, float[] durations, float smoothness = 0.1f, bool ignoreTimeScale = false)
		{
			Coroutine result = StartCoroutine(ApplyShakesTimedRoutine(shakes, rotations, durations, ignoreTimeScale));
			if (_shakeCoroutine == null)
			{
				_shakeCoroutine = StartCoroutine(ShakeRoutine(smoothness, ignoreTimeScale));
			}
			return result;
		}

		private IEnumerator ShakeRoutine(float smoothness, bool ignoreTimeScale = false)
		{
			while (_shakePositions.Count > 0 || Vector3.Distance(_shakeParent.localPosition, _influencesSum) > 0.01f || Quaternion.Angle(_transform.localRotation, _originalRotation) > 0.01f)
			{
				Vector3 target = Utils.GetVectorsSum(_shakePositions) + _influencesSum;
				Vector3 localPosition = Vector3.zero;
				if (ignoreTimeScale)
				{
					localPosition = Vector3.SmoothDamp(_shakeParent.localPosition, target, ref _shakeVelocity, smoothness, float.MaxValue, Time.unscaledDeltaTime);
				}
				else if (base.ProCamera2D.DeltaTime > 0f)
				{
					localPosition = Vector3.SmoothDamp(_shakeParent.localPosition, target, ref _shakeVelocity, smoothness);
				}
				_shakeParent.localPosition = localPosition;
				_shakePositions.Clear();
				if (ignoreTimeScale)
				{
					_rotationTime = Mathf.SmoothDamp(_rotationTime, 1f, ref _rotationVelocity, smoothness, float.MaxValue, Time.unscaledDeltaTime);
				}
				else if (base.ProCamera2D.DeltaTime > 0f)
				{
					_rotationTime = Mathf.SmoothDamp(_rotationTime, 1f, ref _rotationVelocity, smoothness, float.MaxValue, base.ProCamera2D.DeltaTime);
				}
				Quaternion localRotation = Quaternion.Slerp(_transform.localRotation, _rotationTarget, _rotationTime);
				_transform.localRotation = localRotation;
				_rotationTarget = _originalRotation;
				yield return base.ProCamera2D.GetYield();
			}
			ShakeCompleted();
		}

		private void ShakeCompleted()
		{
			_shakeParent.localPosition = _influencesSum;
			_transform.localRotation = _originalRotation;
			_shakeCoroutine = null;
			if (OnShakeCompleted != null)
			{
				OnShakeCompleted();
			}
		}

		private IEnumerator ApplyShakesTimedRoutine(IList<Vector2> shakes, IList<Quaternion> rotations, float[] durations, bool ignoreTimeScale = false)
		{
			_shakeTimedCoroutines = new List<Coroutine>();
			int count = -1;
			while (count < durations.Length - 1)
			{
				count++;
				float duration = durations[count];
				Coroutine coroutine = StartCoroutine(ApplyShakeTimedRoutine(shakes[count], rotations[count], duration, ignoreTimeScale));
				_shakeTimedCoroutines.Add(coroutine);
				yield return coroutine;
			}
		}

		private IEnumerator ApplyShakeTimedRoutine(Vector2 shake, Quaternion rotation, float duration, bool ignoreTimeScale = false)
		{
			_rotationTime = 0f;
			_rotationVelocity = 0f;
			while (duration > 0f)
			{
				duration = ((!ignoreTimeScale) ? (duration - base.ProCamera2D.DeltaTime) : (duration - Time.unscaledDeltaTime));
				_shakePositions.Add(VectorHV(shake.x, shake.y));
				_rotationTarget = rotation;
				yield return base.ProCamera2D.GetYield();
			}
		}

		private IEnumerator StopConstantShakeRoutine(float duration)
		{
			Vector3 velocity = Vector3.zero;
			_influences.Clear();
			while (duration >= 0f)
			{
				duration -= base.ProCamera2D.DeltaTime;
				_constantShakePosition = Vector3.SmoothDamp(_constantShakePosition, Vector3.zero, ref velocity, duration, float.MaxValue);
				_influences.Add(_constantShakePosition);
				yield return base.ProCamera2D.GetYield();
			}
		}

		private IEnumerator CalculateConstantShakePosition(int index, float frequencyMin, float frequencyMax, float amplitudeX, float amplitudeY, float amplitudeZ)
		{
			while (_isConstantShaking)
			{
				float num = UnityEngine.Random.Range(frequencyMin, frequencyMax);
				Vector3 insideUnitSphere = UnityEngine.Random.insideUnitSphere;
				float arg = insideUnitSphere.x * amplitudeX;
				float arg2 = insideUnitSphere.y * amplitudeY;
				float arg3 = insideUnitSphere.z * amplitudeZ;
				if (index < _constantShakePositions.Length)
				{
					_constantShakePositions[index] = VectorHVD(arg, arg2, arg3);
				}
				if (base.ProCamera2D.IgnoreTimeScale)
				{
					yield return new WaitForSecondsRealtime(num);
				}
				else
				{
					yield return new WaitForSeconds(num);
				}
			}
		}

		private IEnumerator ConstantShakeRoutine(float intensity)
		{
			while (_isConstantShaking)
			{
				if (base.ProCamera2D.DeltaTime > 0f)
				{
					Vector3 vector = Utils.GetVectorsSum(_constantShakePositions) / _constantShakePositions.Length;
					_constantShakePosition.Set(Utils.SmoothApproach(_constantShakePosition.x, vector.x, vector.x, intensity, base.ProCamera2D.DeltaTime), Utils.SmoothApproach(_constantShakePosition.y, vector.y, vector.y, intensity, base.ProCamera2D.DeltaTime), Utils.SmoothApproach(_constantShakePosition.z, vector.z, vector.z, intensity, base.ProCamera2D.DeltaTime));
					_influences.Add(_constantShakePosition);
				}
				yield return base.ProCamera2D.GetYield();
			}
		}
	}
}
