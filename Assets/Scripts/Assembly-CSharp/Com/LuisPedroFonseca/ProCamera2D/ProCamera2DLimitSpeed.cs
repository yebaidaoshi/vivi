using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-limit-speed/")]
	public class ProCamera2DLimitSpeed : BasePC2D, IPositionDeltaChanger
	{
		public static string ExtensionName = "Limit Speed";

		public bool LimitHorizontalSpeed = true;

		public float MaxHorizontalSpeed = 2f;

		public bool LimitVerticalSpeed = true;

		public float MaxVerticalSpeed = 2f;

		private int _pdcOrder = 1000;

		public int PDCOrder
		{
			get
			{
				return _pdcOrder;
			}
			set
			{
				_pdcOrder = value;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			base.ProCamera2D.AddPositionDeltaChanger(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if ((bool)base.ProCamera2D)
			{
				base.ProCamera2D.RemovePositionDeltaChanger(this);
			}
		}

		public Vector3 AdjustDelta(float deltaTime, Vector3 originalDelta)
		{
			if (!base.enabled)
			{
				return originalDelta;
			}
			float num = 1f / deltaTime;
			float num2 = Vector3H(originalDelta) * num;
			float num3 = Vector3V(originalDelta) * num;
			if (LimitHorizontalSpeed)
			{
				num2 = Mathf.Clamp(num2, 0f - MaxHorizontalSpeed, MaxHorizontalSpeed);
			}
			if (LimitVerticalSpeed)
			{
				num3 = Mathf.Clamp(num3, 0f - MaxVerticalSpeed, MaxVerticalSpeed);
			}
			return VectorHV(num2 * deltaTime, num3 * deltaTime);
		}
	}
}
