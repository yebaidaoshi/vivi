using UnityEngine;

namespace Player
{
    [DefaultExecutionOrder(-100)]//数值越小，脚本越早执行
    [RequireComponent(typeof(Rigidbody2D))]//确保游戏对象上有 Rigidbody2D 组件，如果没有则自动添加
    [RequireComponent(typeof(Animator))]//确保游戏对象上有 Animator 组件，如果没有则自动添加
    public class PlayerController : MonoBehaviour
    {
        [Header("模块")]
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
        [SerializeField] private PlayerHealth health;


        [Header("选项")]
        [SerializeField] private bool controlLocomotion = true;//是否控制移动
        [SerializeField] private bool locked;//是否锁定玩家控制

        private bool _initialized = false;
        private PlayerContext _ctx;
        private PlayerLocomotion _loco;
        private PlayerCapabilities _caps;

        public PlayerIntent Intent => inputReader != null ? inputReader.Intent : default;//判断 inputReader 是否不等于 null，不等于则返回 inputReader.Intent，否则返回 default（默认值）
        public PlayerMotor Motor => motor;//封装的好处：外部可读取但不能修改
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
            health = health ?? GetComponent<PlayerHealth>() ?? gameObject.AddComponent<PlayerHealth>();
        }
        //??（空合并运算符）逻辑：如果左边不是 null，就用左边；如果左边是 null，就用右边
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

            // 旧版：原先挂在 Heroine 根节点上——若仍存在则迁移复用。
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
        }//? 表示若 audioPlayer 不等于 null 则调用 SendEvent，否则不做任何操作

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
            health.Init(_ctx);        
            health.SetController(this);

        }
        private void Update()
        {
            health.Tick();

            if (!_initialized || locked)
            {
                health.Tick();
                return;
            }
            var intent = inputReader.Intent;
            _ctx.Intent = intent;

            backStep.Tick();
            _loco.TickTimers(motor.DeltaTime);

            ResolveCaps(setAnimGate: true);

            // IsAds：蹲↔站瞄准 BlendTree。OwnsCrouchBaseAnim：也覆盖 Aim_SMG_Release，
            // 避免蹲下时用 PlayBase Crouching 盖过蹲下-ADS 收枪动画（产生抖动）。
            // yieldBaseAnim：近战占用挥砍动画时，蹲下不得覆盖 Attack*。
            crouch.Tick(intent/*这一帧的输入*/, jump.OnAir/*当前是否在空中*/, _caps.CanCrouch/*能蹲吗*/,//传入玩家意图、是否在空中、是否可以蹲下，便于蹲下脚本处理具体逻辑
                adsActive: gun.IsAds/*枪械正在瞄准吗*/, gunOwnsBaseAnim: gun.OwnsCrouchBaseAnim/*枪械占用了基础动画层吗*/,
                yieldBaseAnim: melee.IsAttacking || magic.IsBusy)/*近战或魔法正在播放动画吗*/;
            if (crouch.State == PlayerCrouchState.SlideToCrouch && melee.IsAttacking)
            {
                melee.Cancel();
            }
            ResolveCaps();

            melee.Tick(intent, _caps.CanMelee);
            ResolveCaps();

            // 地面 Magic / GAME_SKILL（LeftShift）：蓄力 ManaFlow，释放 WindMagic。
            bool magicWasBusy = magic.IsBusy;
            magic.Tick(intent, _caps.CanMagic);
            if (magic.IsBusy && !magicWasBusy)
            {
                melee.Cancel();
                crouch.ForceStand();
                // 否则 BackFlip 的 ForcePlay 会每帧覆盖 Magic_Channel*_OnAir。
                jump.YieldAirAnimToAction();
            }
            ResolveCaps();

            // 近战进入 Cancelable 之后：背后 A/D → 后撤步 / W+背后 → 后空翻。
            HandleMeleeBackStepable(intent);
            ResolveCaps(setAnimGate: true);

            // W + 背后 → 后空翻（在 Jump 消耗按键前取消近战/蹲下）。
            if (!_caps.JumpLocked && intent.Jump
                && PlayerJump.IsMoveBehind(intent.Move, motor.Facing)
                && jump.CanBackFlip)
            {
                CancelMeleeAndCrouch();
            }

            jump.Tick(intent, _caps.JumpLocked, actionOwnsAnim: magic.IsBusy);
            ResolveCaps();

            // 地面 Movement State 4（ADS_RELOAD）BackSteppable：换弹时背后 A/D → BackStep。
            // 须在 actionInterrupt 之前启动，以便本帧 gun.Tick 取消换弹。
            HandleReloadBackStep(intent);
            ResolveCaps(setAnimGate: true);

            // 蹲下可与 ADS 叠加（Aim BlendTree 的 `crouching`）；仅滑铲会硬取消瞄准。
            bool actionInterrupt = melee.LocksActions || backStep.IsActive || jump.OnAir
                || magic.LocksActions;
            bool allowGunFlip = _caps.FacingOwner == PlayerFacingOwner.Gun;
            // BackStep 仍在播放时（硬滑行或软恢复）不要（重新）进入 ADS——
            // 在 BackStep 中途再抬起 Aim_SMG 会与片段冲突，导致动画抖动。
            bool canAds = _caps.CanAds && !backStep.IsBusy;
            gun.Tick(intent, canAds, actionInterrupt, crouch.IsSliding, allowGunFlip,
                crouched: crouch.IsCrouching);

            ResolveCaps(setAnimGate: true);

            _loco.Tick(intent, _ctx, _caps);

            // Movable 之后的软 BackStep 恢复——若其他系统已接管动画则让出。
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
            if (crouch.State == PlayerCrouchState.SlideToCrouch)
            {
                crouch.ApplyFixedVelocity();
                motor.ClampFallSpeed();
                anim.SetAxis(0f);
                return;  // 跳过后续所有逻辑
            }

            if (_caps.VelocityOwner == PlayerVelocityOwner.ImmediateOverride)
            {
                if 
                    (jump.HasVelocityOverride) jump.ApplyFixedVelocity();

                else if 
                    (backStep.HasVelocityOverride) backStep.ApplyFixedVelocity();

                else if 
                    (magic.HasVelocityOverride) magic.ApplyFixedVelocity();

                else if 
                    (crouch.HasVelocityOverride) crouch.ApplyFixedVelocity();  
                                                                                   
                else if 
                    (melee.HasVelocityOverride) melee.ApplyFixedVelocity();

                motor.ClampFallSpeed();
            }
            else
            {
                // 蹲下 / 蹲下-ADS：忽略 A/D，不参与速度与朝向转向。
                float axis = crouch.IsCrouching ? 0f : intent.Move;
                float speed = ComputeLocomotionSpeed(axis);
                

                bool allowFlip = _caps.FacingOwner == PlayerFacingOwner.Locomotion
                    && _caps.CanFlip
                    && !PlayerJump.IsHoldingBackForFlip(axis, motor.Facing, intent.Jump);
                motor.UpdateFacing(axis, allowFlip);

                // 地面 Input Ch. / ONAIR / FaceCheck：每帧直接 SetVelocity x（无加速斜坡）。
                motor.SetImmediateVelocityX(speed);
                motor.ClampFallSpeed();
            }

            anim.SetAxis(crouch.IsCrouching ? 0f : intent.Move);
        }
        /// <summary>
		/// 任意地面攻击进入 Cancelable 之后：反向 A/D → BackStep；W → BackFlip。
		/// </summary>
		private void HandleMeleeBackStepable(PlayerIntent intent)
        {
            // Cancelable → BackStepable（ActionCancelable / MovableSheath）。
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
        /// 地面 Movement State 4（ADS_RELOAD）：后退（背后 A/D）取消换弹并转入 BackStep。
        /// 向前 A/D 仍保持走射换弹；仅在背后方向触发。
        /// </summary>
        private void HandleReloadBackStep(PlayerIntent intent)
        {
            // 仅站立换弹。任何非 Standing 的蹲下状态（蹲 / 蹲瞄 / 滑铲 /
            // 起身）都会让 crouch 的 PlayBase Crouching 与 FinishRelease(keepCrouch) 的 ForcePlay
            // Crouching 与 BackStep 片段冲突（抖动）。蹲下权威在 PlayerCrouch。
            // 蹲下也无法平移（地面 DontMoveWhileCrouching）。
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

            // 地面 DontMoveWhileCrouching / ADSCrouch：无 A/D 平移（含蹲下-ADS）。
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
