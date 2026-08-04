using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[ExecuteInEditMode]
	public class ProCamera2DLetterbox : MonoBehaviour
	{
		[Range(0f, 0.5f)]
		public float Amount;

		public Color Color;

		private Material _material;

		private int TopPropertyID;

		private int BottomPropertyID;

		private int ColorPropertyID;

		private float _previousAmount = float.MaxValue;

		private Material material
		{
			get
			{
				if (_material != null)
				{
					return _material;
				}
				_material = new Material(Shader.Find("Hidden/ProCamera2D/Letterbox"))
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				return _material;
			}
		}

		private void OnEnable()
		{
			_previousAmount = float.MaxValue;
			if (TopPropertyID == 0)
			{
				TopPropertyID = Shader.PropertyToID("_Top");
			}
			if (BottomPropertyID == 0)
			{
				BottomPropertyID = Shader.PropertyToID("_Bottom");
			}
			if (ColorPropertyID == 0)
			{
				ColorPropertyID = Shader.PropertyToID("_Color");
			}
		}

		private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
		{
			if (Mathf.Approximately(Amount, 0f) || material == null)
			{
				Graphics.Blit(sourceTexture, destTexture);
				return;
			}
			if (Math.Abs(Amount - _previousAmount) > 0.0001f)
			{
				Amount = Mathf.Clamp01(Amount);
				material.SetFloat(TopPropertyID, 1f - Amount);
				material.SetFloat(BottomPropertyID, Amount);
				material.SetColor(ColorPropertyID, Color);
			}
			Graphics.Blit(sourceTexture, destTexture, material);
			_previousAmount = Amount;
		}

		private void OnDisable()
		{
			if ((bool)_material)
			{
				UnityEngine.Object.DestroyImmediate(_material);
			}
		}

		public void TweenTo(float targetAmount, float duration)
		{
			StopAllCoroutines();
			StartCoroutine(TweenToRoutine(targetAmount, duration));
		}

		private IEnumerator TweenToRoutine(float targetAmount, float duration)
		{
			float initialAmount = Amount;
			float t = 0f;
			while (t <= 1f)
			{
				t += (ProCamera2D.Instance.IgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime) / duration;
				Amount = Utils.EaseFromTo(initialAmount, targetAmount, t, EaseType.EaseOut);
				yield return null;
			}
			Amount = targetAmount;
			yield return null;
		}
	}
}
