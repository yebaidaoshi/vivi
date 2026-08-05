using UnityEngine;



namespace Player

{

    public enum PlayerCrouchState

    {

        Standing,

        Entering,

        Crouching,

        Sliding,

        SlideToCrouch,

        StandingUp

    }




    /// <summary>

    /// 蹲下 / 滑铲 — Crouching FSM（floor.unity）的移植。

    /// 蹲姿的唯一真相来源，含蹲瞄（ADSCrouch）：枪械 ADS 期间，

    /// 本 FSM 仍在运行（Standing→Entering→Crouching），只是把 Base 动画让给枪。

    /// 其他模块必须查询 <see cref="State"/> / <see cref="IsCrouching"/> 等，

    /// 不要从原始输入再推导蹲姿 — PlayerGun 镜像本 FSM（并不持有蹲姿状态）。

    /// 枪械 ADS 时：GAME_PICKUP 通过 Animator float <c>crouching</c> 在 ADS ↔ ADSCrouch 间切换

    ///（Aim_SMG* BlendTree 选取 Crouch_Crouch_Aim_* 片段），不取消 Aim。

    /// <c>Crouch_To_Idle</c> / 起身 <c>Slide_To_Idle</c> 为软 A_to_B（随时可打断）。

    /// 水平覆盖速度在 FixedUpdate 经 <see cref="ApplyFixedVelocity"/> 应用。

    /// </summary>

    public class PlayerCrouch : MonoBehaviour

    {

        [Header("滑铲烟雾 VFX（floor Crouching State3 CreateObject → SlideEffect）")]

        [SerializeField] private GameObject slideSmokePrefab;

        [Tooltip("相对 _Heroine 根节点的偏移；floor 在所有者根节点生成（零偏移）。")]

        [SerializeField] private Vector2 slideSmokeOffset = Vector2.zero;

        [Tooltip("floor State 2 ChronosWait，每次 CreateObject 前的间隔（路径拖尾间距）。")]

        [SerializeField] private float slideSmokeInterval = 0.02f;



        private PlayerMotor _motor;

        private PlayerAnimDriver _anim;

        private PlayerAudio _audio;

        private PlayerMotorSettings _settings;



        private float _slideSmokeCooldown;



        private PlayerCrouchState _state = PlayerCrouchState.Standing;

        private float _slideTimer;

        private float _phaseTimer;

        private float _runAccum;

        private int _slideDir = 1;

        private float _overrideVx;

        private bool _hasOverrideVx;

        private bool _yieldedBaseAnimLastFrame;

        private float _rollElapsed;   // 记录翻滚已播放时间
        private float _rollStartVx;   // 记录翻滚起始水平速度
        private string _rollStateName;

        public PlayerCrouchState State => _state;

        public bool IsCrouching => _state == PlayerCrouchState.Entering

            || _state == PlayerCrouchState.Crouching

            || _state == PlayerCrouchState.Sliding

            || _state == PlayerCrouchState.SlideToCrouch;

        public bool IsSliding => _state == PlayerCrouchState.Sliding;

        /// <summary>蹲着 / 滑铲时为真 — 可打断的起身过程中不为真。</summary>

        public bool IsBusy => IsCrouching;

        public bool IsStandingUp => _state == PlayerCrouchState.StandingUp;

        public bool HasVelocityOverride => _hasOverrideVx || _state == PlayerCrouchState.SlideToCrouch;

        public void ApplyFixedVelocity()
        {
            // Roll 状态不依赖 _hasOverrideVx（它每帧被 Tick 开头清零）
            if (_hasOverrideVx || _state == PlayerCrouchState.SlideToCrouch)
            {
                _motor.SetImmediateVelocityX(_overrideVx);
            }
        }

        /// <summary>蹲下进入片段在自身 Attackable 事件之前为真

        ///（Crouch_Crouch.anim @ 0.3333s）— 在此之前近战不得打断该过渡

        ///（PlayerArbiter.CanMelee）。其他打断（松开 S、魔法、后撤/后空翻）不受影响 —

        /// 见 ForceStand / TickEntering 自身的 !intent.Crouch 检查。</summary>

        public bool CrouchEnterLocked => _state == PlayerCrouchState.Entering

            && PlayerAnimTimings.CrouchEnter.ClipLength + 0.05f - _phaseTimer

                < PlayerAnimTimings.CrouchEnter.Attackable;



        public void Init(PlayerContext context)

        {

            context.Bind(out _motor, out _anim, out _audio, out _settings);

            ResolveVfx();

        }



        private void ResolveVfx()

        {

#if UNITY_EDITOR

            // 模块在运行时组合且无序列化引用；按路径拉取预制体。

            if (slideSmokePrefab == null)

            {

                slideSmokePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(

                    "Assets/GameObject/SlideEffect.prefab");

            }

#endif

        }



        public void Tick(PlayerIntent intent, bool onAir, bool canCrouch,

            bool adsActive = false, bool gunOwnsBaseAnim = false, bool yieldBaseAnim = false)

        {

            _hasOverrideVx = false;



            if (onAir)

            {

                if (_state != PlayerCrouchState.Standing)

                {

                    ForceStand();

                }



                _runAccum = 0f;

                return;

            }



            // 近战/魔法刚释放 Base 动画（例如蹲攻在 Movable 被切断，见

            // PlayerMelee.TickMovableGroundInterrupt），而我们仍停在 Crouching，或仍在

            // Entering 中途（第二次快速攻击在蹲下进入片段播完前就切断了）：

            // 从开头（重新）播放蹲下进入片段，既避免 TickCrouching 直接

            // PlayBase(Crouching) 弹到 idle 姿势，也避免 TickEntering 卡住 —

            // 一旦 Animator 在那段窗口播的是攻击片段，其自身的 IsPlaying(Crouch)/IsPlaying(Crouching)

            // 检查就永远无法通过。

            bool yieldFallingEdge = !yieldBaseAnim && _yieldedBaseAnimLastFrame;

            _yieldedBaseAnimLastFrame = yieldBaseAnim;

            if (yieldFallingEdge && intent.Crouch

                && (_state == PlayerCrouchState.Crouching || _state == PlayerCrouchState.Entering))

            {

                EnterCrouch(adsActive);

            }



            switch (_state)

            {

                case PlayerCrouchState.Standing:

                    TickStanding(intent, canCrouch, adsActive);

                    break;

                case PlayerCrouchState.Entering:

                    TickEntering(intent, adsActive, gunOwnsBaseAnim, yieldBaseAnim);

                    break;

                case PlayerCrouchState.Crouching:

                    TickCrouching(intent, adsActive, gunOwnsBaseAnim, yieldBaseAnim);

                    break;

                case PlayerCrouchState.Sliding:

                    TickSliding(intent);

                    break;

                case PlayerCrouchState.SlideToCrouch:

                    TickSlideToCrouch(intent);

                    break;

                case PlayerCrouchState.StandingUp:

                    TickStandingUp(intent);

                    break;

            }

        }



       



        /// <summary>仅由 <see cref="TickStanding"/> / <see cref="TickStandingUp"/> 调用 —

        /// <see cref="Tick"/> 中的 FSM 分发已保证状态正确。</summary>

        private void UpdateRunAccum(PlayerIntent intent)

        {

            float moveAbs = Mathf.Abs(intent.Move);

            if (moveAbs > 0.5f && !intent.Crouch && !intent.WantsAds)

            {

                _runAccum += _motor.DeltaTime;

            }

            else if (moveAbs < 0.1f)

            {

                _runAccum = 0f;

            }

        }



        private void TickStanding(PlayerIntent intent, bool canCrouch, bool adsActive)

        {

            UpdateRunAccum(intent);



            // floor Crouching Idle：BoolTest DoCrouch everyFrame（按住，非边沿）。

            // Jump→按住 S 必须在落地时蹲下，即使按下发生在空中。

            if (!intent.Crouch || !canCrouch)

            {

                return;

            }



            float moveAbs = Mathf.Abs(intent.Move);

            if (!adsActive && _runAccum >= _settings.runTimeToSlide && moveAbs > 0.5f)

            {

                int dir = intent.Move > 0f ? 1 : intent.Move < 0f ? -1 : _motor.Facing;

                StartSlide(dir);

            }

            else

            {

                EnterCrouch(adsActive);

            }

        }



        private void TickEntering(PlayerIntent intent, bool adsActive, bool gunOwnsBaseAnim,

            bool yieldBaseAnim)

        {

            if (!intent.Crouch)

            {

                ExitCrouch(adsActive);

                return;

            }



            _phaseTimer -= _motor.DeltaTime;



            // ADS 按住：跳过 Crouch 进入片段（枪 BlendTree）。松开：让出 Base 但仍保持 Entering，

            // 以便站立 ADS 松开 → 蹲下时，枪播完后仍能播 Crouch。

            if (adsActive)

            {

                _state = PlayerCrouchState.Crouching;

                return;

            }



            if (gunOwnsBaseAnim || yieldBaseAnim)

            {

                return;

            }



            _anim.SetCrouch(true);

            if (_anim.IsPlaying(PlayerAnimDriver.States.Crouch))

            {

                _anim.SyncCurrent(PlayerAnimDriver.States.Crouch);

                if (_anim.BaseFinished || _phaseTimer <= 0f)

                {

                    _state = PlayerCrouchState.Crouching;

                    _anim.ForcePlay(PlayerAnimDriver.States.Crouching);

                }

            }

            else if (_anim.IsPlaying(PlayerAnimDriver.States.Crouching) || _phaseTimer <= 0f)

            {

                _state = PlayerCrouchState.Crouching;

                _anim.SyncCurrent(PlayerAnimDriver.States.Crouching);

            }

        }



        private void TickCrouching(PlayerIntent intent, bool adsActive, bool gunOwnsBaseAnim,

            bool yieldBaseAnim)

        {

            if (!intent.Crouch)

            {

                // 仅 adsActive（非松开）：枪持有蹲↔站瞄准；松开则让给 BeginStandUp。

                ExitCrouch(adsActive);

                return;

            }



            // ADS / 松开收枪 / 近战：不要用 PlayBase Crouching 盖住它们的片段。

            if (adsActive || gunOwnsBaseAnim || yieldBaseAnim)

            {

                return;

            }



            _anim.SetCrouch(true);

            if (!_anim.IsPlaying(PlayerAnimDriver.States.Crouching)

                && !_anim.IsPlaying(PlayerAnimDriver.States.Crouch))

            {

                _anim.PlayBase(PlayerAnimDriver.States.Crouching);

            }

        }



        /// <summary>

        /// floor ADSCrouch → ADS（松开 GAME_PICKUP）：离开蹲姿状态；枪播放

        /// Crouch_Crouch_Aim_to_Stand_Aim，然后清除 crouching。

        /// 无 ADS 时：正常 Crouch_To_Idle 起身。

        /// </summary>

        private void ExitCrouch(bool adsActive)

        {

            if (adsActive)

            {

                _state = PlayerCrouchState.Standing;

                _phaseTimer = 0f;

                return;

            }



            BeginStandUp();

        }



        private void TickSliding(PlayerIntent intent)

        {
            if (Mathf.Abs(intent.Move) > 0.1f)
            {
                int desired = intent.Move > 0f ? 1 : -1;
                if (desired != _slideDir)
                {
                    // 忽略反向输入，不处理
                }
            }
            



            _slideTimer -= _motor.DeltaTime;

            float fade = Mathf.Clamp01(_slideTimer / _settings.slideDuration);

            SetOverride(_slideDir * _settings.slideForce * Mathf.Lerp(0.35f, 1f, fade));



            // floor Crouching State2→State3：每 ChronosWait(0.02) CreateObject(SlideEffect)

            // 在所有者世界坐标（不挂父）— 沿滑铲路径留下烟雾拖尾。

            TickSlideSmokeTrail();



            if (!_anim.IsPlaying(PlayerAnimDriver.States.Slide)

                && !_anim.IsPlaying(PlayerAnimDriver.States.Sliding))

            {

                _anim.PlayBase(PlayerAnimDriver.States.Sliding);

            }

            else if (_anim.IsPlaying(PlayerAnimDriver.States.Sliding))

            {

                _anim.SyncCurrent(PlayerAnimDriver.States.Sliding);

            }



            if (_slideTimer > 0f)

            {

                return;

            }



            if (intent.Crouch)

            {

                EnterSlideToCrouch();

            }

            else

            {

                BeginStandUpFromSlide();

            }

        }



        private void TickSlideToCrouch(PlayerIntent intent)
        {
            _hasOverrideVx = true;

            _rollElapsed += _motor.DeltaTime;
            _phaseTimer -= _motor.DeltaTime;

            if (_anim.IsPlaying(PlayerAnimDriver.States.SlideToIdle))
            {
                _anim.SyncCurrent(PlayerAnimDriver.States.SlideToIdle);
                _anim.SetCrouchingWeight(1f);
            }

            if (!intent.Crouch)
            {
                BeginStandUpFromSlide();
                return;
            }

            if (intent.WantsAds)
            {
                _state = PlayerCrouchState.Crouching;
                _anim.SetCrouch(true);
                return;
            }

            int facing = transform.localScale.x >= 0f ? 1 : -1;

            // 速度策略：Movable 前保持速度，之后归零
            if (_rollElapsed < PlayerAnimTimings.Roll.Movable)
            {
                _overrideVx = facing * 28f;
            }
            else
            {
                _overrideVx = 0f;
            }

            _motor.SetImmediateVelocityX(_overrideVx);
            // 直接移动位置（绕过物理，确保翻滚位移）
            transform.localPosition += new Vector3(_overrideVx * Time.deltaTime, 0, 0);

            // 阶段门控
            if (_rollElapsed < PlayerAnimTimings.Roll.Cancelable)
            {
                // 硬直期
            }
            else if (_rollElapsed < PlayerAnimTimings.Roll.Movable)
            {
                if (intent.JumpPressed || intent.SlashPressed
                    || intent.EvadePressed || intent.ReloadPressed)
                {
                    ForceStand();
                    return;
                }
            }
            else
            {
                if (intent.JumpPressed || intent.SlashPressed
                    || intent.EvadePressed || intent.ReloadPressed)
                {
                    ForceStand();
                    return;
                }
            }

            if (_phaseTimer <= 0f)
            {
                EnterCrouchingOnRollEnd();
            }
        }
        private void EnterCrouchingOnRollEnd()
        {
            _state = PlayerCrouchState.Crouching;
            _anim.SetCrouch(true);
            _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
            _hasOverrideVx = false;
        }


        private void TickStandingUp(PlayerIntent intent)

        {

            UpdateRunAccum(intent);



            // 软 A_to_B：任何动作立即切断；否则由 loco 软保持 Crouch_To_Idle。

            if (intent.WantsSoftActionInterrupt)

            {

                ForceStand();

                return;

            }



            _phaseTimer -= _motor.DeltaTime;

            bool onStandClip = _anim.IsPlaying(PlayerAnimDriver.States.CrouchToIdle)

                || _anim.IsPlaying(PlayerAnimDriver.States.SlideToIdle);

            if (!onStandClip || _anim.BaseFinished || _phaseTimer <= 0f)

            {

                ForceStand();

            }

        }



        private void SetOverride(float vx)

        {

            _overrideVx = vx;

            _hasOverrideVx = true;

        }



        private void EnterCrouch(bool adsActive = false)

        {

            _state = PlayerCrouchState.Entering;

            _phaseTimer = PlayerAnimTimings.CrouchEnter.ClipLength + 0.05f;

            _runAccum = 0f;

            // ADS 期间 PlayerGun 播放 Aim_Aim_SMG_Hold_to_Crouch_Aim — 不要突然改 crouching。

            // 站立 ADS 松开：仍播 Crouch（枪在同帧因 intent.Crouch 让出）。

            if (adsActive)

            {

                return;

            }



            _anim.SetCrouch(true);

            _anim.ForcePlay(PlayerAnimDriver.States.Crouch);

        }



        private void StartSlide(int dir)

        {

            _state = PlayerCrouchState.Sliding;

            _slideDir = dir >= 0 ? 1 : -1;

            _slideTimer = _settings.slideDuration;

            _runAccum = 0f;

            _motor.ForceFacing(_slideDir);

            SetOverride(_slideDir * _settings.slideForce);

            _motor.SetImmediateVelocityX(_overrideVx);

            _motor.AddForce(new Vector2(_slideDir * 10f, -5f));

            _anim.SetCrouch(true);

            _anim.ForcePlay(PlayerAnimDriver.States.Slide);

            _audio?.PlaySlide();

            // floor：Sliding → State3 CreateObject(SlideEffect) 在所有者处，然后 State2 等待

            // 0.02s 并循环 State3 — 沿路径的未挂父烟雾团（不挂到女主上）。

            SpawnSlideSmokePuff();

            _slideSmokeCooldown = slideSmokeInterval;

        }



        /// <summary>

        /// floor State2 ChronosWait(0.02) → State3 CreateObject。每个烟雾团未挂父，

        /// 经 destroyMe（deathtimer=2）自销毁。

        /// </summary>

        private void TickSlideSmokeTrail()

        {

            if (slideSmokePrefab == null)

            {

                return;

            }



            float dt = _motor != null ? _motor.DeltaTime : Time.deltaTime;

            _slideSmokeCooldown -= dt;

            if (_slideSmokeCooldown > 0f)

            {

                return;

            }



            _slideSmokeCooldown = slideSmokeInterval;

            SpawnSlideSmokePuff();

        }



        private void SpawnSlideSmokePuff()

        {

            if (slideSmokePrefab == null)

            {

                return;

            }



            int sign = _slideDir >= 0 ? 1 : -1;

            var fx = Instantiate(slideSmokePrefab);

            // 路径拖尾：当前女主世界坐标；保留预制体自带的 −90° 粒子旋转。

            fx.transform.position = transform.position

                + new Vector3(slideSmokeOffset.x * sign, slideSmokeOffset.y, 0f);

        }



        private void StopSlideSmokeTrail()

        {

            // 留下已有烟雾团在地上；只停止生成新的。

            _slideSmokeCooldown = 0f;

        }



        private void EnterSlideToCrouch()
        {
            StopSlideSmokeTrail();
            _state = PlayerCrouchState.SlideToCrouch;
            _rollElapsed = 0f;
            _phaseTimer = PlayerAnimTimings.Roll.ClipLength + 0.05f;

            _overrideVx = _motor.Facing * 8f;

            _motor.SetImmediateVelocityX(_overrideVx);

            _anim.ForcePlay(PlayerAnimDriver.States.SlideToIdle);
            _anim.SetCrouchingWeight(1f);
            _anim.SetCrouch(true);
        }


        private void BeginStandUp()

        {

            _state = PlayerCrouchState.StandingUp;

            _phaseTimer = PlayerAnimTimings.CrouchToIdle.ClipLength + 0.05f;

            _anim.SetCrouch(false);

            _anim.ForcePlay(PlayerAnimDriver.States.CrouchToIdle);

        }



        private void BeginStandUpFromSlide()

        {

            StopSlideSmokeTrail();

            _state = PlayerCrouchState.StandingUp;

            _phaseTimer = PlayerAnimTimings.SlideToIdle.ClipLength + 0.05f;

            _anim.SetCrouch(false);

            _anim.ForcePlay(PlayerAnimDriver.States.SlideToIdle);

        }



        private void CancelSlideToRun(int facing)

        {

            StopSlideSmokeTrail();

            _state = PlayerCrouchState.Standing;

            _slideTimer = 0f;

            _phaseTimer = 0f;

            _runAccum = 0f;

            _anim.SetCrouch(false);

            _motor.ForceFacing(facing);

            SetOverride(facing * _settings.runSpeed);

            _motor.SetImmediateVelocityX(_overrideVx);

            _anim.ForcePlay(PlayerAnimDriver.States.Run);

        }



        public void ForceStand()

        {

            StopSlideSmokeTrail();

            _state = PlayerCrouchState.Standing;

            _slideTimer = 0f;

            _phaseTimer = 0f;

            _runAccum = 0f;

            _hasOverrideVx = false;

            _anim.SetCrouch(false);

        }



        private void OnDisable()

        {

            // 玩家在滑铲中途被销毁时，切勿留下循环烟雾拖尾成孤儿。

            StopSlideSmokeTrail();

        }

    }

}
