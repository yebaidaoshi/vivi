using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-parallax/")]
	[ExecuteInEditMode]
	public class ProCamera2DParallax : BasePC2D, IPostMover
	{
		public static string ExtensionName = "Parallax";

		public List<ProCamera2DParallaxLayer> ParallaxLayers = new List<ProCamera2DParallaxLayer>();

		public bool ParallaxHorizontal = true;

		public bool ParallaxVertical = true;

		public bool ParallaxZoom = true;

		public Vector3 RootPosition = Vector3.zero;

		public int FrontDepthStart = 1;

		public int BackDepthStart = -1;

		public bool UseIndependentAxisSpeeds;

		public bool AutomaticallyConfigureCameraClearFlags = true;

		private float _initialOrtographicSize;

		private float[] _initialSpeeds;

		private Coroutine _animateCoroutine;

		private int _pmOrder = 1000;

		public int PMOrder
		{
			get
			{
				return _pmOrder;
			}
			set
			{
				_pmOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (base.ProCamera2D == null)
			{
				return;
			}
			if (Application.isPlaying)
			{
				CalculateParallaxObjectsOffset();
			}
			foreach (ProCamera2DParallaxLayer parallaxLayer in ParallaxLayers)
			{
				if (parallaxLayer.ParallaxCamera != null)
				{
					parallaxLayer.CameraTransform = parallaxLayer.ParallaxCamera.transform;
				}
			}
			_initialSpeeds = new float[ParallaxLayers.Count];
			for (int i = 0; i < _initialSpeeds.Length; i++)
			{
				_initialSpeeds[i] = ParallaxLayers[i].Speed;
			}
			if (base.ProCamera2D.GameCamera != null)
			{
				_initialOrtographicSize = base.ProCamera2D.GameCamera.orthographicSize;
				if (!base.ProCamera2D.GameCamera.orthographic)
				{
					base.enabled = false;
				}
			}
			base.ProCamera2D.AddPostMover(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePostMover(this);
			}
		}

		public void PostMove(float deltaTime)
		{
			if (base.enabled)
			{
				Move();
			}
		}

		public void CalculateParallaxObjectsOffset()
		{
			ProCamera2DParallaxObject[] array = Object.FindObjectsOfType<ProCamera2DParallaxObject>();
			Dictionary<int, ProCamera2DParallaxLayer> dictionary = new Dictionary<int, ProCamera2DParallaxLayer>();
			for (int i = 0; i <= 31; i++)
			{
				foreach (ProCamera2DParallaxLayer parallaxLayer in ParallaxLayers)
				{
					if ((int)parallaxLayer.LayerMask == ((int)parallaxLayer.LayerMask | (1 << i)))
					{
						dictionary[i] = parallaxLayer;
					}
				}
			}
			for (int j = 0; j < array.Length; j++)
			{
				Vector3 arg = array[j].transform.position - RootPosition;
				float arg2 = Vector3H(arg) * (UseIndependentAxisSpeeds ? dictionary[array[j].gameObject.layer].SpeedX : dictionary[array[j].gameObject.layer].Speed);
				float arg3 = Vector3V(arg) * (UseIndependentAxisSpeeds ? dictionary[array[j].gameObject.layer].SpeedY : dictionary[array[j].gameObject.layer].Speed);
				array[j].transform.position = VectorHVD(arg2, arg3, Vector3D(arg)) + RootPosition;
			}
		}

		private void Move()
		{
			Vector3 arg = _transform.position - RootPosition;
			for (int i = 0; i < ParallaxLayers.Count; i++)
			{
				if (ParallaxLayers[i].CameraTransform != null)
				{
					ParallaxLayers[i].ParallaxCamera.rect = base.ProCamera2D.GameCamera.rect;
					float arg2 = (ParallaxHorizontal ? (Vector3H(arg) * (UseIndependentAxisSpeeds ? ParallaxLayers[i].SpeedX : ParallaxLayers[i].Speed)) : Vector3H(arg));
					float arg3 = (ParallaxVertical ? (Vector3V(arg) * (UseIndependentAxisSpeeds ? ParallaxLayers[i].SpeedY : ParallaxLayers[i].Speed)) : Vector3V(arg));
					ParallaxLayers[i].CameraTransform.position = RootPosition + VectorHVD(arg2, arg3, Vector3D(_transform.position));
					if (ParallaxZoom)
					{
						ParallaxLayers[i].ParallaxCamera.orthographicSize = _initialOrtographicSize + (base.ProCamera2D.GameCamera.orthographicSize - _initialOrtographicSize) * ParallaxLayers[i].Speed;
					}
				}
			}
		}

		public void ToggleParallax(bool value, float duration = 2f, EaseType easeType = EaseType.EaseInOut)
		{
			if (_initialSpeeds != null)
			{
				if (_animateCoroutine != null)
				{
					StopCoroutine(_animateCoroutine);
				}
				_animateCoroutine = StartCoroutine(Animate(value, duration, easeType));
			}
		}

		private IEnumerator Animate(bool value, float duration, EaseType easeType)
		{
			float[] currentSpeeds = new float[ParallaxLayers.Count];
			for (int i = 0; i < currentSpeeds.Length; i++)
			{
				currentSpeeds[i] = ParallaxLayers[i].Speed;
			}
			float t = 0f;
			while (t <= 1f)
			{
				t += base.ProCamera2D.DeltaTime / duration;
				for (int j = 0; j < ParallaxLayers.Count; j++)
				{
					if (value)
					{
						ParallaxLayers[j].Speed = Utils.EaseFromTo(currentSpeeds[j], _initialSpeeds[j], t, easeType);
					}
					else
					{
						ParallaxLayers[j].Speed = Utils.EaseFromTo(currentSpeeds[j], 1f, t, easeType);
					}
				}
				yield return base.ProCamera2D.GetYield();
			}
		}
	}
}
