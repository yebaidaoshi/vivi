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

    public class PlayerJump : MonoBehaviour
    {
        [Header("跳跃烟雾 VFX（floor Movement Jump CreateObject → JumpEffect）")]
        [SerializeField] private GameObject jumpSmokePrefab;
        [SerializeField] private Vector2 jumpSmokeOffset = Vector2.zero;
        [SerializeField] private float jumpSmokeLifetime = 1.2f;

        [Header("滑铲跳动画名（完整路径，如 Jump/Jump_Dash）")]
        [SerializeField] private string slideJumpAnim = "Jump/Jump_Dash";

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

        private string _currentAirAnim;
        private float _currentAirAnimEndTime;

        public PlayerAirState State => _state;
        public bool OnAir => _state == PlayerAirState.Rising || _state == PlayerAirState.Falling
            || _state == PlayerAirState.BackFlip;
        public bool LandingLocked => !_landToRun
            && (_state == PlayerAirState.Landing || _state == PlayerAirState.BackFlipLand);
        public bool LandToRunLocksActions => _landToRun
            && _landToRunElapsed < PlayerAnimTimings.LandingToRun.SeRun;
        public bool IsBackFlipping => _state == PlayerAirState.BackFlip;
        public bool CanBackFlip => _motor != null && _motor.IsGrounded
            && !OnAir
            && !LandToRunLocksActions;
        public bool HasVelocityOverride => IsBackFlipping;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolveVfx();
        }

        private void ResolveVfx()
        {
#if UNITY_EDITOR
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

            if (_landLock > 0f) _landLock -= dt;
            if (_takeoffLock > 0f) _takeoffLock -= dt;

            if (intent.JumpPressed) _jumpBuffer = _settings.jumpBuffer;
            else _jumpBuffer -= dt;

            bool grounded = _motor.IsGrounded;
            bool suppressAnim = actionOwnsAnim;
            _prevMove = intent.Move;

            if (_state == PlayerAirState.BackFlip)
            {
                TickBackFlip(grounded, suppressAnim);
                return;
            }

            if (_landToRun)
            {
                TickLandToRun();
                if (_landToRun) return;
            }

            if (_state == PlayerAirState.BackFlipLand)
            {
                TickBackFlipLandHold(intent, suppressAnim);
                if (_state != PlayerAirState.Grounded) return;
            }
            else if (LandingLocked)
            {
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
                }
                else
                {
                    TickLandHold(_landingState);
                    return;
                }
            }

            if (OnAir)
            {
                TickAir(intent, grounded, suppressAnim: movementLocked || suppressAnim);
                return;
            }

            if (!grounded && _state == PlayerAirState.Grounded && !_motor.CanJump)
            {
                EnterFall();
                return;
            }

            if (movementLocked) return;

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

            if (!_motor.CanJump) _jumpConsumed = false;
        }

        public static bool IsMoveBehind(float move, int facing)
        {
            return Mathf.Abs(move) > 0.5f && move * facing < 0f;
        }

        public static bool IsHoldingBackForFlip(float move, int facing, bool jumpHeld)
        {
            return jumpHeld && IsMoveBehind(move, facing);
        }

        private bool IsMoveBehind(float move) => IsMoveBehind(move, _motor.Facing);

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
                _landToRun = false;
                _state = PlayerAirState.Grounded;
                _landLock = 0f;
                _landingState = null;
            }
        }

        private void TickLandHold(string expectedState)
        {
            bool onClip = !string.IsNullOrEmpty(expectedState) && _anim.IsPlaying(expectedState);
            if (!onClip && _state == PlayerAirState.Landing)
            {
                onClip = _anim.IsPlaying(PlayerAnimDriver.States.Landing);
            }

            if (onClip)
            {
                if (!string.IsNullOrEmpty(expectedState)) _anim.SyncCurrent(expectedState);

                if (_anim.BaseFinished || _landLock <= 0f)
                {
                    FinishLanding();
                }

                return;
            }

            if (_landLock <= 0f) FinishLanding();
        }

        private void FinishLanding()
        {
            _state = PlayerAirState.Grounded;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            _jumpConsumed = false;
            ClearCurrentAirAnimInternal();
        }

        private void TickAir(PlayerIntent intent, bool grounded, bool suppressAnim)
        {
            float vy = _motor.GetVelocity().y;

            if (!suppressAnim && _takeoffLock > 0f && IsMoveBehind(intent.Move) && !IsJumpAttackAnim())
            {
                ConvertRisingToBackFlip();
                TickBackFlip(grounded, suppressAnim: false);
                return;
            }

            if (vy > 0.5f) _state = PlayerAirState.Rising;
            else if (vy < -0.1f) _state = PlayerAirState.Falling;

            if (!suppressAnim)
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
                    ClearAirOnOwnedLand();
                }
                else
                {
                    Land(intent);
                }
            }
        }

        private void ClearAirOnOwnedLand()
        {
            _jumpConsumed = false;
            _takeoffLock = 0f;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            _state = PlayerAirState.Grounded;
            ClearCurrentAirAnimInternal();
            _anim.SetAirFloat(0f, 0f);
        }

        private void TickBackFlip(bool grounded, bool suppressAnim)
        {
            _backFlipAir -= _motor.DeltaTime;
            if (!suppressAnim) UpdateAirFloat(0f);

            float vy = _motor.GetVelocity().y;
            if (grounded && _backFlipAir <= 0f && vy <= 0.5f)
            {
                if (suppressAnim) ClearAirOnOwnedLand();
                else EnterBackFlipLand();
            }
            else if (!suppressAnim)
            {
                if (!_anim.IsPlaying(PlayerAnimDriver.States.BackFlip))
                    _anim.ForcePlay(PlayerAnimDriver.States.BackFlip);
                else
                    _anim.SyncCurrent(PlayerAnimDriver.States.BackFlip);
            }
        }

        public void YieldAirAnimToAction()
        {
            if (_state != PlayerAirState.BackFlip && !OnAir && !LandingLocked && !_landToRun) return;

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
                ClearCurrentAirAnimInternal();
                return;
            }

            float vy = _motor != null ? _motor.GetVelocity().y : 0f;
            _state = vy > 0.5f ? PlayerAirState.Rising : PlayerAirState.Falling;
        }

        public void ClearCustomAirAnim()
        {
            ClearCurrentAirAnimInternal();
        }

        public void NotifyEvadeStart()
        {
            _jumpBuffer = 0f;
            _jumpConsumed = true;
            _state = PlayerAirState.Rising;
            _takeoffLock = _settings.jumpTakeoffLock;
            ClearCurrentAirAnimInternal();
        }

        public void ForceLandAfterEvade(float moveAxis)
        {
            _jumpConsumed = false;
            _takeoffLock = 0f;
            _landLock = 0f;
            _landingState = null;
            _landToRun = false;
            _landToRunElapsed = 0f;
            ClearCurrentAirAnimInternal();
            _anim.SetAirFloat(0f, 0f);

            bool wantRun = Mathf.Abs(moveAxis) > 0.1f;
            if (wantRun)
            {
                int moveDir = moveAxis > 0f ? 1 : -1;
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

        private void ClearCurrentAirAnimInternal()
        {
            _currentAirAnim = null;
            _currentAirAnimEndTime = 0f;
        }

        public void ApplyFixedVelocity() { }

        private void UpdateAirFloat(float move)
        {
            float axis = Mathf.Clamp(move, -1f, 1f);
            _anim.SetAirFloat(_motor.Facing * axis, _settings.airFloatDampTime);
        }

        private void EnsureAirAnim()
        {
            if (!string.IsNullOrEmpty(_currentAirAnim))
            {
                bool stillInTime = Time.time < _currentAirAnimEndTime;
                if (stillInTime)
                {
                    _anim.SyncCurrent(_currentAirAnim);
                    return;
                }

                ClearCurrentAirAnimInternal();
            }

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

        public void DoJump(float moveAxis)
        {
            _jumpBuffer = 0f;
            _jumpConsumed = true;
            _state = PlayerAirState.Rising;
            _takeoffLock = _settings.jumpTakeoffLock;
            _motor.SetVelocity(Vector2.zero);
            _motor.AddForce(new Vector2(0f, _settings.jumpForce), ForceMode2D.Impulse);
            _anim.ForcePlay(PlayerAnimDriver.States.Jump);
            ClearCurrentAirAnimInternal();
            UpdateAirFloat(moveAxis);
            _audio?.PlayJump();
            PlayerVfx.SpawnOneShot(jumpSmokePrefab, transform, jumpSmokeOffset, _motor.Facing,
                true, jumpSmokeLifetime);
        }

        public void DoSlideJump(int slideDir)
        {
            _jumpBuffer = 0f;
            _jumpConsumed = true;
            _state = PlayerAirState.Rising;
            _takeoffLock = _settings.jumpTakeoffLock;

            float vx = _settings.runSpeed * _settings.slideJumpSpeedMultiplier * slideDir;
            _motor.SetVelocity(new Vector2(vx, 0f));
            _motor.AddForce(new Vector2(0f, _settings.jumpForce), ForceMode2D.Impulse);

            if (!string.IsNullOrEmpty(slideJumpAnim) && _anim.Animator != null)
            {
                bool hasState = _anim.Animator.HasState(0, Animator.StringToHash(slideJumpAnim));
                if (hasState)
                {
                    float clipLength = GetClipLength(slideJumpAnim);
                    _currentAirAnim = slideJumpAnim;
                    _currentAirAnimEndTime = Time.time + clipLength;
                    _anim.ForcePlay(slideJumpAnim);
                }
                else
                {
                    _anim.ForcePlay(PlayerAnimDriver.States.Jump);
                    ClearCurrentAirAnimInternal();
                }
            }
            else
            {
                _anim.ForcePlay(PlayerAnimDriver.States.Jump);
                ClearCurrentAirAnimInternal();
            }

            UpdateAirFloat(slideDir);
            _audio?.PlayJump();
            PlayerVfx.SpawnOneShot(jumpSmokePrefab, transform, jumpSmokeOffset, _motor.Facing,
                true, jumpSmokeLifetime);
        }

        private float GetClipLength(string statePath)
        {
            if (_anim.Animator == null) return 0.5f;
            var controller = _anim.Animator.runtimeAnimatorController;
            if (controller == null) return 0.5f;

            string leafName = statePath;
            int slash = statePath.LastIndexOf('/');
            if (slash >= 0 && slash < statePath.Length - 1)
                leafName = statePath.Substring(slash + 1);

            foreach (var clip in controller.animationClips)
            {
                if (clip == null) continue;
                if (clip.name == leafName || clip.name == statePath)
                    return clip.length;
            }

            return 0.5f;
        }

        private void EnterFall()
        {
            _state = PlayerAirState.Falling;
            _takeoffLock = 0f;
            ClearCurrentAirAnimInternal();
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
            ClearCurrentAirAnimInternal();
            _anim.SetAirFloat(0f, 0f);

            bool wantRun = Mathf.Abs(intent.Move) > 0.1f;
            if (wantRun)
            {
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

        public bool TryStartBackFlip()
        {
            if (!CanBackFlip) return false;

            FinishLanding();
            _landToRun = false;
            _landToRunElapsed = 0f;
            BeginBackFlip();
            return true;
        }

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
            ClearCurrentAirAnimInternal();

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
            if (!OnAir) _state = PlayerAirState.Rising;
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
            _backFlipTakeoffDir = -_backFlipTakeoffDir;
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