using UnityEngine;
using UnityEngine.Rendering;
using Spine;
using Spine.Unity;

namespace Player
{
    public class PlayerEvade : MonoBehaviour
    {
        [Header("叠加剪影材质")]
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private int sortingOrderOffset = 1;

        [Header("取消窗口")]
        [SerializeField] private float cancelableAt = 47f / 60f;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;
        private PlayerHealth _health;

        private SkeletonRenderer _skeletonRenderer;
        private MeshFilter _mainMeshFilter;
        private MeshRenderer _mainMeshRenderer;

        private Transform _overlayLayer;
        private MeshFilter _overlayMeshFilter;
        private MeshRenderer _overlayMeshRenderer;

        private bool _active;
        private float _elapsed;
        private bool _invincibleApplied;
        private bool _overlayApplied;

        public bool IsActive => _active;
        public float Elapsed => _elapsed;
        public float CancelableAt => cancelableAt;
        public bool CanCancel => _active && _elapsed >= cancelableAt;

        public void Init(PlayerContext ctx, PlayerHealth health)
        {
            ctx.Bind(out _motor, out _anim, out _audio, out _settings);
            _health = health;
            _skeletonRenderer = GetComponent<SkeletonRenderer>();
            _mainMeshFilter = GetComponent<MeshFilter>();
            _mainMeshRenderer = GetComponent<MeshRenderer>();

            EnsureOverlayLayer();
        }

        private void EnsureOverlayLayer()
        {
            if (overlayMaterial == null) return;

            var existing = transform.Find("EvadeOverlay");
            if (existing != null)
            {
                _overlayLayer = existing;
            }
            else
            {
                var go = new GameObject("EvadeOverlay");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _overlayLayer = go.transform;
            }

            _overlayMeshFilter = _overlayLayer.GetComponent<MeshFilter>();
            if (_overlayMeshFilter == null)
                _overlayMeshFilter = _overlayLayer.gameObject.AddComponent<MeshFilter>();

            _overlayMeshRenderer = _overlayLayer.GetComponent<MeshRenderer>();
            if (_overlayMeshRenderer == null)
                _overlayMeshRenderer = _overlayLayer.gameObject.AddComponent<MeshRenderer>();

            if (_mainMeshRenderer != null)
            {
                _overlayMeshRenderer.sortingLayerID = _mainMeshRenderer.sortingLayerID;
                _overlayMeshRenderer.sortingOrder = _mainMeshRenderer.sortingOrder + sortingOrderOffset;
            }

            _overlayMeshRenderer.sharedMaterial = overlayMaterial;
            _overlayMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayMeshRenderer.receiveShadows = false;
            _overlayMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _overlayMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _overlayLayer.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_overlayApplied) return;
            if (_mainMeshFilter == null || _overlayMeshFilter == null) return;
            if (_overlayMeshFilter.sharedMesh != _mainMeshFilter.sharedMesh)
                _overlayMeshFilter.sharedMesh = _mainMeshFilter.sharedMesh;
        }

        public bool TryGetThreat(out Transform attacker)
        {
            attacker = null;
            if (_settings == null) return false;

            var hits = Physics2D.OverlapCircleAll(
                transform.position, _settings.evadeDetectRadius, ~0);

            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var combat = hit.GetComponentInParent<Enemy.EnemyCombat>();
                if (combat != null && combat.IsAttackInWindup)
                {
                    attacker = combat.transform;
                    return true;
                }
            }
            return false;
        }

        public bool HasThreatInWindup() => TryGetThreat(out _);

        public bool TryStart(int facing)
        {
            if (_active) return false;
            if (_motor == null || !_motor.IsGrounded) return false;

            _active = true;
            _elapsed = 0f;
            _invincibleApplied = false;
            _overlayApplied = false;

            _anim.ForcePlay(PlayerAnimDriver.States.JumpEvade);

            _motor.ForceFacing(facing);
            _motor.SetVelocity(new Vector2(facing * _settings.evadeForceX, 0f));
            _motor.AddForce(new Vector2(0f, _settings.evadeForceY), ForceMode2D.Impulse);

            _audio?.PlayJump();
            return true;
        }

        public void Tick(float dt)
        {
            if (!_active || _settings == null) return;

            _elapsed += dt;

            bool inWindow = _elapsed >= _settings.evadeInvincibleStart
                         && _elapsed <= _settings.evadeInvincibleEnd;

            if (inWindow != _invincibleApplied)
            {
                _invincibleApplied = inWindow;
                _health?.SetEvadeInvincible(inWindow);
            }

            if (inWindow != _overlayApplied)
            {
                _overlayApplied = inWindow;
                SetOverlay(inWindow);
            }

            if (_elapsed >= _settings.evadeDuration)
                End();
        }

        private void SetOverlay(bool on)
        {
            if (_overlayLayer == null) return;

            _overlayLayer.gameObject.SetActive(on);

            if (on && _mainMeshFilter != null && _overlayMeshFilter != null)
                _overlayMeshFilter.sharedMesh = _mainMeshFilter.sharedMesh;
        }

        public void End()
        {
            if (!_active) return;
            _active = false;

            _health?.SetEvadeInvincible(false);
            SetOverlay(false);
            _invincibleApplied = false;
            _overlayApplied = false;
        }

        private void OnDisable() => End();
    }
}