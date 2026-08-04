using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/extension-pixel-perfect/")]
	[ExecuteInEditMode]
	public class ProCamera2DPixelPerfectSprite : BasePC2D, IPostMover
	{
		public bool IsAMovingObject;

		public bool IsAChildSprite;

		public Vector2 LocalPosition;

		[Range(-8f, 32f)]
		public int SpriteScale;

		private Sprite _sprite;

		private ProCamera2DPixelPerfect _pixelPerfectPlugin;

		[SerializeField]
		private Vector3 _initialScale = Vector3.one;

		private int _prevSpriteScale;

		private int _pmOrder = 2000;

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
				base.enabled = false;
				return;
			}
			GetPixelPerfectPlugin();
			GetSprite();
			base.ProCamera2D.AddPostMover(this);
		}

		private void Start()
		{
			SetAsPixelPerfect();
		}

		public void PostMove(float deltaTime)
		{
			if (base.enabled)
			{
				Step();
			}
		}

		private void Step()
		{
			if (!(_pixelPerfectPlugin == null) && _pixelPerfectPlugin.enabled)
			{
				if (IsAMovingObject)
				{
					SetAsPixelPerfect();
				}
				_prevSpriteScale = SpriteScale;
			}
		}

		private void GetPixelPerfectPlugin()
		{
			_pixelPerfectPlugin = base.ProCamera2D.GetComponent<ProCamera2DPixelPerfect>();
		}

		private void GetSprite()
		{
			SpriteRenderer component = GetComponent<SpriteRenderer>();
			if (component != null)
			{
				_sprite = component.sprite;
			}
		}

		public void SetAsPixelPerfect()
		{
			if (IsAChildSprite)
			{
				_transform.localPosition = VectorHVD(Utils.AlignToGrid(LocalPosition.x, _pixelPerfectPlugin.PixelStep), Utils.AlignToGrid(LocalPosition.y, _pixelPerfectPlugin.PixelStep), Vector3D(_transform.localPosition));
			}
			_transform.position = VectorHVD(Utils.AlignToGrid(Vector3H(_transform.position), _pixelPerfectPlugin.PixelStep), Utils.AlignToGrid(Vector3V(_transform.position), _pixelPerfectPlugin.PixelStep), Vector3D(_transform.position));
			if (SpriteScale == 0)
			{
				if (_prevSpriteScale == 0)
				{
					_initialScale = _transform.localScale;
				}
				else
				{
					_transform.localScale = _initialScale;
				}
			}
			else
			{
				float num = ((SpriteScale < 0) ? (1f / (float)SpriteScale * -1f) : ((float)SpriteScale));
				float num2 = 1f;
				num2 = _sprite.pixelsPerUnit * num * (1f / _pixelPerfectPlugin.PixelsPerUnit);
				_transform.localScale = new Vector3(Mathf.Sign(_transform.localScale.x) * num2, Mathf.Sign(_transform.localScale.y) * num2, _transform.localScale.z);
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (base.ProCamera2D != null)
			{
				base.ProCamera2D.RemovePostMover(this);
			}
		}
	}
}
