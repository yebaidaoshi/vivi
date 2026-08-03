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
    /// Jump / fall / land / backflip — port of Jump FSM.
    /// Idle Landing: soft hold, interruptible. Landing_to_Run: hard-lock until first SE_Run,
    /// then yield; facing flips to move dir on land (Movement LRAnim).
    /// Rule : left/right only drives airfloat lean.
    /// </summary>
    public class PlayerJump : MonoBehaviour
    {
        [Header("Jump smoke VFX (floor Movement Jump CreateObject → JumpEffect)")]
        [SerializeField] private GameObject jumpSmokePrefab;
        [Tooltip("Offset from the _Heroine root; x mirrored by facing.")]
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
        public PlayerAirState State => _state;
        public bool OnAir => _state == PlayerAirState.Rising || _state == PlayerAirState.Falling
            || _state == PlayerAirState.BackFlip;
        /// <summary>Soft idle / backflip land hold — interruptible.</summary>
        public bool LandingLocked => !_landToRun
            && (_state == PlayerAirState.Landing || _state == PlayerAirState.BackFlipLand);
        /// <summary>Landing_to_Run before first SE_Run — hard-locks actions.</summary>
        public bool LandToRunLocksActions => _landToRun
            && _landToRunElapsed < PlayerAnimTimings.LandingToRun.SeRun;
        /// <summary>Air backflip only (land is interruptible soft hold).</summary>
        public bool IsBackFlipping => _state == PlayerAirState.BackFlip;
        /// <summary>Grounded; W + behind A/D → Jump State 7.</summary>
        public bool CanBackFlip => _motor != null && _motor.IsGrounded
            && !OnAir
            && !LandToRunLocksActions;
        /// <summary>Blocks loco ramp during flip; velocity is one-shot (no per-frame rewrite).</summary>
        public bool HasVelocityOverride => IsBackFlipping;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolveVfx();
        }

        private void ResolveVfx()
        {
#if UNITY_EDITOR
			// Modules are composed at runtime with no serialized refs; pull the prefab by path.
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

            if (_state == PlayerAirState.BackFlip)
            {
                // JumpLocked is always set during backflip — use actionOwnsAnim, not movementLocked.
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
                // SE_Run reached — fall through with actions unlocked.
            }

            if (LandingLocked)
            {
                if (intent.WantsSoftActionInterrupt || actionOwnsAnim)
                {
                    FinishLanding();
                    // Fall through so jump / grounded logic can run same frame.
                }
                else
                {
                    TickLandHold(_landingState);
                    return;
                }
            }

            if (OnAir)
            {
                // JumpLocked (e.g. MagicBusy): keep air state / land clear, but do not steal anim.
                TickAir(intent, grounded, suppressAnim: movementLocked || suppressAnim);
                return;
            }

            // Walk-off ledge (Fall event equivalent).
            if (!grounded && _state == PlayerAirState.Grounded && !_motor.CanJump)
            {
                EnterFall();
                return;
            }

            if (movementLocked)
            {
                return;
            }

            // W + behind A/D → BackFlip; plain W → Jump. A held W keeps taking off each time the
            // character is grounded (idle jump→land→jump loop); _jumpConsumed gates one takeoff per
            // ground contact. Buffer still catches a tap made just before touchdown.
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

        /// <summary>A/D toward the character's back (opposite facing).</summary>
        public static bool IsMoveBehind(float move, int facing)
        {
            return Mathf.Abs(move) > 0.5f && move * facing < 0f;
        }

        /// <summary>W + behind A/D — do not flip facing (backflip takeoff).</summary>
        public static bool IsHoldingBackForFlip(float move, int facing, bool jumpHeld)
        {
            return jumpHeld && IsMoveBehind(move, facing);
        }

        private bool IsMoveBehind(float move) => IsMoveBehind(move, _motor.Facing);

        /// <summary>Hard-lock until first SE_Run, then Grounded so loco soft-continues the clip.</summary>
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
                // Unlock actions; leave anim playing for locomotion soft hold.
                _landToRun = false;
                _state = PlayerAirState.Grounded;
                _landLock = 0f;
                _landingState = null;
            }
        }

        /// <summary>Hold land clip until BaseFinished; landLock is only a safety timeout.</summary>
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

                // Prefer clip end; landLock is a safety timeout if BaseFinished never fires.
                if (_anim.BaseFinished || _landLock <= 0f)
                {
                    FinishLanding();
                }

                return;
            }

            // Lost the land state (interrupted) or timeout.
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
            // BackFlipLand used to leave this stuck true while grounded (CanJump never clears it).
            _jumpConsumed = false;
        }

        private void TickAir(PlayerIntent intent, bool grounded, bool suppressAnim)
        {
            float vy = _motor.GetVelocity().y;

            // floor Jump [Jump] state: during the takeoff ChronosWait only, behind input → BackFlip.
            // Strictly the takeoff window (not the whole rise) so it can't fire mid-air.
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

            if (!suppressAnim)//如果没有被禁止播放动画，那么就执行下面的代码
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
                    // Magic (etc.) already owns the base layer — clear air without Landing*.
                    ClearAirOnOwnedLand();
                }
                else
                {
                    Land(intent);
                }
            }
        }

        /// <summary>Touchdown while another system owns anim (magic channel): exit air, no land clip.</summary>
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
        /// Magic (etc.) interrupted air ownership — leave BackFlip without replaying Jump_BackFlip.
        /// Keeps velocity; subsequent frames use Rising/Falling + suppressAnim.
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

        /// <summary>No per-frame rewrite — preserves State 7 one-shot velocity while blocking loco ramp.</summary>
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

        /// <param name="moveAxis">Airfloat lean only — mesh facing stays locked in air.</param>
        public void DoJump(float moveAxis)
        {
            _jumpBuffer = 0f;
            _jumpConsumed = true;
            _state = PlayerAirState.Rising;
            _takeoffLock = _settings.jumpTakeoffLock;
            // floor Jump: SetVelocity(0,0) → AddForce Impulse (0, JumpForce=40)
            _motor.SetVelocity(Vector2.zero);
            _motor.AddForce(new Vector2(0f, _settings.jumpForce), ForceMode2D.Impulse);
            _anim.ForcePlay(PlayerAnimDriver.States.Jump);
            UpdateAirFloat(moveAxis);
            _audio?.PlayJump();
            // floor Jump CreateObject → JumpEffect at PLAYER root (one-shot takeoff smoke).
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
                // Movement LRAnim: flip to GAME_MOVE; same facing → Forward, else Landing_to_Run.
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
        /// W + behind A/D → Jump State 7: SetVelocity(0,0) → AddForce Impulse (-30*facing, 30).
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

        /// <summary>floor Jump [Jump]→State 7: redirect the takeoff into BackFlip (facing stays put).</summary>
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

            // floor State 7: SetVelocity(0,0) → BackStepForce=-30*facing → AddForce (fx, 30)
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
            _jumpConsumed = false;
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
    }
}
