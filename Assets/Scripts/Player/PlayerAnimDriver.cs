using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimDriver : MonoBehaviour
    {
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

        public void SetOwnerGate(PlayerAnimOwner owner)
        {
            _ownerGate = owner;
        }

        public void PlayBase(string stateName, float normalizedTime = 0f)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName))
                return;

            if (_currentBase == stateName && IsPlaying(stateName))
                return;

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
            if (_animator == null || string.IsNullOrEmpty(stateName))
                return;

            _currentBase = stateName;
            _animator.CrossFade(stateName, transitionDuration, 0, normalizedTime);
        }

        public void SyncCurrent(string stateName)
        {
            _currentBase = stateName;
        }

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
            if (_animator == null || string.IsNullOrEmpty(stateName))
                return false;

            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(stateName)
                || info.IsName("SMG." + stateName)
                || IsPlayingPath(stateName)
                || IsPlayingPath("SMG." + stateName);
        }

        public bool AimFinished
        {
            get
            {
                if (_animator == null)
                    return true;

                if (!string.IsNullOrEmpty(_currentAim) && !IsPlayingAim(_currentAim))
                    return false;

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
            if (_animator == null)
                return;

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
            if (_animator == null)
                return false;

            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(stateName) || IsPlayingPath(stateName);
        }

        public float NormalizedTime
        {
            get
            {
                if (_animator == null)
                    return 0f;
                return _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            }
        }

        public bool BaseFinished
        {
            get
            {
                if (_animator == null)
                    return true;

                if (!string.IsNullOrEmpty(_currentBase) && !IsPlaying(_currentBase))
                    return false;

                var info = _animator.GetCurrentAnimatorStateInfo(0);
                return info.normalizedTime >= 1f && !_animator.IsInTransition(0) && !info.loop;
            }
        }

        public void SetBool(string name, bool value)
        {
            if (_animator != null && HasParam(name))
                _animator.SetBool(name, value);
        }

        public void SetFloat(string name, float value)
        {
            if (_animator != null && HasParam(name))
                _animator.SetFloat(name, value);
        }

        public void SetTrigger(string name)
        {
            if (_animator != null && HasParam(name))
                _animator.SetTrigger(name);
        }

        public void SetAimLayerWeight(float weight)
        {
            if (_aimLayer >= 0)
                _animator.SetLayerWeight(_aimLayer, Mathf.Clamp01(weight));
        }

        public void SetCrouch(bool crouch)
        {
            SetBool("crouch", crouch);
            SetCrouchingWeight(crouch ? 1f : 0f);
        }

        public void SetCrouchingWeight(float weight)
        {
            SetFloat("crouching", Mathf.Clamp01(weight));
        }

        public void SetAiming(bool aiming)
        {
            SetBool("aiming", aiming);
        }

        public void SetAirFloat(float value, float dampTime = 0.12f)
        {
            if (_animator == null || !HasParam("airfloat"))
                return;

            if (dampTime <= 0f)
            {
                _animator.SetFloat("airfloat", value);
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.016f;
            _animator.SetFloat("airfloat", value, dampTime, dt);
        }

        public void SetAimDir(float value)
        {
            SetFloat("aimDir", value);
        }

        public void SetAxis(float value)
        {
            SetFloat("axis", value);
        }

        private bool IsPlayingPath(string path)
        {
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(path);
        }

        private int FindLayer(string name)
        {
            if (_animator == null)
                return -1;

            for (int i = 0; i < _animator.layerCount; i++)
            {
                if (_animator.GetLayerName(i) == name)
                    return i;
            }
            return -1;
        }

        private bool HasParam(string name)
        {
            foreach (var p in _animator.parameters)
            {
                if (p.name == name)
                    return true;
            }
            return false;
        }

        public string CurrentBase => _currentBase;

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