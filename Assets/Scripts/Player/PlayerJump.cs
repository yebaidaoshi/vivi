using UnityEngine;

namespace Player
{
    public enum PlayerAirState
    {
        Grounded,
        Rising,
        Falling,
        Landing,
        BackFlip,
        BackFlipLand
    }

    /// <summary>
    /// 跳跃 / 下落 / 落地 / 后空翻 — Jump FSM 的移植。
    /// Idle Landing：软保持，可打断。Landing_to_Run：硬锁定直到首次 SE_Run，
    /// 然后让出；落地时朝向翻转到移动方向（Movement LRAnim）。
    /// 规则：左右仅驱动 airfloat 倾侧。
    /// </summary>
    public class PlayerJump : MonoBehaviour
    {
        [Header("跳跃烟雾 VFX（floor Movement Jump CreateObject → JumpEffect）")]
        [SerializeField] private GameObject jumpSmokePrefab;
        [Tooltip("相对 _Heroine 根节点的偏移；x 随朝向镜像。")]
        [SerializeField] private Vector2 jumpSmokeOffset = Vector2.zero;
        [SerializeField] private float jumpSmokeLifetime = 1.2f;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private PlayerAirState _state = PlayerAirState.Grounded;
        private float _jumpBuffer;
        private float _landLock;
        private float _backFlipAir;
        private float _takeoffLock;
        private bool _jumpConsumed;
        private string _landingState;
        private bool _landToRun;
        private float _landToRunElapsed;

        private int _backFlipTakeoffDir;
        private float _prevMove;
        public PlayerAirState State => _state;
        public bool OnAir => _state == PlayerAirState.Rising || _state == PlayerAirState.Falling
            || _state == PlayerAirState.BackFlip;
        /// <summary>软 Idle / 后空翻落地保持 — 可打断。</summary>
        public bool LandingLocked => !_landToRun
            && (_state == PlayerAirState.Landing || _state == PlayerAirState.BackFlipLand);
        /// <summary>Landing_to_Run 在首次 SE_Run 之前 — 硬锁定动作。</summary>
        public bool LandToRunLocksActions => _landToRun
            && _landToRunElapsed < PlayerAnimTimings.LandingToRun.SeRun;
        /// <summary>仅空中后空翻（落地为可打断的软保持）。</summary>
        public bool IsBackFlipping => _state == PlayerAirState.BackFlip;
        /// <summary>着地时；W + 身后 A/D → Jump State 7。</summary>
        public bool CanBackFlip => _motor != null && _motor.IsGrounded
            && !OnAir
            && !LandToRunLocksActions;
        /// <summary>后空翻期间阻塞 loco 加速；速度为一次性写入（不按帧重写）。</summary>
        public bool HasVelocityOverride => IsBackFlipping;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolveVfx();
        }

        private void ResolveVfx()
        {
#if UNITY_EDITOR
			// 模块在运行时组合且无序列化引用；按路径拉取预制体。
			if (jumpSmokePrefab == null)
			{
				jumpSmokePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
					"Assets/GameObject/JumpEffect.prefab");
			}
#endif
        }

        public void Tick(PlayerIntent intent, bool movementLocked, bool actionOwnsAnim = false)
        {

            float dt = _motor.DeltaTime;

            if (_landLock > 0f)
            {
                _landLock -= dt;
            }

            if (_takeoffLock > 0f)
            {
                _takeoffLock -= dt;
            }

            if (intent.JumpPressed)
            {
                _jumpBuffer = _settings.jumpBuffer;
            }
            else
            {
                _jumpBuffer -= dt;
            }

            bool grounded = _motor.IsGrounded;
            bool suppressAnim = actionOwnsAnim;
            _prevMove = intent.Move;
            if (_state == PlayerAirState.BackFlip)
            {
                // 后空翻期间 JumpLocked 始终为真 — 用 actionOwnsAnim，不用 movementLocked。
                TickBackFlip(grounded, suppressAnim);
                return;
            }

            if (_landToRun)
            {
                TickLandToRun();
                if (_landToRun)
                {
                    return;
                }
                // 已到达 SE_Run — 动作已解锁，继续往下走。
            }


            if (_state == PlayerAirState.BackFlipLand)
            {
                TickBackFlipLandHold(intent, suppressAnim);
                if (_state != PlayerAirState.Grounded)
                    return;   // 仍在落地中，或已衔接新后空翻，本帧结束
            }
            else if (LandingLocked)
            {
                // 其他落地（普通 Landing）保持原有逻辑
                if (intent.WantsSoftActionInterrupt || actionOwnsAnim)
                    FinishLanding();
                else
                {
                    TickLandHold(_landingState);
                    return;
                }
            }

            if (LandingLocked)
            {
                if (intent.WantsSoftActionInterrupt || actionOwnsAnim)
                {
                    FinishLanding();
                    // 继续往下走，使跳跃 / 着地逻辑可在同一帧运行。
                }
                else
                {
                    TickLandHold(_landingState);
                    return;
                }
            }

            if (OnAir)
            {
                // JumpLocked（例如 MagicBusy）：保持空中状态 / 落地清理，但不抢动画。
                TickAir(intent, grounded, suppressAnim: movementLocked || suppressAnim);
                return;
            }

            // 走出平台边缘（等价于 Fall 事件）。
            if (!grounded && _state == PlayerAirState.Grounded && !_motor.CanJump)
            {
                EnterFall();
                return;
            }

            if (movementLocked)
            {
                return;
            }

            // W + 身后 A/D → BackFlip；普通 W → Jump。按住 W 时每次着地都会起跳
            //（idle 跳→落→跳 循环）；_jumpConsumed 限制每次着地只起跳一次。
            // 缓冲仍可接住落地前刚按下的轻点。
            if (_motor.CanJump && !_jumpConsumed)
            {
                bool behind = IsMoveBehind(intent.Move);
                if (behind && intent.Jump && CanBackFlip)
                {
                    TryStartBackFlip();
                }
                else if ((_jumpBuffer > 0f || intent.Jump) && !behind)
                {
                    DoJump(intent.Move);
                }
            }

            if (!_motor.CanJump)
            {
                _jumpConsumed = false;
            }
        }

        /// <summary>A/D 朝向角色背后（与朝向相反）。</summary>
        public static bool IsMoveBehind(float move, int facing)
        {
            return Mathf.Abs(move) > 0.5f && move * facing < 0f;
        }

        /// <summary>W + 身后 A/D — 不翻转朝向（后空翻起跳）。</summary>
        public static bool IsHoldingBackForFlip(float move, int facing, bool jumpHeld)
        {
            return jumpHeld && IsMoveBehind(move, facing);
        }

        private bool IsMoveBehind(float move) => IsMoveBehind(move, _motor.Facing);

        /// <summary>硬锁定直到首次 SE_Run，然后切 Grounded，由 loco 软续播该片段。</summary>
        private void TickLandToRun()
        {
            _landToRunElapsed += _motor.DeltaTime;
            if (!string.IsNullOrEmpty(_landingState) && _anim.IsPlaying(_landingState))
            {
                _anim.SyncCurrent(_landingState);
            }
            else if (!string.IsNullOrEmpty(_landingState))
            {
                _anim.ForcePlay(_landingState);
            }

            if (_landToRunElapsed >= PlayerAnimTimings.LandingToRun.SeRun)
            {
                // 解锁动作；把动画留给 locomotion 软保持继续播。
                _landToRun = false;
                _state = PlayerAirState.Grounded;
                _landLock = 0f;
                _landingState = null;
            }
        }

        /// <summary>保持落地片段直到 BaseFinished；landLock 仅作安全超时。</summary>
        private void TickLandHold(string expectedState)
        {
            bool onClip = !string.IsNullOrEmpty(expectedState) && _anim.IsPlaying(expectedState);
            if (!onClip && _state == PlayerAirState.Landing)
            {
                onClip = _anim.IsPlaying(PlayerAnimDriver.States.Landing);
            }

            if (onClip)
            {
                if (!string.IsNullOrEmpty(expectedState))
                {
                    _anim.SyncCurrent(expectedState);
                }

                // 优先以片段结束为准；若 BaseFinished 永不触发，landLock 作安全超时。
                if (_anim.BaseFinished || _landLock <= 0f)
                {
                    FinishLanding();
                }

                return;
            }

            // 丢失落地状态（被打断）或超时。
            if (_landLock <= 0f)
            {
                FinishLanding();
            }
        }

        private void FinishLanding()
        {
            _state = PlayerAirState.Grounded;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            // BackFlipLand 曾在着地时把此标志卡在 true（CanJump 永远清不掉）。
            _jumpConsumed = false;
        }

        private void TickAir(PlayerIntent intent, bool grounded, bool suppressAnim)
        {
            float vy = _motor.GetVelocity().y;

            // floor Jump [Jump] 状态：仅在起跳 ChronosWait 窗口内，身后输入 → BackFlip。
            // 严格限定起跳窗口（非整段上升），避免空中误触发。
            if (!suppressAnim && _takeoffLock > 0f && IsMoveBehind(intent.Move) && !IsJumpAttackAnim())
            {
                ConvertRisingToBackFlip();
                TickBackFlip(grounded, suppressAnim: false);
                return;
            }

            if (vy > 0.5f)
            {
                _state = PlayerAirState.Rising;
            }
            else if (vy < -0.1f)
            {
                _state = PlayerAirState.Falling;
            }

            if (!suppressAnim)//若未被禁止播放动画，则执行下面的代码
            {
                UpdateAirFloat(intent.Move);

                if (!IsJumpAttackAnim())
                {
                    EnsureAirAnim();
                }
            }

            if (grounded && vy <= 0.5f && _takeoffLock <= 0f)
            {
                if (suppressAnim)
                {
                    // Magic（等）已持有 Base Layer — 清空空中状态，不播 Landing*。
                    ClearAirOnOwnedLand();
                }
                else
                {
                    Land(intent);
                }
            }
        }

        /// <summary>落地时另一系统持有动画（魔法吟唱）：退出空中，不播落地片段。</summary>
        private void ClearAirOnOwnedLand()
        {
            _jumpConsumed = false;
            _takeoffLock = 0f;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            _state = PlayerAirState.Grounded;
            _anim.SetAirFloat(0f, 0f);
        }

        private void TickBackFlip(bool grounded, bool suppressAnim)
        {
            _backFlipAir -= _motor.DeltaTime;
            if (!suppressAnim)
            {
                UpdateAirFloat(0f);
            }

            float vy = _motor.GetVelocity().y;
            if (grounded && _backFlipAir <= 0f && vy <= 0.5f)
            {
                if (suppressAnim)
                {
                    ClearAirOnOwnedLand();
                }
                else
                {
                    EnterBackFlipLand();
                }
            }
            else if (!suppressAnim)
            {
                if (!_anim.IsPlaying(PlayerAnimDriver.States.BackFlip))
                {
                    _anim.ForcePlay(PlayerAnimDriver.States.BackFlip);
                }
                else
                {
                    _anim.SyncCurrent(PlayerAnimDriver.States.BackFlip);
                }
            }
        }

        /// <summary>
        /// Magic（等）打断空中所有权 — 离开 BackFlip，不重播 Jump_BackFlip。
        /// 保留速度；后续帧走 Rising/Falling + suppressAnim。
        /// </summary>
        public void YieldAirAnimToAction()
        {
            if (_state != PlayerAirState.BackFlip && !OnAir && !LandingLocked && !_landToRun)
            {
                return;
            }

            _takeoffLock = 0f;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            _anim.SetAirFloat(0f, 0f);

            if (_motor != null && _motor.IsGrounded)
            {
                _state = PlayerAirState.Grounded;
                _jumpConsumed = false;
                return;
            }

            float vy = _motor != null ? _motor.GetVelocity().y : 0f;
            _state = vy > 0.5f ? PlayerAirState.Rising : PlayerAirState.Falling;
        }

        /// <summary>不按帧重写 — 在阻塞 loco 加速的同时保留 State 7 的一次性速度。</summary>
        public void ApplyFixedVelocity()
        {
        }

        private void UpdateAirFloat(float move)
        {
            float axis = Mathf.Clamp(move, -1f, 1f);
            _anim.SetAirFloat(_motor.Facing * axis, _settings.airFloatDampTime);
        }

        private void EnsureAirAnim()
        {
            if (_anim.IsPlaying(PlayerAnimDriver.States.Jump)
                || _anim.IsPlaying(PlayerAnimDriver.States.OnAir))
            {
                if (_anim.IsPlaying(PlayerAnimDriver.States.OnAir))
                {
                    _anim.SyncCurrent(PlayerAnimDriver.States.OnAir);
                }

                return;
            }

            _anim.PlayBase(PlayerAnimDriver.States.OnAir);
        }

        private bool IsJumpAttackAnim()
        {
            return _anim.IsPlaying(PlayerAnimDriver.States.JumpAttackUp)
                || _anim.IsPlaying(PlayerAnimDriver.States.JumpAttackDown);
        }

        /// <param name="moveAxis">仅用于 airfloat 倾侧 — 空中网格朝向保持锁定。</param>
        public void DoJump(float moveAxis)
        {
            _jumpBuffer = 0f;
            _jumpConsumed = true;
            _state = PlayerAirState.Rising;
            _takeoffLock = _settings.jumpTakeoffLock;
            // floor Jump：SetVelocity(0,0) → AddForce Impulse (0, JumpForce=40)
            _motor.SetVelocity(Vector2.zero);
            _motor.AddForce(new Vector2(0f, _settings.jumpForce), ForceMode2D.Impulse);
            _anim.ForcePlay(PlayerAnimDriver.States.Jump);
            UpdateAirFloat(moveAxis);
            _audio?.PlayJump();
            // floor Jump CreateObject → JumpEffect，在 PLAYER 根节点（一次性起跳烟雾）。
            PlayerVfx.SpawnOneShot(jumpSmokePrefab, transform, jumpSmokeOffset, _motor.Facing,
                true, jumpSmokeLifetime);
        }

        private void EnterFall()
        {
            _state = PlayerAirState.Falling;
            _takeoffLock = 0f;
            _anim.ForcePlay(PlayerAnimDriver.States.OnAir);
            UpdateAirFloat(0f);
        }

        private void Land(PlayerIntent intent)
        {
            _jumpConsumed = false;
            _takeoffLock = 0f;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            _anim.SetAirFloat(0f, 0f);

            bool wantRun = Mathf.Abs(intent.Move) > 0.1f;
            if (wantRun)
            {
                // Movement LRAnim：翻转到 GAME_MOVE；同朝向 → Forward，否则 Landing_to_Run。
                int moveDir = intent.Move > 0f ? 1 : -1;
                bool sameFacing = moveDir == _motor.Facing;
                _motor.ForceFacing(moveDir);

                _landToRun = true;
                _landToRunElapsed = 0f;
                _state = PlayerAirState.Landing;
                _landingState = sameFacing
                    ? PlayerAnimDriver.States.LandingToRunForward
                    : PlayerAnimDriver.States.LandingToRun;
                _landLock = PlayerAnimTimings.LandingToRun.ClipLength + 0.05f;
                _anim.ForcePlay(_landingState);
            }
            else
            {
                _state = PlayerAirState.Landing;
                _landingState = PlayerAnimDriver.States.Landing;
                _landLock = PlayerAnimTimings.Landing.ClipLength + 0.05f;
                _anim.ForcePlay(_landingState);
            }

            _audio?.PlayLanding();
        }

        /// <summary>
        /// W + 身后 A/D → Jump State 7：SetVelocity(0,0) → AddForce Impulse (-30*facing, 30)。
        /// </summary>
        public bool TryStartBackFlip()
        {
            if (!CanBackFlip)
            {
                return false;
            }

            FinishLanding();
            _landToRun = false;
            _landToRunElapsed = 0f;
            BeginBackFlip();
            return true;
        }

        /// <summary>floor Jump [Jump]→State 7：把起跳改道为 BackFlip（朝向不变）。</summary>
        private void ConvertRisingToBackFlip()
        {
            BeginBackFlip();
        }

        private void BeginBackFlip()
        {
            _state = PlayerAirState.BackFlip;
            _jumpConsumed = true;          
            _jumpBuffer = 0f;
            _backFlipAir = _settings.backFlipMinAir;
            _landLock = 0f;
            _takeoffLock = _settings.backFlipMinAir;
            _backFlipTakeoffDir = -_motor.Facing;   

            float fx = -_settings.backFlipForce * _motor.Facing;
            _motor.SetVelocity(Vector2.zero);
            _motor.AddForce(new Vector2(fx, _settings.backFlipJumpForce), ForceMode2D.Impulse);
            _anim.SetAirFloat(0f, 0f);
            _anim.ForcePlay(PlayerAnimDriver.States.BackFlip);
            _audio?.PlayBackFlip();
        }

        private void EnterBackFlipLand()
        {
            _state = PlayerAirState.BackFlipLand;
            _landingState = PlayerAnimDriver.States.BackFlipLand;
            _landLock = PlayerAnimTimings.BackFlipLand.ClipLength + 0.05f;
            
            _motor.SetImmediateVelocityX(0f);
            _anim.SetAirFloat(0f, 0f);
            _anim.ForcePlay(_landingState);
            _audio?.PlayBackFlipLand();
        }

        public void NotifyJumpAttack()
        {
            if (!OnAir)
            {
                _state = PlayerAirState.Rising;
            }

            _takeoffLock = Mathf.Max(_takeoffLock, 0.05f);
        }
        private void TickBackFlipLandHold(PlayerIntent intent, bool actionOwnsAnim)
        {
            if (actionOwnsAnim)
            {
                FinishLanding();
                return;
            }
            bool takeoffDirHeld = _backFlipTakeoffDir > 0
                ? intent.Move > 0.1f
                : intent.Move < -0.1f;
            bool oppositeNow = _backFlipTakeoffDir > 0
                ? intent.Move < -0.1f
                : intent.Move > 0.1f;
            bool oppositePressed = oppositeNow && !(_prevMove < -0.1f || _prevMove > 0.1f); 
           
            if (!takeoffDirHeld && oppositePressed)
            {
                FinishLanding();            
                BeginOppositeBackFlip();   
                return;
            }
            bool staleBackFlipHold = takeoffDirHeld && intent.Jump;
            if (!staleBackFlipHold)
            {
               
                bool realActionInterrupt = Mathf.Abs(intent.Move) > 0.1f
                    || intent.JumpPressed
                    || intent.SlashPressed
                    || intent.WantsAds
                    || intent.EvadePressed
                    || intent.Crouch
                    || intent.ReloadPressed
                    || intent.Skill;

                if (realActionInterrupt)
                {
                    FinishLanding();
                    return;
                }
            }
            TickLandHold(_landingState);
        }
        private void BeginOppositeBackFlip()
        {
            _backFlipTakeoffDir = -_backFlipTakeoffDir;   // 反转方向
            _state = PlayerAirState.BackFlip;
            _jumpConsumed = true;
            _jumpBuffer = 0f;
            _backFlipAir = _settings.backFlipMinAir;
            _landLock = 0f;
            _takeoffLock = _settings.backFlipMinAir;

            float fx = _settings.backFlipForce * _backFlipTakeoffDir;
            _motor.SetVelocity(Vector2.zero);
            _motor.AddForce(new Vector2(fx, _settings.backFlipJumpForce), ForceMode2D.Impulse);
            _anim.SetAirFloat(0f, 0f);
            _anim.ForcePlay(PlayerAnimDriver.States.BackFlip);
            _audio?.PlayBackFlip();
        }

    }
}
