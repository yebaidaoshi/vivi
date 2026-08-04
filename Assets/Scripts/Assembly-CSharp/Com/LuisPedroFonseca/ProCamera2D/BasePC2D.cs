using System;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public abstract class BasePC2D : MonoBehaviour
	{
		[SerializeField]
		private ProCamera2D _pc2D;

		protected Func<Vector3, float> Vector3H;

		protected Func<Vector3, float> Vector3V;

		protected Func<Vector3, float> Vector3D;

		protected Func<float, float, Vector3> VectorHV;

		protected Func<float, float, float, Vector3> VectorHVD;

		protected Transform _transform;

		private bool _enabled;

		public ProCamera2D ProCamera2D
		{
			get
			{
				if (_pc2D != null)
				{
					return _pc2D;
				}
				_pc2D = GetComponent<ProCamera2D>();
				if (_pc2D == null && Camera.main != null)
				{
					_pc2D = Camera.main.GetComponent<ProCamera2D>();
				}
				if (_pc2D == null)
				{
					_pc2D = UnityEngine.Object.FindObjectOfType(typeof(ProCamera2D)) as ProCamera2D;
				}
				return _pc2D;
			}
			set
			{
				_pc2D = value;
			}
		}

		protected virtual void Awake()
		{
			_transform = base.transform;
			if (base.enabled)
			{
				Enable();
			}
			ResetAxisFunctions();
		}

		protected virtual void OnEnable()
		{
			Enable();
		}

		protected virtual void OnDisable()
		{
			Disable();
		}

		protected virtual void OnDestroy()
		{
			Disable();
		}

		public virtual void OnReset()
		{
		}

		private void Enable()
		{
			if (!_enabled && !(_pc2D == null))
			{
				_enabled = true;
				ProCamera2D pc2D = _pc2D;
				pc2D.OnReset = (Action)Delegate.Combine(pc2D.OnReset, new Action(OnReset));
			}
		}

		private void Disable()
		{
			if (_pc2D != null && _enabled)
			{
				_enabled = false;
				ProCamera2D pc2D = _pc2D;
				pc2D.OnReset = (Action)Delegate.Remove(pc2D.OnReset, new Action(OnReset));
			}
		}

		private void ResetAxisFunctions()
		{
			if (Vector3H != null || ProCamera2D == null)
			{
				return;
			}
			switch (_pc2D.Axis)
			{
			case MovementAxis.XY:
				Vector3H = (Vector3 vector) => vector.x;
				Vector3V = (Vector3 vector) => vector.y;
				Vector3D = (Vector3 vector) => vector.z;
				VectorHV = (float h, float v) => new Vector3(h, v, 0f);
				VectorHVD = (float h, float v, float d) => new Vector3(h, v, d);
				break;
			case MovementAxis.XZ:
				Vector3H = (Vector3 vector) => vector.x;
				Vector3V = (Vector3 vector) => vector.z;
				Vector3D = (Vector3 vector) => vector.y;
				VectorHV = (float h, float v) => new Vector3(h, 0f, v);
				VectorHVD = (float h, float v, float d) => new Vector3(h, d, v);
				break;
			case MovementAxis.YZ:
				Vector3H = (Vector3 vector) => vector.z;
				Vector3V = (Vector3 vector) => vector.y;
				Vector3D = (Vector3 vector) => vector.x;
				VectorHV = (float h, float v) => new Vector3(0f, v, h);
				VectorHVD = (float h, float v, float d) => new Vector3(d, v, h);
				break;
			}
		}
	}
}
