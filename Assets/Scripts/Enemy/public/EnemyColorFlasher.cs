using Spine;
using Spine.Unity;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人整体染色/闪白的统一管理（替代之前会互相打断的协程）。
    /// - Flash：受击闪色，带优先级，后一次覆盖前一次
    /// - Pulse：眩晕时的呼吸闪烁，独立于受击
    /// 基色只在 Start 记录一次，所以不会出现"变了回不来"的问题。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyColorFlasher : MonoBehaviour
    {
        [SerializeField] private Color hurtColor = new Color(1f, 1f, 0.4f, 1f);
        [SerializeField] private Color critColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color stunColor = new Color(0.55f, 0.85f, 1f, 1f);
        [SerializeField] private float flashSpeed = 30f;

        private SkeletonMecanim _mecanim;
        private Skeleton _skeleton;
        private Color _base = Color.white;

        private Color _flashColor;
        private float _flashUntil;
        private bool _pulsing;

        private void Start()
        {
            Cache();
        }

        private void Cache()
        {
            if (_skeleton != null) return;
            _mecanim = GetComponent<SkeletonMecanim>();
            if (_mecanim == null) _mecanim = GetComponentInChildren<SkeletonMecanim>();
            if (_mecanim != null) _skeleton = _mecanim.skeleton;
            if (_skeleton != null)
                _base = new Color(_skeleton.R, _skeleton.G, _skeleton.B, _skeleton.A);
        }

        public void FlashHurt() => Flash(hurtColor, 0.12f);
        public void FlashCritical() => Flash(critColor, 0.2f);

        public void Flash(Color color, float duration)
        {
            _flashColor = color;
            _flashUntil = Time.time + duration;
        }

        public void SetStunPulse(bool on)
        {
            _pulsing = on;
        }

        private void LateUpdate()
        {
            Cache();
            if (_skeleton == null) return;

            Color target = _base;

            if (_pulsing)
            {
                float t = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                target = Color.Lerp(_base, stunColor, 0.35f + t * 0.45f);
            }
            else if (Time.time < _flashUntil)
            {
                target = _flashColor;
            }

            // 只改 RGB，alpha 完全交给 Spine/其它系统，避免死亡时整只渐隐
            _skeleton.R = target.r;
            _skeleton.G = target.g;
            _skeleton.B = target.b;
        }
    }
}
