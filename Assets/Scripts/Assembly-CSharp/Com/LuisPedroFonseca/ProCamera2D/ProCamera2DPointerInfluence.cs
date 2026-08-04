using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-pointer-influence/")]
	public class ProCamera2DPointerInfluence : BasePC2D, IPreMover
	{
		public static string ExtensionName = "Pointer Influence";

		public float MaxHorizontalInfluence = 3f;

		public float MaxVerticalInfluence = 2f;

		public float InfluenceSmoothness = 0.2f;

		private Vector2 _influence;

		private Vector2 _velocity;

		private int _prmOrder = 3000;

		public int PrMOrder
		{
			get
			{
				return _prmOrder;
			}
			set
			{
				_prmOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			base.ProCamera2D.AddPreMover(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePreMover(this);
			}
		}

		public override void OnReset()
		{
			_influence = Vector2.zero;
			_velocity = Vector2.zero;
		}

		public void PreMove(float deltaTime)
		{
			if (base.enabled)
			{
				ApplyInfluence(deltaTime);
			}
		}

		private void ApplyInfluence(float deltaTime)
		{
			Vector3 vector = base.ProCamera2D.GameCamera.ScreenToViewportPoint(Input.mousePosition);
			float num = vector.x.Remap(0f, 1f, -1f, 1f);
			float num2 = vector.y.Remap(0f, 1f, -1f, 1f);
			float x = num * MaxHorizontalInfluence;
			float y = num2 * MaxVerticalInfluence;
			_influence = Vector2.SmoothDamp(_influence, new Vector2(x, y), ref _velocity, InfluenceSmoothness, float.PositiveInfinity, deltaTime);
			base.ProCamera2D.ApplyInfluence(_influence);
		}
	}
}
