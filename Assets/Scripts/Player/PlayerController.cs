using UnityEngine;

namespace Player
{
    [DefaultExecutionOrder(-100)]//数值越小，脚本越早执行
    [RequireComponent(typeof(Rigidbody2D))]//确保游戏对象上有 Rigidbody2D 组件，如果没有则自动添加
    [RequireComponent(typeof(Animator))]//确保游戏对象上有 Animator 组件，如果没有则自动添加
    public class PlayerController : MonoBehaviour
    {
        [Header("Modules")]
        [SerializeField] private PlayerInputReader inputReader;//输入读取组件
        [SerializeField] private PlayerMotor motor;//移动组件
        [SerializeField] private PlayerJump jump;//跳跃组件
        [SerializeField] private PlayerCrouch crouch;//蹲下组件
        [SerializeField] private PlayerMelee melee;//近战组件
        [SerializeField] private PlayerGun gun;//枪械组件
        [SerializeField] private PlayerBackStep backStep;//后撤步组件
        [SerializeField] private PlayerMagic magic;//魔法组件
        [SerializeField] private PlayerAnimDriver anim;//动画驱动组件
        [SerializeField] private PlayerAudio audioPlayer;//音频播放器组件

        [Header("Options")]
        [SerializeField] private bool controlLocomotion = true;//是否控制移动
        [SerializeField] private bool locked;//是否锁定玩家控制

        private bool _initialized = false;
        private PlayerContext _ctx;
        private PlayerLocomotion _loco;
        private PlayerCapabilities _caps;

        public PlayerIntent Intent => inputReader != null ? inputReader.Intent : default;//判断inputReader是否不等与null，如果不等于null就返回inputReader.Intent，否则返回(;后面的内容)defaul（默认值）
        public PlayerMotor Motor => motor;//封装的好处 外部可读取但不能修改
        public PlayerCapabilities Caps => _caps;
        public PlayerContext Context => _ctx;
        public bool Locked
        {
            get => locked;
            set => locked = value;
        }
        private void Awake()
        {
            EnsureModules();
            _ctx = new PlayerContext();
            _loco = new PlayerLocomotion();
            WireModules();//连接模块
            _initialized = true;
        }
        private void EnsureModules()//确保模块存在，如果不存在则添加
        {
            inputReader = inputReader ?? GetComponent<PlayerInputReader>() ?? gameObject.AddComponent<PlayerInputReader>();
            motor = motor ?? GetComponent<PlayerMotor>() ?? gameObject.AddComponent<PlayerMotor>();
            anim = anim ?? GetComponent<PlayerAnimDriver>() ?? gameObject.AddComponent<PlayerAnimDriver>();
            audioPlayer = audioPlayer ?? ResolveAudioModule();
            jump = jump ?? GetComponent<PlayerJump>() ?? gameObject.AddComponent<PlayerJump>();
            crouch = crouch ?? GetComponent<PlayerCrouch>() ?? gameObject.AddComponent<PlayerCrouch>();
            melee = melee ?? GetComponent<PlayerMelee>() ?? gameObject.AddComponent<PlayerMelee>();
            gun = gun ?? GetComponent<PlayerGun>() ?? gameObject.AddComponent<PlayerGun>();
            backStep = backStep ?? GetComponent<PlayerBackStep>() ?? gameObject.AddComponent<PlayerBackStep>();
            magic = magic ?? GetComponent<PlayerMagic>() ?? gameObject.AddComponent<PlayerMagic>();
        }
        //?? (空合并运算符) 逻辑： 如果左边不是 null，就用左边；如果左边是 null，就用右边
        private PlayerAudio ResolveAudioModule()//确保音频模块存在，如果不存在则添加
        {
            Transform audioRoot = transform.Find("Audio");
            if (audioRoot == null)
            {
                var go = new GameObject("Audio");
                go.layer = gameObject.layer;
                audioRoot = go.transform;
                audioRoot.SetParent(transform, false);
            }

            var onAudio = audioRoot.GetComponent<PlayerAudio>();
            if (onAudio != null)
            {
                return onAudio;
            }

            // Legacy: previously lived on the Heroine root — migrate if present.
            var onRoot = GetComponent<PlayerAudio>();
            if (onRoot != null)
            {
                return onRoot;
            }

            return audioRoot.gameObject.AddComponent<PlayerAudio>();
        }
        public void SendEvent(string eventName)//发送事件给音频播放器
        {
            audioPlayer?.SendEvent(eventName);
        }//?表示如果audioPlayer不等于null就调用SendEvent方法，否则不做任何操作

        private void WireModules()//连接模块
        {
            _ctx.Motor = motor;//连接移动组件
            _ctx.Anim = anim;//连接动画驱动组件
            _ctx.Audio = audioPlayer;//连接音频播放器组件
            _ctx.Settings = motor.Settings;//连接移动组件的设置
            _ctx.NotifyJumpAttack = jump.NotifyJumpAttack;//连接跳跃组件的通知方法

            jump.Init(_ctx);
            crouch.Init(_ctx);
            melee.Init(_ctx);
            gun.Init(_ctx);
            backStep.Init(_ctx);
            magic.Init(_ctx);
        }
        private void Update()
        {

            if (!_initialized || locked)
            {
                return;
            }
            var intent = inputReader.Intent;
            _ctx.Intent = intent;

            backStep.Tick();
            _loco.TickTimers(motor.DeltaTime);

            ResolveCaps(setAnimGate: true);

            // IsAds: crouch↔stand aim BlendTree. OwnsCrouchBaseAnim: also covers Aim_SMG_Release
            // so crouch does not PlayBase Crouching over crouch-ADS fold-out (jitter).
            // yieldBaseAnim: crouch must not overwrite Attack* while melee owns the swing.                             
            crouch.Tick(intent/*这一帧的输入*/, jump.OnAir/*当前是否在空中*/, _caps.CanCrouch/*能蹲吗*/,//传入玩家意图、是否在空中、是否可以蹲下便于处理蹲下脚本具体逻辑
                adsActive: gun.IsAds/*枪械正在瞄准吗*/, gunOwnsBaseAnim: gun.OwnsCrouchBaseAnim/*枪械占用了基础动画层吗*/,
                yieldBaseAnim: melee.IsAttacking || magic.IsBusy)/*近战或魔法正在播放动画吗*/;
            ResolveCaps();

            melee.Tick(intent, _caps.CanMelee);
            ResolveCaps();

            // floor Magic / GAME_SKILL (LeftShift): channel ManaFlow, release WindMagic.
            bool magicWasBusy = magic.IsBusy;
            magic.Tick(intent, _caps.CanMagic);
            if (magic.IsBusy && !magicWasBusy)
            {
                melee.Cancel();
                crouch.ForceStand();
                // BackFlip ForcePlay would otherwise overwrite Magic_Channel*_OnAir every frame.
                jump.YieldAirAnimToAction();
            }
            ResolveCaps();

            // After melee Cancelable: behind A/D → backstep / W+behind → backflip.
            HandleMeleeBackStepable(intent);
            ResolveCaps(setAnimGate: true);

            // W + behind → backflip (cancel melee/crouch before Jump consumes press).
            if (!_caps.JumpLocked && intent.Jump
                && PlayerJump.IsMoveBehind(intent.Move, motor.Facing)
                && jump.CanBackFlip)
            {
                CancelMeleeAndCrouch();
            }

            jump.Tick(intent, _caps.JumpLocked, actionOwnsAnim: magic.IsBusy);
            ResolveCaps();

            // floor Movement State 4 (ADS_RELOAD) BackSteppable: behind A/D during reload → BackStep.
            // Start it before actionInterrupt so gun.Tick cancels the reload this frame.
            HandleReloadBackStep(intent);
            ResolveCaps(setAnimGate: true);

            // Crouch stacks with ADS (Aim BlendTree `crouching`); only slide hard-cancels aim.
            bool actionInterrupt = melee.LocksActions || backStep.IsActive || jump.OnAir
                || magic.LocksActions;
            bool allowGunFlip = _caps.FacingOwner == PlayerFacingOwner.Gun;
            // Do not (re)enter ADS while a BackStep is still playing (hard coast or soft recovery) —
            // re-raising Aim_SMG mid-BackStep fights the clip and jitters between anims.
            bool canAds = _caps.CanAds && !backStep.IsBusy;
            gun.Tick(intent, canAds, actionInterrupt, crouch.IsSliding, allowGunFlip,
                crouched: crouch.IsCrouching);

            ResolveCaps(setAnimGate: true);

            _loco.Tick(intent, _ctx, _caps);

            // Soft BackStep recovery after Movable — yield when another system took the anim.
            if (backStep.IsBusy && !backStep.IsActive
                && _caps.AnimOwner != PlayerAnimOwner.BackStep
                && !anim.IsPlaying(PlayerAnimDriver.States.BackStep))
            {
                backStep.Interrupt();
            }
        }
        private void FixedUpdate()
        {
            if (!_initialized || locked || !controlLocomotion)
            {
                return;
            }

            motor.ProbeEnvironment();
            ResolveCaps();

            var intent = inputReader.Intent;
            _ctx.Intent = intent;

            if (_caps.VelocityOwner == PlayerVelocityOwner.ImmediateOverride)
            {
                if (jump.HasVelocityOverride)
                {
                    jump.ApplyFixedVelocity();
                }
                else if (backStep.HasVelocityOverride)
                {
                    backStep.ApplyFixedVelocity();
                }
                else if (magic.HasVelocityOverride)
                {
                    magic.ApplyFixedVelocity();
                }
                else if (melee.HasVelocityOverride)
                {
                    melee.ApplyFixedVelocity();
                }
                else if (crouch.HasVelocityOverride)
                {
                    crouch.ApplyFixedVelocity();
                }

                motor.ClampFallSpeed();
            }
            else
            {
                // Crouch / crouch-ADS: ignore A/D for velocity and facing steer.
                float axis = crouch.IsCrouching ? 0f : intent.Move;
                float speed = ComputeLocomotionSpeed(axis);

                bool allowFlip = _caps.FacingOwner == PlayerFacingOwner.Locomotion
                    && _caps.CanFlip
                    && !PlayerJump.IsHoldingBackForFlip(axis, motor.Facing, intent.Jump);
                motor.UpdateFacing(axis, allowFlip);

                // floor Input Ch. / ONAIR / FaceCheck: SetVelocity x every frame (no accel ramp).
                motor.SetImmediateVelocityX(speed);
                motor.ClampFallSpeed();
            }

            anim.SetAxis(crouch.IsCrouching ? 0f : intent.Move);
        }
        /// <summary>
		/// After any ground attack Cancelable: opposite A/D → BackStep; W → BackFlip.
		/// </summary>
		private void HandleMeleeBackStepable(PlayerIntent intent)
        {
            // Cancelable → BackStepable (ActionCancelable / MovableSheath).
            if (melee.Phase != PlayerMeleePhase.ActionCancelable
                && melee.Phase != PlayerMeleePhase.MovableSheath)
            {
                return;
            }

            if (!motor.IsGrounded || jump.OnAir || backStep.IsBusy)
            {
                return;
            }

            if (!PlayerJump.IsMoveBehind(intent.Move, motor.Facing))
            {
                return;
            }

            CancelMeleeAndCrouch();

            if (intent.Jump && jump.CanBackFlip)
            {
                jump.TryStartBackFlip();
                return;
            }

            backStep.TryStart();
        }

        /// <summary>
        /// floor Movement State 4 (ADS_RELOAD): retreating (behind A/D) cancels the reload into a
        /// BackStep. Forward A/D keeps the walk-aim reload; this only fires on the behind direction.
        /// </summary>
        private void HandleReloadBackStep(PlayerIntent intent)
        {
            // Stand reload only. Any non-Standing crouch state (crouch / crouch-aim / slide /
            // stand-up) would make crouch PlayBase Crouching and FinishRelease(keepCrouch) ForcePlay
            // Crouching, fighting the BackStep clip (jitter). PlayerCrouch is the crouch authority.
            // Crouch also can't strafe (floor DontMoveWhileCrouching).
            if (!gun.IsReloading || crouch.State != PlayerCrouchState.Standing
                || !motor.IsGrounded || jump.OnAir || backStep.IsBusy)
            {
                return;
            }

            if (!PlayerJump.IsMoveBehind(intent.Move, motor.Facing))
            {
                return;
            }

            backStep.TryStart();
        }

        private void ResolveCaps(bool setAnimGate = false)
        {
            RefreshLayers();
            _caps = PlayerArbiter.Resolve(_ctx.Layers);
            _ctx.Caps = _caps;
            if (setAnimGate)
            {
                anim.SetOwnerGate(_caps.AnimOwner);
            }
        }

        private void CancelMeleeAndCrouch()
        {
            melee.Cancel();
            crouch.ForceStand();
            magic.Cancel();
        }

        private float ComputeLocomotionSpeed(float axis)
        {
            if (!_caps.CanMove)
            {
                return 0f;
            }

            if (!crouch.IsCrouching)
            {
                if (gun.IsAds)
                {
                    return axis * motor.Settings.adsWalkSpeed;
                }

                if (jump.OnAir)
                {
                    return axis * motor.Settings.airSpeed;
                }

                return axis * motor.Settings.runSpeed;
            }

            // floor DontMoveWhileCrouching / ADSCrouch: no A/D strafe (incl. crouch-ADS).
            return 0f;
        }

        private void RefreshLayers()
        {
            _ctx.Layers = new PlayerLayerSnapshot
            {
                MeleeLocksMovement = melee.LocksMovement,
                MeleeLocksActions = melee.LocksActions,
                MeleeIsAttacking = melee.IsAttacking,
                CrouchState = crouch.State,
                CrouchEnterLocked = crouch.CrouchEnterLocked,
                JumpOnAir = jump.OnAir,
                JumpLandingLocked = jump.LandingLocked,
                JumpLandToRunLocksActions = jump.LandToRunLocksActions,
                JumpIsBackFlipping = jump.IsBackFlipping,
                JumpCanBackFlip = jump.CanBackFlip,
                BackStepActive = backStep.IsActive,
                BackStepBusy = backStep.IsBusy,
                GunIsAds = gun.IsAds,
                GunIsBusy = gun.IsBusy,
                GunIsReloading = gun.IsReloading,
                MagicBusy = magic.IsBusy,
                LocoTurnLock = _loco.TurnLockActive,
                Grounded = motor.IsGrounded,
                AbsSmoothedVelocityX = Mathf.Abs(motor.SmoothedVelocityX)
            };
        }
    }
}