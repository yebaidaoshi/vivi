using UnityEngine;

namespace Player
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : MonoBehaviour
    {
        [Header("模块")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerJump jump;
        [SerializeField] private PlayerCrouch crouch;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerGun gun;
        [SerializeField] private PlayerBackStep backStep;
        [SerializeField] private PlayerMagic magic;
        [SerializeField] private PlayerAnimDriver anim;
        [SerializeField] private PlayerAudio audioPlayer;
        [SerializeField] private PlayerHealth health;

        [Header("选项")]
        [SerializeField] private bool controlLocomotion = true;
        [SerializeField] private bool locked;

        private bool _initialized = false;
        private PlayerContext _ctx;
        private PlayerLocomotion _loco;
        private PlayerCapabilities _caps;

        public PlayerIntent Intent => inputReader != null ? inputReader.Intent : default;
        public PlayerMotor Motor => motor;
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
            WireModules();
            _initialized = true;
        }

        private void EnsureModules()
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
            health = health ?? GetComponent<PlayerHealth>() ?? gameObject.AddComponent<PlayerHealth>();
        }

        private PlayerAudio ResolveAudioModule()
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
            if (onAudio != null) return onAudio;

            var onRoot = GetComponent<PlayerAudio>();
            if (onRoot != null) return onRoot;

            return audioRoot.gameObject.AddComponent<PlayerAudio>();
        }

        public void SendEvent(string eventName)
        {
            audioPlayer?.SendEvent(eventName);
        }

        private void WireModules()
        {
            _ctx.Motor = motor;
            _ctx.Anim = anim;
            _ctx.Audio = audioPlayer;
            _ctx.Settings = motor.Settings;
            _ctx.NotifyJumpAttack = jump.NotifyJumpAttack;

            jump.Init(_ctx);
            crouch.Init(_ctx);
            melee.Init(_ctx);
            gun.Init(_ctx);
            backStep.Init(_ctx);
            magic.Init(_ctx);
            health.Init(_ctx);
            health.SetController(this);
        }

        private void Update()
        {
            health.Tick();

            if (!_initialized || locked)
                return;

            // ★ 核心：硬直/受击动画/Idle_Damage_A 期间跳过所有逻辑，只更新意图
            if (health.IsInHitStun || health.IsHitAnimationPlaying || health.IsInIdleDamage)
            {
                _ctx.Intent = inputReader.Intent;
                return;
            }

            var intent = inputReader.Intent;
            _ctx.Intent = intent;

            backStep.Tick();
            _loco.TickTimers(motor.DeltaTime);

            ResolveCaps(setAnimGate: true);

            crouch.Tick(intent, jump.OnAir, _caps.CanCrouch,
                adsActive: gun.IsAds, gunOwnsBaseAnim: gun.OwnsCrouchBaseAnim,
                yieldBaseAnim: melee.IsAttacking || magic.IsBusy);
            if (crouch.State == PlayerCrouchState.SlideToCrouch && melee.IsAttacking)
            {
                melee.Cancel();
            }
            ResolveCaps();

            jump.Tick(intent, _caps.JumpLocked, actionOwnsAnim: magic.IsBusy);
            ResolveCaps();

            melee.Tick(intent, _caps.CanMelee);
            ResolveCaps();

            bool magicWasBusy = magic.IsBusy;
            magic.Tick(intent, _caps.CanMagic);
            if (magic.IsBusy && !magicWasBusy)
            {
                melee.Cancel();
                crouch.ForceStand();
                jump.YieldAirAnimToAction();
            }
            ResolveCaps();

            HandleMeleeBackStepable(intent);
            ResolveCaps(setAnimGate: true);

            if (!_caps.JumpLocked && intent.Jump
                && PlayerJump.IsMoveBehind(intent.Move, motor.Facing)
                && jump.CanBackFlip)
            {
                CancelMeleeAndCrouch();
            }

            // ★ 修改：注释掉换弹时触发后撤步的调用
            // HandleReloadBackStep(intent);
            ResolveCaps(setAnimGate: true);

            bool actionInterrupt = melee.LocksActions || backStep.IsActive || jump.OnAir
                || magic.LocksActions;
            bool allowGunFlip = _caps.FacingOwner == PlayerFacingOwner.Gun;
            bool canAds = _caps.CanAds && !backStep.IsBusy;
            gun.Tick(intent, canAds, actionInterrupt, crouch.IsSliding, allowGunFlip,
                crouched: crouch.IsCrouching);
            ResolveCaps(setAnimGate: true);

            _loco.Tick(intent, _ctx, _caps);

            if (backStep.IsBusy && !backStep.IsActive
                && _caps.AnimOwner != PlayerAnimOwner.BackStep
                && !anim.IsPlaying(PlayerAnimDriver.States.BackStep))
            {
                backStep.Interrupt();
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized || locked || !controlLocomotion) return;

            motor.ProbeEnvironment();
            ResolveCaps();

            var intent = inputReader.Intent;
            _ctx.Intent = intent;

            if (crouch.State == PlayerCrouchState.SlideToCrouch)
            {
                crouch.ApplyFixedVelocity();
                motor.ClampFallSpeed();
                anim.SetAxis(0f);
                return;
            }

            if (_caps.VelocityOwner == PlayerVelocityOwner.ImmediateOverride)
            {
                if (jump.HasVelocityOverride) jump.ApplyFixedVelocity();
                else if (backStep.HasVelocityOverride) backStep.ApplyFixedVelocity();
                else if (magic.HasVelocityOverride) magic.ApplyFixedVelocity();
                else if (crouch.HasVelocityOverride) crouch.ApplyFixedVelocity();
                else if (melee.HasVelocityOverride) melee.ApplyFixedVelocity();
                motor.ClampFallSpeed();
            }
            else
            {
                float axis = crouch.IsCrouching ? 0f : intent.Move;
                float speed = ComputeLocomotionSpeed(axis);

                bool allowFlip = _caps.FacingOwner == PlayerFacingOwner.Locomotion
                    && _caps.CanFlip
                    && !PlayerJump.IsHoldingBackForFlip(axis, motor.Facing, intent.Jump);
                motor.UpdateFacing(axis, allowFlip);

                motor.SetImmediateVelocityX(speed);
                motor.ClampFallSpeed();
            }

            anim.SetAxis(crouch.IsCrouching ? 0f : intent.Move);
        }

        private void HandleMeleeBackStepable(PlayerIntent intent)
        {
            if (melee.Phase != PlayerMeleePhase.ActionCancelable
                && melee.Phase != PlayerMeleePhase.MovableSheath) return;

            if (!motor.IsGrounded || jump.OnAir || backStep.IsBusy) return;

            if (!PlayerJump.IsMoveBehind(intent.Move, motor.Facing)) return;

            CancelMeleeAndCrouch();

            if (intent.Jump && jump.CanBackFlip)
            {
                jump.TryStartBackFlip();
                return;
            }

            backStep.TryStart();
        }

        // 此方法已不再被调用（已注释），保留以防后续可能使用
        private void HandleReloadBackStep(PlayerIntent intent)
        {
            if (!gun.IsReloading || crouch.State != PlayerCrouchState.Standing
                || !motor.IsGrounded || jump.OnAir || backStep.IsBusy) return;

            if (!PlayerJump.IsMoveBehind(intent.Move, motor.Facing)) return;

            backStep.TryStart();
        }

        private void ResolveCaps(bool setAnimGate = false)
        {
            RefreshLayers();
            _caps = PlayerArbiter.Resolve(_ctx.Layers);
            _ctx.Caps = _caps;
            if (setAnimGate) anim.SetOwnerGate(_caps.AnimOwner);
        }

        private void CancelMeleeAndCrouch()
        {
            melee.Cancel();
            crouch.ForceStand();
            magic.Cancel();
        }

        private float ComputeLocomotionSpeed(float axis)
        {
            if (!_caps.CanMove) return 0f;

            if (!crouch.IsCrouching)
            {
                if (gun.IsAds) return axis * motor.Settings.adsWalkSpeed;
                if (jump.OnAir) return axis * motor.Settings.airSpeed;
                return axis * motor.Settings.runSpeed;
            }

            return 0f;
        }

        public void ResetCombo()
        {
            if (melee != null) melee.Cancel();
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