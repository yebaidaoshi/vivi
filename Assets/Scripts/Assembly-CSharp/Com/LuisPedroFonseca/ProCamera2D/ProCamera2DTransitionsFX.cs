using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-transitions-fx/")]
	public class ProCamera2DTransitionsFX : BasePC2D
	{
		public static string ExtensionName = "TransitionsFX";

		public Action OnTransitionEnterStarted;

		public Action OnTransitionEnterEnded;

		public Action OnTransitionExitStarted;

		public Action OnTransitionExitEnded;

		public Action OnTransitionStarted;

		public Action OnTransitionEnded;

		private static ProCamera2DTransitionsFX _instance;

		public TransitionsFXShaders TransitionShaderEnter;

		public float DurationEnter = 0.5f;

		public float DelayEnter;

		public EaseType EaseTypeEnter = EaseType.EaseOut;

		public Color BackgroundColorEnter = Color.black;

		public TransitionFXSide SideEnter;

		public TransitionFXDirection DirectionEnter;

		[Range(2f, 128f)]
		public int BlindsEnter = 16;

		public Texture TextureEnter;

		[Range(0f, 1f)]
		public float TextureSmoothingEnter = 0.2f;

		public TransitionsFXShaders TransitionShaderExit;

		public float DurationExit = 0.5f;

		public float DelayExit;

		public EaseType EaseTypeExit = EaseType.EaseOut;

		public Color BackgroundColorExit = Color.black;

		public TransitionFXSide SideExit;

		public TransitionFXDirection DirectionExit;

		[Range(2f, 128f)]
		public int BlindsExit = 16;

		public Texture TextureExit;

		[Range(0f, 1f)]
		public float TextureSmoothingExit = 0.2f;

		public bool StartSceneOnEnterState = true;

		private Coroutine _transitionCoroutine;

		private float _step;

		private Material _transitionEnterMaterial;

		private Material _transitionExitMaterial;

		private BasicBlit _blit;

		private int _material_StepID;

		private int _material_BackgroundColorID;

		private string _previousEnterShader = "";

		private string _previousExitShader = "";

		public static ProCamera2DTransitionsFX Instance
		{
			get
			{
				if (object.Equals(_instance, null))
				{
					_instance = ProCamera2D.Instance.GetComponent<ProCamera2DTransitionsFX>();
					if (object.Equals(_instance, null))
					{
						throw new UnityException("ProCamera2D does not have a TransitionFX extension.");
					}
				}
				return _instance;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			_instance = this;
			_material_StepID = Shader.PropertyToID("_Step");
			_material_BackgroundColorID = Shader.PropertyToID("_BackgroundColor");
			_blit = base.gameObject.AddComponent<BasicBlit>();
			_blit.enabled = false;
			UpdateTransitionsShaders();
			UpdateTransitionsProperties();
			UpdateTransitionsColor();
			if (StartSceneOnEnterState)
			{
				_step = 1f;
				_blit.CurrentMaterial = _transitionEnterMaterial;
				_blit.CurrentMaterial.SetFloat(_material_StepID, _step);
				_blit.enabled = true;
			}
		}

		public void TransitionEnter()
		{
			Transition(_transitionEnterMaterial, DurationEnter, DelayEnter, 1f, 0f, EaseTypeEnter);
		}

		public void TransitionExit()
		{
			Transition(_transitionExitMaterial, DurationExit, DelayExit, 0f, 1f, EaseTypeExit);
		}

		public void UpdateTransitionsShaders()
		{
			string text = TransitionShaderEnter.ToString();
			if (!_previousEnterShader.Equals(text))
			{
				_transitionEnterMaterial = new Material(Shader.Find("Hidden/ProCamera2D/TransitionsFX/" + text));
				_previousEnterShader = text;
			}
			string text2 = TransitionShaderExit.ToString();
			if (!_previousExitShader.Equals(text2))
			{
				_transitionExitMaterial = new Material(Shader.Find("Hidden/ProCamera2D/TransitionsFX/" + text2));
				_previousExitShader = text2;
			}
		}

		public void UpdateTransitionsProperties()
		{
			if (TransitionShaderEnter == TransitionsFXShaders.Wipe || TransitionShaderEnter == TransitionsFXShaders.Blinds)
			{
				_transitionEnterMaterial.SetInt("_Direction", (int)SideEnter);
				_transitionEnterMaterial.SetInt("_Blinds", BlindsEnter);
			}
			else if (TransitionShaderEnter == TransitionsFXShaders.Shutters)
			{
				_transitionEnterMaterial.SetInt("_Direction", (int)DirectionEnter);
			}
			else if (TransitionShaderEnter == TransitionsFXShaders.Texture)
			{
				_transitionEnterMaterial.SetTexture("_TransitionTex", TextureEnter);
				_transitionEnterMaterial.SetFloat("_Smoothing", TextureSmoothingEnter);
			}
			if (TransitionShaderExit == TransitionsFXShaders.Wipe || TransitionShaderExit == TransitionsFXShaders.Blinds)
			{
				_transitionExitMaterial.SetInt("_Direction", (int)SideExit);
				_transitionExitMaterial.SetInt("_Blinds", BlindsExit);
			}
			else if (TransitionShaderExit == TransitionsFXShaders.Shutters)
			{
				_transitionExitMaterial.SetInt("_Direction", (int)DirectionExit);
			}
			else if (TransitionShaderExit == TransitionsFXShaders.Texture)
			{
				_transitionExitMaterial.SetTexture("_TransitionTex", TextureExit);
				_transitionExitMaterial.SetFloat("_Smoothing", TextureSmoothingExit);
			}
		}

		public void UpdateTransitionsColor()
		{
			_transitionEnterMaterial.SetColor(_material_BackgroundColorID, BackgroundColorEnter);
			_transitionExitMaterial.SetColor(_material_BackgroundColorID, BackgroundColorExit);
		}

		public void Clear()
		{
			_blit.enabled = false;
		}

		private void Transition(Material material, float duration, float delay, float startValue, float endValue, EaseType easeType)
		{
			if (_transitionEnterMaterial == null)
			{
				Debug.LogWarning("TransitionsFX not initialized yet. You're probably calling TransitionEnter/Exit from an Awake method. Please call it from a Start method instead.");
				return;
			}
			if (_transitionCoroutine != null)
			{
				StopCoroutine(_transitionCoroutine);
			}
			_transitionCoroutine = StartCoroutine(TransitionRoutine(material, duration, delay, startValue, endValue, easeType));
		}

		private IEnumerator TransitionRoutine(Material material, float duration, float delay, float startValue, float endValue, EaseType easeType)
		{
			_blit.enabled = true;
			_step = startValue;
			_blit.CurrentMaterial = material;
			_blit.CurrentMaterial.SetFloat(_material_StepID, _step);
			if (endValue == 0f)
			{
				if (OnTransitionEnterStarted != null)
				{
					OnTransitionEnterStarted();
				}
			}
			else if (endValue == 1f && OnTransitionExitStarted != null)
			{
				OnTransitionExitStarted();
			}
			if (OnTransitionStarted != null)
			{
				OnTransitionStarted();
			}
			if (delay > 0f)
			{
				if (base.ProCamera2D.IgnoreTimeScale)
				{
					yield return new WaitForSecondsRealtime(delay);
				}
				else
				{
					yield return new WaitForSeconds(delay);
				}
			}
			float t = 0f;
			while (t <= 1f)
			{
				t += base.ProCamera2D.DeltaTime / duration;
				_step = Utils.EaseFromTo(startValue, endValue, t, easeType);
				material.SetFloat(_material_StepID, _step);
				yield return null;
			}
			_step = endValue;
			material.SetFloat(_material_StepID, _step);
			if (endValue == 0f)
			{
				if (OnTransitionEnterEnded != null)
				{
					OnTransitionEnterEnded();
				}
			}
			else if (endValue == 1f && OnTransitionExitEnded != null)
			{
				OnTransitionExitEnded();
			}
			if (OnTransitionEnded != null)
			{
				OnTransitionEnded();
			}
			if (endValue == 0f)
			{
				_blit.enabled = false;
			}
		}
	}
}
