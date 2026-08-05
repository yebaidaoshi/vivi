using UnityEngine;



namespace Player 

{

	/// <summary>

	/// Animator 辅助类：优先用完整 Mecanim 路径播放嵌套的 ONAIR 状态

	///（与 HeroineJumpAttackAnimFix 为 PlayMaker 做过的绕过同理）。

	/// </summary>

	[RequireComponent(typeof(Animator))]//强制挂载 Animator

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

        /// <summary>由控制器在仲裁决议后设置。用于所有权断言。</summary>

        public void SetOwnerGate(PlayerAnimOwner owner)

        {

            _ownerGate = owner;//设置动画控制权：规定本帧应由谁操控身体动画

        }

        public void PlayBase(string stateName, float normalizedTime = 0f)//播放循环动画

        {

            if (_animator == null || string.IsNullOrEmpty(stateName))

            {

                return;

            }

            // 如果“上次记录的状态”和“当前传入的状态”相同，并且 Animator 确实正在播放这个状态，

            // 那就直接返回，什么都不做。

            if (_currentBase == stateName && IsPlaying(stateName))

            {

                return;

            }



            _currentBase = stateName;

            TryPlay(stateName, normalizedTime);

        }

        public void ForcePlay(string stateName)//强制播放一次性动画

        {

            _currentBase = stateName;

            TryPlay(stateName, 0f);

        }

        public void SyncCurrent(string stateName)

        {

            _currentBase = stateName;

        }

        public bool IsInSmgAim()//判断是否处于射击瞄准状态；无论在哪一层，只要在射击状态就返回 true

        {

            return IsPlayingAim(States.AimSmg)

                || IsPlayingAim(States.AimSmgHold)

                || IsPlayingAim(States.AimSmgReload)

                || IsPlayingAim(States.AimSmgRelease)

                || IsPlayingAim(States.AimSmgVibration)

                || IsPlayingAim(States.AimSmgReAim)

                || IsPlayingAim(States.CrouchAimToStandAim)

                // 兼容旧逻辑：部分调用方仍在 Base Layer 上 Play Aim_*。

                || IsPlaying(States.AimSmg)

                || IsPlaying(States.AimSmgHold)

                || IsPlaying(States.AimSmgReload)

                || IsPlaying(States.AimSmgRelease)

                || IsPlaying(States.AimSmgVibration)

                || IsPlaying(States.AimSmgReAim)

                || IsPlaying(States.CrouchAimToStandAim);

        }

        /// <summary>

        /// 播放 SMG 瞄准状态。它们位于 Base Layer 下嵌套的 SMG 状态机中

        ///（不是 Aim 层——Aim 层只放 Aim_Standing / Rope）。

        /// </summary>

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

            {

                return false;

            }

            // SMG.* 嵌套在 Base Layer 下。

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

                {

                    return true;

                }



                if (!string.IsNullOrEmpty(_currentAim) && !IsPlayingAim(_currentAim))

                {

                    return false;

                }



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

            {

                return;

            }



            // Base Layer 上优先走嵌套 SMG 路径（Aim_SMG* / Crouch_Aim_to_Stand_Aim）。

            if (layer == 0 && !stateName.StartsWith("SMG.", System.StringComparison.Ordinal))

            {

                string smgPath = "SMG." + stateName;

                int smgHash = Animator.StringToHash(smgPath);

                if (_animator.HasState(layer, smgHash))

                {

                    _animator.Play(smgHash, layer, normalizedTime);

                    return;

                }

            }



            int hash = Animator.StringToHash(stateName);

            if (_animator.HasState(layer, hash))

            {

                _animator.Play(hash, layer, normalizedTime);

                return;

            }



            // 回退到叶子名（例如从 path.Aim_SMG 得到 Aim_SMG）。

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

            {

                return false;

            }



            var info = _animator.GetCurrentAnimatorStateInfo(0);

            return info.IsName(stateName) || IsPlayingPath(stateName);

        }



        public float NormalizedTime

        {

            get

            {

                if (_animator == null)

                {

                    return 0f;

                }



                return _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

            }

        }



        public bool BaseFinished

        {

            get

            {

                if (_animator == null)

                {

                    return true;

                }



                if (!string.IsNullOrEmpty(_currentBase) && !IsPlaying(_currentBase))

                {

                    return false;

                }



                var info = _animator.GetCurrentAnimatorStateInfo(0);

                return info.normalizedTime >= 1f && !_animator.IsInTransition(0) && !info.loop;

            }

        }



        public void SetBool(string name, bool value)

        {

            if (_animator != null && HasParam(name))

            {

                _animator.SetBool(name, value);

            }

        }



        public void SetFloat(string name, float value)

        {

            if (_animator != null && HasParam(name))

            {

                _animator.SetFloat(name, value);

            }

        }



        public void SetTrigger(string name)

        {

            if (_animator != null && HasParam(name))

            {

                _animator.SetTrigger(name);

            }

        }



        public void SetAimLayerWeight(float weight)

        {
            if (_aimLayer >= 0)

            {

                _animator.SetLayerWeight(_aimLayer, Mathf.Clamp01(weight));

            }

        }



        public void SetCrouch(bool crouch)

        {

            SetBool("crouch", crouch);

            SetCrouchingWeight(crouch ? 1f : 0f);

        }



        /// <summary>仅设置 Aim/SMG BlendTree 参数（不改动 bool <c>crouch</c>）。</summary>

        public void SetCrouchingWeight(float weight)

        {

            SetFloat("crouching", Mathf.Clamp01(weight));

        }



        public void SetAiming(bool aiming)

        {

            SetBool("aiming", aiming);

        }



        /// <summary>

        /// Jump / OnAir 混合：-1 向后，0 中立，+1 向前（朝向 * 移动）。

        /// 使用 Animator dampTime，使左右倾侧像 PlayMaker SetAnimatorFloat 一样缓入。

        /// </summary>

        public void SetAirFloat(float value, float dampTime = 0.12f)

        {

            if (_animator == null || !HasParam("airfloat"))

            {

                return;

            }



            if (dampTime <= 0f)

            {

                _animator.SetFloat("airfloat", value);

                return;

            }



            float dt = Time.deltaTime;

            if (dt <= 0f)

            {

                dt = 0.016f;

            }



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

            {

                return -1;

            }



            for (int i = 0; i < _animator.layerCount; i++)

            {

                if (_animator.GetLayerName(i) == name)

                {

                    return i;

                }

            }



            return -1;

        }



        private bool HasParam(string name)

        {

            foreach (var p in _animator.parameters)

            {

                if (p.name == name)

                {

                    return true;

                }

            }



            return false;

        }



        // 女主控制器使用的规范状态路径。

        public static class States

        {

            public const string Idle = "Idle";

            public const string Run = "Run";

            public const string RunTurning = "Run_Turning";

            public const string RunToIdle = "Run_to_Idle";

            public const string Landing = "Landing";

            public const string LandingToRun = "Landing_to_Run";

            public const string LandingToRunForward = "Landing_to_Run_Forward";

            public const string BackStep = "BackStep";

            public const string Crouch = "Crouch";

            public const string Crouching = "Crouching";

            public const string CrouchToIdle = "Crouch_To_Idle";

            public const string Slide = "Slide";

            public const string Sliding = "Sliding";

            public const string SlideToIdle = "Slide_To_Idle";

            public const string Roll = "Roll";

            // Jump FSM / ONAIR：Jump exitTime → OnAir（airfloat 混合树）。

            public const string Jump = "Jump";

            public const string OnAir = "OnAir";

            public const string JumpAttackUp = "ONAIR.Jump_Attack_Up";

            public const string JumpAttackDown = "ONAIR.Jump_Attack_Down";

            public const string BackFlip = "ONAIR.Jump/Jump_BackFlip";

            public const string BackFlipLand = "ONAIR.Jump/Jump_BackFlip_Land";

            // 控制器状态为 Attack1 / Attack2 / Attack4（没有 Attack3）。

            public const string Attack1 = "Attack1";

            public const string Attack2 = "Attack2";

            public const string Attack3 = "Attack4";

            public const string Attack4 = "Attack4";

            public const string SlideAttack = "Slide_Attack";

            // 短名称与 floor.unity 中 GunFire 的 AnimatorPlay 一致。

            public const string AimSmg = "Aim_SMG";

            public const string AimSmgHold = "Aim_SMG_Hold";

            public const string AimSmgRelease = "Aim_SMG_Release";

            public const string AimSmgReload = "Aim_SMG_Reload";

            public const string AimSmgVibration = "Aim_SMG_vibration";

            public const string AimSmgReAim = "Aim_SMG_ReAim";

            /// <summary>

            /// Aim/SMG BlendTree：crouching=0 → Aim_Aim_SMG_Hold_to_Crouch_Aim，

            /// crouching=1 → Crouch_Crouch_Aim_to_Stand_Aim。

            /// </summary>

            public const string CrouchAimToStandAim = "Crouch_Aim_to_Stand_Aim";

            // floor Magic FSM 的 AnimatorPlay 名称。

            public const string MagicChannel = "Magic_Channel";

            public const string MagicChanneling = "Magic_Channeling";

            public const string MagicChannelOnAir = "Magic_Channel_OnAir";

            public const string MagicChannelingOnAir = "Magic_Channeling_OnAir";

            public const string MagicChannelCancel = "Magic_Channel_Cancel";

            public const string MagicChannelCancelOnAir = "Magic_Channel_Cancel_OnAir";

            public const string MagicFrontCast = "Magic_FrontCast";

        }

    }

}

