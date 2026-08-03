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
    /// Crouch / slide — port of Crouching FSM (floor.unity).
    /// SINGLE SOURCE OF TRUTH for crouch posture, including crouch-aim (ADSCrouch): while gun ADS,
    /// this FSM still runs (Standing→Entering→Crouching), it just yields the base anim to the gun.
    /// Other modules must query <see cref="State"/> / <see cref="IsCrouching"/> etc. rather than
    /// re-deriving crouch from raw input — PlayerGun mirrors this FSM (it does not own crouch state).
    /// While gun ADS: GAME_PICKUP toggles ADS ↔ ADSCrouch via animator float <c>crouching</c>
    /// (Aim_SMG* BlendTrees pick Crouch_Crouch_Aim_* clips) without cancelling Aim.
    /// <c>Crouch_To_Idle</c> / stand-up <c>Slide_To_Idle</c> are soft A_to_B (interruptible anytime).
    /// Horizontal override velocity is applied in FixedUpdate via <see cref="ApplyFixedVelocity"/>.
    /// </summary>
    public class PlayerCrouch : MonoBehaviour
    {
        [Header("Slide smoke VFX (floor Crouching Slide CreateObject → SlideEffect)")]
        [SerializeField] private GameObject slideSmokePrefab;
        [Tooltip("Offset from the _Heroine root; floor spawns at the owner root (zero).")]
        [SerializeField] private Vector2 slideSmokeOffset = Vector2.zero;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private GameObject _slideFx;

        private PlayerCrouchState _state = PlayerCrouchState.Standing;
        private float _slideTimer;
        private float _phaseTimer;
        private float _runAccum;
        private int _slideDir = 1;
        private float _overrideVx;
        private bool _hasOverrideVx;
        private bool _yieldedBaseAnimLastFrame;

        public PlayerCrouchState State => _state;
        public bool IsCrouching => _state == PlayerCrouchState.Entering
            || _state == PlayerCrouchState.Crouching
            || _state == PlayerCrouchState.Sliding
            || _state == PlayerCrouchState.SlideToCrouch;
        public bool IsSliding => _state == PlayerCrouchState.Sliding;
        /// <summary>True while crouched / sliding — not during interruptible stand-up.</summary>
        public bool IsBusy => IsCrouching;
        public bool IsStandingUp => _state == PlayerCrouchState.StandingUp;
        public bool HasVelocityOverride => _hasOverrideVx;
        /// <summary>True during the crouch-enter clip before its own Attackable event
        /// (Crouch_Crouch.anim @ 0.3333s) — melee must not cut the transition short before then
        /// (PlayerArbiter.CanMelee). Other interrupts (release S, magic, backstep/backflip) are
        /// unaffected — see ForceStand / TickEntering's own !intent.Crouch check.</summary>
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
			// Modules are composed at runtime with no serialized refs; pull the prefab by path.
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

            // Melee/magic just released the base anim (e.g. a crouch-attack cut at Movable, see
            // PlayerMelee.TickMovableGroundInterrupt) while we were held in Crouching, or still
            // mid Entering (a second rapid attack cut the crouch-enter clip before it finished):
            // (re)play the crouch-enter clip from the top instead of either letting TickCrouching
            // PlayBase(Crouching) snap straight to the idle pose, or leaving TickEntering stuck —
            // its own IsPlaying(Crouch)/IsPlaying(Crouching) checks can never pass once the
            // Animator spent that window showing the attack clip instead.
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

        public void ApplyFixedVelocity()
        {
            if (_hasOverrideVx)
            {
                _motor.SetImmediateVelocityX(_overrideVx);
            }
        }

        /// <summary>Called only from <see cref="TickStanding"/> / <see cref="TickStandingUp"/> —
        /// the FSM dispatch in <see cref="Tick"/> already guarantees the state.</summary>
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

            // floor Crouching Idle: BoolTest DoCrouch everyFrame (held, not edge).
            // Jump→hold S must crouch on land even though press happened in air.
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

            // ADS hold: skip Crouch enter clip (gun BlendTree). Release: yield base but keep Entering
            // so stand-ADS-release → crouch can still play Crouch after gun finishes.
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
                // adsActive only (not release): gun owns crouch↔stand aim; release yields to BeginStandUp.
                ExitCrouch(adsActive);
                return;
            }

            // ADS / release fold-out / melee: do not PlayBase Crouching over their clips.
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
        /// floor ADSCrouch → ADS on GAME_PICKUP release: leave crouch state; gun plays
        /// Crouch_Crouch_Aim_to_Stand_Aim then clears crouching.
        /// Without ADS: normal Crouch_To_Idle stand-up.
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
                    CancelSlideToRun(desired);
                    return;
                }
            }

            _slideTimer -= _motor.DeltaTime;
            float fade = Mathf.Clamp01(_slideTimer / _settings.slideDuration);
            SetOverride(_slideDir * _settings.slideForce * Mathf.Lerp(0.35f, 1f, fade));

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
            if (!intent.Crouch)
            {
                BeginStandUpFromSlide();
                return;
            }

            // ADS while settling into crouch → stay crouched (crouch-aim), do not ForceStand.
            if (intent.WantsAds)
            {
                _state = PlayerCrouchState.Crouching;
                _anim.SetCrouch(true);
                return;
            }

            if (intent.JumpPressed || intent.Jump || intent.SlashPressed
                || intent.EvadePressed || intent.ReloadPressed)
            {
                ForceStand();
                return;
            }

            _phaseTimer -= _motor.DeltaTime;
            if (_anim.IsPlaying(PlayerAnimDriver.States.SlideToIdle))
            {
                _anim.SyncCurrent(PlayerAnimDriver.States.SlideToIdle);
                if (_anim.BaseFinished || _phaseTimer <= 0f)
                {
                    _state = PlayerCrouchState.Crouching;
                    _anim.SetCrouch(true);
                    _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
                }
            }
            else if (_anim.IsPlaying(PlayerAnimDriver.States.Crouching) || _phaseTimer <= 0f)
            {
                _state = PlayerCrouchState.Crouching;
                _anim.SetCrouch(true);
                _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
            }
        }

        private void TickStandingUp(PlayerIntent intent)
        {
            UpdateRunAccum(intent);

            // Soft A_to_B: any action cuts immediately; loco soft-holds Crouch_To_Idle otherwise.
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
            // During ADS, PlayerGun plays Aim_Aim_SMG_Hold_to_Crouch_Aim — do not snap crouching.
            // Stand-ADS release: still play Crouch (gun yields on intent.Crouch the same frame).
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
            // floor Slide CreateObject: looping SlideEffect at the owner (_Heroine) root; it trails
            // for the whole slide and is stopped when the slide ends (StopSlideFx).
            StopSlideFx();
            _slideFx = PlayerVfx.SpawnOneShot(slideSmokePrefab, transform, slideSmokeOffset,
                _slideDir, false, 0f);
        }

        private void StopSlideFx()
        {
            PlayerVfx.StopAndDestroy(_slideFx);
            _slideFx = null;
        }

        private void EnterSlideToCrouch()
        {
            StopSlideFx();
            _state = PlayerCrouchState.SlideToCrouch;
            // Controller has Slide_To_Idle (0.53); Spine also has Slide_to_Crouch (0.6).
            _phaseTimer = PlayerAnimTimings.SlideToIdle.ClipLength + 0.05f;
            _anim.ForcePlay(PlayerAnimDriver.States.SlideToIdle);
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
            StopSlideFx();
            _state = PlayerCrouchState.StandingUp;
            _phaseTimer = PlayerAnimTimings.SlideToIdle.ClipLength + 0.05f;
            _anim.SetCrouch(false);
            _anim.ForcePlay(PlayerAnimDriver.States.SlideToIdle);
        }

        private void CancelSlideToRun(int facing)
        {
            StopSlideFx();
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
            StopSlideFx();
            _state = PlayerCrouchState.Standing;
            _slideTimer = 0f;
            _phaseTimer = 0f;
            _runAccum = 0f;
            _hasOverrideVx = false;
            _anim.SetCrouch(false);
        }

        private void OnDisable()
        {
            // Never leave a looping slide trail orphaned if the player is torn down mid-slide.
            StopSlideFx();
        }
    }
}
