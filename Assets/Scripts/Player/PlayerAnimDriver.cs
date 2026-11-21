using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimDriver : MonoBehaviour
    {
        /// <summary>
        /// Base Layer 里有两套同名状态：根层的单片段"副本"（只有站姿）和 SMG 子状态机里按 crouching 混合的 BlendTree（原版）。
        /// 用短名 Play 会撞名，蹲姿时必须播 SMG 里的那一套。
        /// </summary>
        public enum SmgRouteMode
        {
            /// <summary>只有 crouching &gt; 0.5 时才路由到 SMG 子状态机（站姿行为保持原样，推荐）。</summary>
            CrouchOnly,
            /// <summary>站 / 蹲都走 SMG 子状态机（与原版 Animator 设计一致，但会改变站姿所用的 clip 副本）。</summary>
            Always,
            /// <summary>关闭路由（回到旧行为）。</summary>
            Off,
        }

        [Header("SMG 同名状态路由")]
        [SerializeField] private SmgRouteMode smgRouteMode = SmgRouteMode.CrouchOnly;
        [SerializeField] private bool logSmgRouting;

        private const string SmgMachine = "SMG";
        private const string SmgPrefix = "SMG.";

        // Base Layer 根层与 SMG 子状态机里同名的状态（+ 只存在于 SMG 的蹲/站过渡）
        private static readonly HashSet<string> SmgLeaves = new HashSet<string>
        {
            "Aim_SMG", "Aim_SMG_Hold", "Aim_SMG_ReAim", "Aim_SMG_Release",
            "Aim_SMG_Reload", "Aim_SMG_TooClose", "Aim_SMG_vibration",
            "Crouch_Aim_to_Stand_Aim",
        };

        private readonly Dictionary<string, int> _smgPlayHash = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _smgFullHash = new Dictionary<string, int>();
        private RuntimeAnimatorController _smgCacheController;
        private float _crouchingValue;

        private Animator _animator;
        private string _currentBase;
        private string _currentAim;
        private int _aimLayer = -1;
        private PlayerAnimOwner _ownerGate = PlayerAnimOwner.None;

        public Animator Animator => _animator;
        public PlayerAnimOwner OwnerGate => _ownerGate;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _aimLayer = FindLayer("Aim");
        }

        public void SetOwnerGate(PlayerAnimOwner owner) => _ownerGate = owner;

        public void PlayBase(string stateName, float normalizedTime = 0f)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return;
            if (_currentBase == stateName && IsPlaying(stateName)) return;
            _currentBase = stateName;
            TryPlay(stateName, normalizedTime);
        }

        public void ForcePlay(string stateName)
        {
            _currentBase = stateName;
            TryPlay(stateName, 0f);
        }

        public void CrossFade(string stateName, float transitionDuration = 0.15f, float normalizedTime = 0f)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return;
            _currentBase = stateName;

            if (TryGetSmgRoute(stateName, out var smgLeaf) && TryGetSmgPlayHash(smgLeaf, out var smgHash))
            {
                _animator.CrossFade(smgHash, transitionDuration, 0, normalizedTime);
                return;
            }

            _animator.CrossFade(stateName, transitionDuration, 0, normalizedTime);
        }

        public void SyncCurrent(string stateName) => _currentBase = stateName;

        public bool IsInSmgAim()
        {
            return IsPlayingAim(States.AimSmg)
                || IsPlayingAim(States.AimSmgHold)
                || IsPlayingAim(States.AimSmgReload)
                || IsPlayingAim(States.AimSmgRelease)
                || IsPlayingAim(States.AimSmgVibration)
                || IsPlayingAim(States.AimSmgReAim)
                || IsPlayingAim(States.CrouchAimToStandAim)
                || IsPlaying(States.AimSmg)
                || IsPlaying(States.AimSmgHold)
                || IsPlaying(States.AimSmgReload)
                || IsPlaying(States.AimSmgRelease)
                || IsPlaying(States.AimSmgVibration)
                || IsPlaying(States.AimSmgReAim)
                || IsPlaying(States.CrouchAimToStandAim);
        }

        public void ForcePlayAim(string stateName)
        {
            _currentAim = stateName;
            _currentBase = stateName;
            TryPlayOnLayer(0, stateName, 0f);
        }

        public void SyncAim(string stateName)
        {
            _currentAim = stateName;
            _currentBase = stateName;
        }

        public bool IsPlayingAim(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(0);

            // 撞名状态按完整路径精确比较：别把根层的站姿副本误判成"正在播 SMG 蹲瞄准"
            if (TryGetSmgRoute(stateName, out var smgLeaf) && TryGetSmgPlayHash(smgLeaf, out _))
            {
                return info.fullPathHash == SmgFullPathHash(smgLeaf);
            }

            return info.IsName(stateName)
                || info.IsName("SMG." + stateName)
                || IsPlayingPath(stateName)
                || IsPlayingPath("SMG." + stateName);
        }

        public bool AimFinished
        {
            get
            {
                if (_animator == null) return true;
                if (!string.IsNullOrEmpty(_currentAim) && !IsPlayingAim(_currentAim)) return false;
                var info = _animator.GetCurrentAnimatorStateInfo(0);
                return info.normalizedTime >= 1f && !_animator.IsInTransition(0) && !info.loop;
            }
        }

        private void TryPlay(string stateName, float normalizedTime)
        {
            TryPlayOnLayer(0, stateName, normalizedTime);
        }

        private void TryPlayOnLayer(int layer, string stateName, float normalizedTime)
        {
            if (_animator == null) return;

            if (layer == 0 && TryGetSmgRoute(stateName, out var smgLeaf) && TryGetSmgPlayHash(smgLeaf, out var smgHash))
            {
                _animator.Play(smgHash, 0, normalizedTime);
                return;
            }

            int hash = Animator.StringToHash(stateName);
            if (_animator.HasState(layer, hash))
            {
                _animator.Play(hash, layer, normalizedTime);
                return;
            }

            int dot = stateName.LastIndexOf('.');
            if (dot >= 0 && dot < stateName.Length - 1)
            {
                string leaf = stateName.Substring(dot + 1);
                int leafHash = Animator.StringToHash(leaf);
                if (_animator.HasState(layer, leafHash))
                {
                    _animator.Play(leafHash, layer, normalizedTime);
                    return;
                }
            }

            _animator.Play(stateName, layer, normalizedTime);
        }

        public bool IsPlaying(string stateName)
        {
            if (_animator == null) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(0);

            if (TryGetSmgRoute(stateName, out var smgLeaf) && TryGetSmgPlayHash(smgLeaf, out _))
            {
                return info.fullPathHash == SmgFullPathHash(smgLeaf);
            }

            return info.IsName(stateName) || IsPlayingPath(stateName);
        }

        public float NormalizedTime
        {
            get
            {
                if (_animator == null) return 0f;
                return _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            }
        }

        public bool BaseFinished
        {
            get
            {
                if (_animator == null) return true;
                if (!string.IsNullOrEmpty(_currentBase) && !IsPlaying(_currentBase)) return false;
                var info = _animator.GetCurrentAnimatorStateInfo(0);
                return info.normalizedTime >= 1f && !_animator.IsInTransition(0) && !info.loop;
            }
        }

        public void SetBool(string name, bool value)
        {
            if (_animator != null && HasParam(name)) _animator.SetBool(name, value);
        }

        public void SetFloat(string name, float value)
        {
            if (_animator != null && HasParam(name)) _animator.SetFloat(name, value);
        }

        public void SetTrigger(string name)
        {
            if (_animator != null && HasParam(name)) _animator.SetTrigger(name);
        }

        public void SetAimLayerWeight(float weight)
        {
            if (_aimLayer >= 0) _animator.SetLayerWeight(_aimLayer, Mathf.Clamp01(weight));
        }

        public void SetCrouch(bool crouch)
        {
            SetBool("crouch", crouch);
            SetCrouchingWeight(crouch ? 1f : 0f);
        }

        public void SetCrouchingWeight(float weight)
        {
            _crouchingValue = Mathf.Clamp01(weight);
            SetFloat("crouching", _crouchingValue);
        }

        public void SetAiming(bool aiming) => SetBool("aiming", aiming);

        public void SetAirFloat(float value, float dampTime = 0.12f)
        {
            if (_animator == null || !HasParam("airfloat")) return;

            if (dampTime <= 0f)
            {
                _animator.SetFloat("airfloat", value);
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.016f;
            _animator.SetFloat("airfloat", value, dampTime, dt);
        }

        public void SetAimDir(float value) => SetFloat("aimDir", value);
        public void SetAxis(float value) => SetFloat("axis", value);

        public void FreezeAnimator()
        {
            if (_animator != null) _animator.speed = 0f;
        }

        public void ResumeAnimator()
        {
            if (_animator != null) _animator.speed = 1f;
        }

        private bool IsPlayingPath(string path)
        {
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(path);
        }

        private int FindLayer(string name)
        {
            if (_animator == null) return -1;
            for (int i = 0; i < _animator.layerCount; i++)
                if (_animator.GetLayerName(i) == name) return i;
            return -1;
        }

        private bool HasParam(string name)
        {
            foreach (var p in _animator.parameters)
                if (p.name == name) return true;
            return false;
        }

        public string CurrentBase => _currentBase;

        /// <summary>Animator 里有没有这个状态（Base 层，按名字）。</summary>
        public bool HasState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return false;
            var a = GetComponent<Animator>();
            return a != null && a.HasState(0, Animator.StringToHash(stateName));
        }

        // ───────────────────────── SMG 同名状态路由 ─────────────────────────

        /// <summary>这个名字要不要改走 SMG 子状态机里的那一套（只判断名字 + 当前模式）。</summary>
        private bool TryGetSmgRoute(string stateName, out string leaf)
        {
            leaf = null;
            if (_animator == null || smgRouteMode == SmgRouteMode.Off || string.IsNullOrEmpty(stateName)) return false;

            bool explicitPath = stateName.StartsWith(SmgPrefix);
            leaf = explicitPath ? stateName.Substring(SmgPrefix.Length) : stateName;
            if (!SmgLeaves.Contains(leaf)) return false;

            if (explicitPath || smgRouteMode == SmgRouteMode.Always) return true;
            return _crouchingValue > 0.5f;
        }

        private void RefreshSmgCache()
        {
            if (_smgCacheController == _animator.runtimeAnimatorController) return;
            _smgCacheController = _animator.runtimeAnimatorController;
            _smgPlayHash.Clear();
            _smgFullHash.Clear();
        }

        /// <summary>"Base Layer.SMG.Aim_SMG_Hold" 的 hash，等于 AnimatorStateInfo.fullPathHash。</summary>
        private int SmgFullPathHash(string leaf)
        {
            RefreshSmgCache();
            if (!_smgFullHash.TryGetValue(leaf, out var hash))
            {
                hash = Animator.StringToHash(_animator.GetLayerName(0) + "." + SmgMachine + "." + leaf);
                _smgFullHash[leaf] = hash;
            }

            return hash;
        }

        /// <summary>SMG 里这个状态能不能被 Play 到（HasState 验证，结果缓存）。</summary>
        private bool TryGetSmgPlayHash(string leaf, out int hash)
        {
            RefreshSmgCache();
            if (_smgPlayHash.TryGetValue(leaf, out hash)) return hash != 0;

            int full = SmgFullPathHash(leaf);
            int relative = Animator.StringToHash(SmgPrefix + leaf);
            hash = _animator.HasState(0, full) ? full : (_animator.HasState(0, relative) ? relative : 0);
            _smgPlayHash[leaf] = hash;

            if (hash == 0)
            {
                Debug.LogWarning($"[PlayerAnimDriver] 找不到 SMG 子状态机里的状态 '{leaf}'（Base Layer.SMG.{leaf}），蹲姿将退回短名播放。", this);
            }
            return hash != 0;
        }

        /// <summary>按"层/子状态机/状态"完整路径播放（Base 层的蹲瞄准状态就需要这个）。</summary>
        public bool PlayByFullPath(string fullPath, float normalizedTime = 0f)
        {
            if (string.IsNullOrEmpty(fullPath)) return false;

            var a = GetComponent<Animator>();
            if (a == null) return false;

            var hash = Animator.StringToHash(fullPath);

            if (!a.HasState(0, hash)) return false;

            a.Play(hash, 0, Mathf.Clamp01(normalizedTime));
            return true;
        }

        /// <summary>当前 Base 层是不是这个完整路径的状态。</summary>
        public bool IsStateFullPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return false;

            var a = GetComponent<Animator>();
            if (a == null) return false;

            return a.GetCurrentAnimatorStateInfo(0).IsName(fullPath);
        }
        /// <summary>在指定层上按"子状态机/状态"路径播放。</summary>
        public bool PlayByFullPathOnLayer(string fullPath, int layer, float normalizedTime = 0f)
        {
            if (string.IsNullOrEmpty(fullPath)) return false;

            var a = GetComponent<Animator>();
            if (a == null || layer < 0 || layer >= a.layerCount) return false;

            var hash = Animator.StringToHash(fullPath);
            if (!a.HasState(layer, hash)) return false;

            a.Play(hash, layer, Mathf.Clamp01(normalizedTime));
            return true;
        }

        public static class States
        {
            public const string Idle = "Idle";
            public const string Run = "Run";
            public const string RunTurning = "Run_Turning";
            public const string RunToIdle = "Run_to_Idle";

            public const string Jump = "Jump";
            public const string OnAir = "OnAir";
            public const string JumpAttackUp = "ONAIR.Jump_Attack_Up";
            public const string JumpAttackDown = "ONAIR.Jump_Attack_Down";
            public const string BackFlip = "ONAIR.Jump/Jump_BackFlip";
            public const string BackFlipLand = "ONAIR.Jump/Jump_BackFlip_Land";
            public const string Landing = "Landing";
            public const string LandingToRun = "Landing_to_Run";
            public const string LandingToRunForward = "Landing_to_Run_Forward";

            // ★ 新增：滑铲跳 / 越敌跳
            public const string JumpDash = "Jump_Dash";
            public const string JumpEvade = "Jump_Evade";

            public const string BackStep = "BackStep";
            public const string StepForward2 = "Step_Forward2";

            public const string Crouch = "Crouch";
            public const string Crouching = "Crouching";
            public const string CrouchToIdle = "Crouch_To_Idle";
            public const string Slide = "Slide";
            public const string Sliding = "Sliding";
            public const string SlideToIdle = "Slide_To_Idle";
            public const string Roll = "Roll";

            public const string Attack1 = "Attack1";
            public const string Attack2 = "Attack2";
            public const string Attack3 = "Attack4";
            public const string Attack4 = "Attack4";
            public const string SlideAttack = "Slide_Attack";

            public const string AimSmg = "Aim_SMG";
            public const string AimSmgHold = "Aim_SMG_Hold";
            public const string AimSmgRelease = "Aim_SMG_Release";
            public const string AimSmgReload = "Aim_SMG_Reload";
            public const string AimSmgVibration = "Aim_SMG_vibration";
            public const string AimSmgReAim = "Aim_SMG_ReAim";
            public const string CrouchAimToStandAim = "Crouch_Aim_to_Stand_Aim";

            public const string MagicChannel = "Magic_Channel";
            public const string MagicChanneling = "Magic_Channeling";
            public const string MagicChannelOnAir = "Magic_Channel_OnAir";
            public const string MagicChannelingOnAir = "Magic_Channeling_OnAir";
            public const string MagicChannelCancel = "Magic_Channel_Cancel";
            public const string MagicChannelCancelOnAir = "Magic_Channel_Cancel_OnAir";
            public const string MagicFrontCast = "Magic_FrontCast";

            public const string Damage_A = "Damage_A";
            public const string Damage_A2 = "Damage/Damage_A2";
            public const string Damage_B = "Damage/Damage_B";
            public const string Shirimochi = "Shirimochi_1";
            public const string Shirimochi_Dead = "Shirimochi_Dead";
            public const string Damage_A_Dead = "Damage_A_Dead";

            public const string Idle_Damage_A = "Idle_Damage_A";

            public const string AimWalk = "Aim_Walk";
            public const string AimWalkBackward = "Aim_Walk_Backward";
        }
    }
}