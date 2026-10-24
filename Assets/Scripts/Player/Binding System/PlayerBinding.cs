using UnityEngine;

namespace Player
{
    public interface IBindingSource
    {
        bool IsBinding { get; }
        BindingProfile Profile { get; }
        void OnPlayerEscaped();
        void OnPlayerDied();
    }

    public enum BindingPhase
    {
        None,
        Pinned,
        Loop,
        Escape,
        Dead
    }

    public class PlayerBinding : MonoBehaviour
    {
        public static PlayerBinding Instance { get; private set; }

        [Header("挣脱 UI 预制体")]
        [SerializeField] private GameObject struggleUIPrefab;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerHealth _health;

        private IBindingSource _source;
        private BindingProfile _profile;
        private PlayerStruggleUI _struggleUI;
        private Rigidbody2D _rb;
        private RigidbodyType2D _originalBodyType;

        private BindingPhase _phase = BindingPhase.None;
        private float _phaseTimer;
        private float _struggleProgress;
        private float _lastPressTime = -10f;
        private bool _prevAds;

        public bool IsActive => _phase != BindingPhase.None;
        public BindingPhase Phase => _phase;
        public float StruggleProgress => _struggleProgress;
        public IBindingSource Source => _source;

        public event System.Action<float> OnStruggleProgressChanged;
        public event System.Action OnEscapeSucceeded;

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            if (_rb != null) _originalBodyType = _rb.bodyType;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyStruggleUI();
        }

        public void Init(PlayerContext ctx, PlayerHealth health)
        {
            ctx.Bind(out _motor, out _anim, out _audio, out _);
            _health = health;
        }

        private static bool IsSourceAlive(IBindingSource s)
        {
            if (s == null) return false;
            if (s is UnityEngine.Object obj) return obj != null;
            return true;
        }

        private bool SourceAlive => IsSourceAlive(_source);

        public void HandleBindingHit(IBindingSource source, int damage, GameObject attacker)
        {
            if (!IsSourceAlive(source)) return;
            if (!source.IsBinding) return;

            if (!IsActive || _source != source)
                EnterBinding(source);

            ApplyDamage(damage, attacker);
        }

        private void EnterBinding(IBindingSource source)
        {
            _source = source;
            _profile = source.Profile;

            if (_profile == null)
            {
                _source = null;
                return;
            }

            _phase = BindingPhase.Pinned;
            _phaseTimer = _profile.pinnedDuration;
            _struggleProgress = 0f;
            _lastPressTime = -10f;
            _prevAds = false;

            _motor?.SetVelocity(Vector2.zero);
            _anim?.ForcePlay(_profile.pinnedAnim);

            OnStruggleProgressChanged?.Invoke(0f);
        }

        public void Tick(in PlayerIntent intent)
        {
            if (!IsActive) return;

            _motor?.SetVelocity(Vector2.zero);

            switch (_phase)
            {
                case BindingPhase.Pinned: TickPinned(Time.deltaTime); break;
                case BindingPhase.Loop:   TickLoop(intent, Time.deltaTime); break;
                case BindingPhase.Escape: TickEscape(Time.deltaTime); break;
                case BindingPhase.Dead:   TickDead(Time.deltaTime); break;
            }

            _prevAds = intent.Ads;
        }

        private void TickPinned(float dt)
        {
            if (_health != null && _health.CurrentHealth <= 0)
            {
                EnterDead();
                return;
            }

            _phaseTimer -= dt;
            if (_phaseTimer <= 0f) EnterLoop();
        }

        private void EnterLoop()
        {
            _phase = BindingPhase.Loop;
            _phaseTimer = 0f;
            _anim?.ForcePlay(_profile.loopAnim);

            SpawnStruggleUI();
        }

        private void TickLoop(in PlayerIntent intent, float dt)
        {
            if (_health != null && _health.CurrentHealth <= 0)
            {
                EnterDead();
                return;
            }

            int presses = CountStrugglePresses(intent, _profile.allowedKeys);
            if (presses > 0 && (Time.time - _lastPressTime) >= _profile.minPressInterval)
            {
                _lastPressTime = Time.time;
                _struggleProgress = Mathf.Clamp01(
                    _struggleProgress + _profile.strugglePerPress * presses);

                OnStruggleProgressChanged?.Invoke(_struggleProgress);
                if (_profile.struggleSe != null) _audio?.Play(_profile.struggleSe);

                _struggleUI?.UpdateProgress(_struggleProgress);

                if (_struggleProgress >= 1f) EnterEscape();
            }
            else if (_profile.struggleDecayPerSec > 0f && presses == 0)
            {
                _struggleProgress = Mathf.Max(0f,
                    _struggleProgress - _profile.struggleDecayPerSec * dt);
                OnStruggleProgressChanged?.Invoke(_struggleProgress);

                _struggleUI?.UpdateProgress(_struggleProgress);
            }
        }

        private int CountStrugglePresses(in PlayerIntent i, StruggleKeys keys)
        {
            int n = 0;
            if ((keys & StruggleKeys.Left)  != 0 && i.MoveLeftPressed)  n++;
            if ((keys & StruggleKeys.Right) != 0 && i.MoveRightPressed) n++;
            if ((keys & StruggleKeys.Up)    != 0 && i.JumpPressed)      n++;
            if ((keys & StruggleKeys.Down)  != 0 && i.CrouchPressed)    n++;
            if ((keys & StruggleKeys.LMB)   != 0 && i.FirePressed)      n++;
            if ((keys & StruggleKeys.RMB)   != 0 && i.Ads && !_prevAds) n++;
            return n;
        }

        private void EnterEscape()
        {
            _phase = BindingPhase.Escape;
            _phaseTimer = _profile.escapeDuration;
            _anim?.ForcePlay(_profile.escapeAnim);
            if (_profile.escapeSe != null) _audio?.Play(_profile.escapeSe);

            _struggleUI?.PlayUnlock();
            _struggleUI = null;

            if (SourceAlive) _source.OnPlayerEscaped();
            OnEscapeSucceeded?.Invoke();
        }

        private void TickEscape(float dt)
        {
            if (_health != null && _health.CurrentHealth <= 0)
            {
                EnterDead();
                return;
            }

            _phaseTimer -= dt;
            if (_phaseTimer <= 0f) EndBinding();
        }

        private void EnterDead()
        {
            _phase = BindingPhase.Dead;
            _phaseTimer = _profile.deadDuration;
            _anim?.ForcePlay(_profile.deadAnim);
            if (_profile.deathSe != null) _audio?.Play(_profile.deathSe);

            DestroyStruggleUI();

            _health?.LockControllerForDeath();
            if (SourceAlive) _source.OnPlayerDied();
        }

        private void TickDead(float dt)
        {
            _phaseTimer -= dt;
            if (_phaseTimer <= 0f)
            {
                _health?.NotifyBindingDeathComplete();
                EndBinding();
            }
        }

        private void EndBinding()
        {
            _phase = BindingPhase.None;
            _phaseTimer = 0f;
            _struggleProgress = 0f;
            _source = null;
            _profile = null;

            DestroyStruggleUI();

            if (_rb != null)
                _rb.bodyType = _originalBodyType;

            OnStruggleProgressChanged?.Invoke(0f);
        }

        private void ApplyDamage(int damage, GameObject attacker)
        {
            _health?.TakeBindingDamage(damage, attacker);
        }

        public void ForceEnd()
        {
            if (!IsActive) return;
            if (SourceAlive) _source.OnPlayerEscaped();
            EndBinding();
        }

        private void SpawnStruggleUI()
        {
            if (struggleUIPrefab == null || _struggleUI != null) return;

            var go = Instantiate(struggleUIPrefab);
            
            _struggleUI = go.GetComponent<PlayerStruggleUI>();
            if (_struggleUI != null)
            {
                _struggleUI.Show(transform);
            }
        }

        private void DestroyStruggleUI()
        {
            if (_struggleUI != null)
            {
                Destroy(_struggleUI.gameObject);
                _struggleUI = null;
            }
        }
    }
}