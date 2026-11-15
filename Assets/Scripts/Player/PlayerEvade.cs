using UnityEngine;
using UnityEngine.Rendering;
using Spine;
using Spine.Unity;
using SceneCamera;

namespace Player
{
    public class PlayerEvade : MonoBehaviour
    {
        [Header("叠加剪影材质")]
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private int sortingOrderOffset = 1;

        [Header("取消窗口")]
        [SerializeField] private float cancelableAt = 47f / 60f;

        [Header("完美闪避（无敌帧内躲过敌人攻击判定）")]
        [Tooltip("完美闪避成功的震屏时长 / 强度")]
        [SerializeField] private float perfectShakeDuration = 0.16f;
        [SerializeField] private float perfectShakeStrength = 0.3f;
        [Tooltip("完美闪避成功后额外延长多少秒无敌（方便穿出攻击）")]
        [SerializeField] private float perfectExtraInvincible = 0.08f;
        [Tooltip("同一次闪避最短判定间隔，防止一个判定盒多次触发")]
        [SerializeField] private float perfectDodgeGuard = 0.15f;

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

        private float _invincibleBonus;
        private float _perfectGuardUntil;

        /// <summary>完美闪避成功（参数 = 被躲过的攻击者）。</summary>
        public event System.Action<GameObject> OnPerfectDodge;

        /// <summary>累计完美闪避次数（调试/成就用）。</summary>
        public int PerfectDodgeCount { get; private set; }

        public bool IsActive => _active;
        public float Elapsed => _elapsed;
        public float CancelableAt => cancelableAt;
        public bool CanCancel => _active && _elapsed >= cancelableAt;

        /// <summary>现在是不是闪避的无敌帧窗口（原版判定"完美闪避"就靠这个）。</summary>
        public bool IsInvincibleWindow => _active && _invincibleApplied;

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

        /// <summary>
        /// 完美闪避：闪避的无敌帧窗口里被敌人攻击判定扫到时，由 EnemyCombat 调用。
        /// 原版的判定就是"闪避无敌帧躲过正在生效的攻击判定"，反馈 = 拖影 + 震屏 + 敌人攻击落空。
        /// </summary>
        public void NotifyPerfectDodge(GameObject attacker)
        {
            if (!IsInvincibleWindow) return;
            if (Time.time < _perfectGuardUntil) return;

            _perfectGuardUntil = Time.time + Mathf.Max(0.05f, perfectDodgeGuard);
            PerfectDodgeCount++;

            _invincibleBonus += Mathf.Max(0f, perfectExtraInvincible);

            var cam = CameraDirector.Instance;
            if (cam != null) cam.Shake(perfectShakeDuration, perfectShakeStrength);

            _audio?.PlayBackStep();

            OnPerfectDodge?.Invoke(attacker);
        }

        public bool TryStart(int facing)
        {
            if (_active) return false;
            if (_motor == null || !_motor.IsGrounded) return false;

            _active = true;
            _elapsed = 0f;
            _invincibleApplied = false;
            _overlayApplied = false;
            _invincibleBonus = 0f;

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
                         && _elapsed <= _settings.evadeInvincibleEnd + _invincibleBonus;

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