using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    /// <summary>
    /// ADS / fire / reload — port of GunFire FSM + Gun OnADS aim (floor.unity).
    /// Firing: CreateObject _Bullet + MuzzleFlashLight at Gun, SMG_Cartridge at ejection.
    /// Aim: FollowMouse2D(Aim_Target) + SmoothLookAt2d(AimPivot) + face-flip when mouse crosses.
    /// Locomotion overlay: Movement ADS sets aimDir = abs(facingScale - GAME_MOVE)
    ///   (0 walk / 1 stand / 2 walk-back) for Aim layer Aim_Standing blend tree.
    /// </summary>
    public class PlayerGun : MonoBehaviour
    {
        private enum AdsPhase
        {
            Idle,
            Raising,
            Holding,
            Releasing,
            Reloading,
            // Stand-aim ↔ crouch-aim posture swap (Crouch_Aim_to_Stand_Aim BlendTree).
            // A first-class sub-phase of aiming; entered from Holding, exits back to Holding.
            CrouchAimTransition
        }

        [Header("Prefabs (GunFire Firing CreateObject)")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject cartridgePrefab;

        [Header("Spawn points (heroine children)")]
        [SerializeField] private Transform gunMuzzle;
        [SerializeField] private Transform ejectionPort;

        [Header("Mouse aim (Gun FSM OnADS)")]
        [SerializeField] private Transform aimTarget;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private float aimLookSpeed = 12f;
        [SerializeField] private float aimTargetSmoothing = 0.35f;
        [Tooltip("World-X slack before flipping to face the mouse (Gun OnADS FloatCompare).")]
        [SerializeField] private float faceFlipSlop = 0.15f;

        [Header("Aim laser (GunSight)")]
        [Tooltip("Draw the red aiming ray while the gun is raised (GunSight in floor.unity).")]
        [SerializeField] private bool showAimLaser = true;
        [SerializeField] private Color laserColor = new Color(1f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private float laserWidth = 0.04f;
        [SerializeField] private float laserMaxLength = 30f;
        [Tooltip("Obstacles that stop the ray. Nothing = draw full length.")]
        [SerializeField] private LayerMask laserBlockMask;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private AdsPhase _phase = AdsPhase.Idle;
        private float _phaseTimer;
        private float _aimWeight;
        private float _fireCooldown;
        private float _vibrationTimer;
        private int _ammo;
        private bool _playedAimSound;
        private Quaternion _aimPivotRestLocal = Quaternion.identity;
        private bool _aimPivotRestStored;
        private Vector2 _aimDir = Vector2.right;
        /// <summary>While releasing, true only if nothing is interrupting — allows Idle/Run/melee to cut fold.</summary>
        private bool _releaseHoldsAnim;
        private LineRenderer _laser;
        private Transform _aimingRoot;

        // Aim_*_SMG states use BlendTrees — Unity does not fire clip SendEvents reliably.
        // Drive reload / fold SEs from elapsed time matching the baked event times.
        private float _reloadElapsed;
        private bool _reloadSeOffMagazine;
        private bool _reloadSeSetMagazine;
        private bool _reloadSeCocking;
        /// <summary>
        /// Do not treat leftover Aim_SMG_Hold as reload-done (move-ADS → reload used to
        /// EnterHold on the first frame and skip Aim_SMG_Reload).
        /// </summary>
        private bool _reloadClipSeen;
        private float _releaseElapsed;
        private bool _releaseSeFoldSmg;
        /// <summary>
        /// Mecanim Aim_SMG_Release → Crouching (crouching&gt;0.9, exitTime 1). Once the release
        /// state has been seen, do not ForcePlay it again — that one-frame restart is the crouch-ADS cancel jitter.
        /// </summary>
        private bool _releaseClipSeen;

        /// <summary>
        /// Committed Aim BlendTree crouching (0 stand / 1 crouch). Mirrors the PlayerCrouch FSM
        /// (passed in as <c>crouched</c>) for enter/hold/reload; latched at release start so the
        /// crouch fold-out clip is chosen correctly even if the crouch key is let go mid-release.
        /// PlayerGun does NOT own crouch state — PlayerCrouch is the authority.
        /// </summary>
        private bool _aimCrouch;
        /// <summary>PlayerCrouch.IsCrouching mirror for this frame (set at the top of Tick).</summary>
        private bool _crouched;
        private bool _crouchAimTransitionTarget;
        private float _crouchAimTransitionTimer;
        private float _crouchAimBlend;
        private bool _crouchAimClipSeen;

        public bool IsAds => _phase == AdsPhase.Raising || _phase == AdsPhase.Holding
            || _phase == AdsPhase.Reloading
            || _phase == AdsPhase.CrouchAimTransition;
        public bool IsReloading => _phase == AdsPhase.Reloading;
        /// <summary>
        /// Base-layer Aim_SMG* (incl. release) — crouch must not PlayBase Crouching over it.
        /// Unlike IsAds, this stays true while Releasing so crouch-ADS → crouch idle does not jitter.
        /// </summary>
        public bool OwnsCrouchBaseAnim => IsAds
            || (_phase == AdsPhase.Releasing && _releaseHoldsAnim);
        /// <summary>Blocks locomotion anim. Release is interruptible and does not lock once yielded.</summary>
        public bool IsBusy => _phase == AdsPhase.Raising || _phase == AdsPhase.Holding
            || _phase == AdsPhase.Reloading
            || _phase == AdsPhase.CrouchAimTransition
            || (_phase == AdsPhase.Releasing && _releaseHoldsAnim);
        public int Ammo => _ammo;
        public Vector2 AimDirection => _aimDir;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            _ammo = _settings.magazineCapacity;
            _anim.SetAimLayerWeight(0f);
            _anim.SetAimDir(1f);
            ResolveRefs();
        }

        private void ResolveRefs()
        {
            if (gunMuzzle == null)
            {
                gunMuzzle = FindChildTransform("Gun");
            }

            if (ejectionPort == null)
            {
                ejectionPort = FindChildTransform("SMG_EjectionPort");
            }

            if (aimTarget == null)
            {
                aimTarget = FindChildTransform("Aim_Target");
            }

            if (aimPivot == null)
            {
                aimPivot = FindChildTransform("AimPivot");
            }

            if (aimPivot != null && !_aimPivotRestStored)
            {
                _aimPivotRestLocal = aimPivot.localRotation;
                _aimPivotRestStored = true;
            }

            if (_aimingRoot == null)
            {
                _aimingRoot = FindChildTransform("Aiming");
            }

            // Create under Aiming at init so hierarchy matches floor GunSight placement.
            if (showAimLaser)
            {
                EnsureLaser();
            }

#if UNITY_EDITOR
			if (bulletPrefab == null)
			{
				bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/_Bullet.prefab");
			}

			if (muzzleFlashPrefab == null)
			{
				muzzleFlashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/MuzzleFlashLight.prefab");
			}

			if (cartridgePrefab == null)
			{
				cartridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/SMG_Cartridge.prefab");
			}
#endif
        }

        private Transform FindChildTransform(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        /// <param name="actionInterrupt">
        /// Melee / jump / crouch / backstep already owning the character — abort release anim.
        /// </param>
        /// <param name="crouched">PlayerCrouch.IsCrouching — the crouch authority the gun mirrors.</param>
        public void Tick(PlayerIntent intent, bool canAds, bool actionInterrupt, bool sliding,
            bool allowFaceFlip, bool crouched)
        {
            _crouched = crouched;

            if (_fireCooldown > 0f)
            {
                _fireCooldown -= _motor.DeltaTime;
            }

            if (_vibrationTimer > 0f)
            {
                _vibrationTimer -= _motor.DeltaTime;
            }

            if (_phase == AdsPhase.Reloading)
            {
                TickReloading(intent, allowFaceFlip, actionInterrupt, sliding);
            }
            else
            {
                // Enter via canAds; while already aiming, interrupts match former actionInterrupt.
                bool wantAds = intent.WantsAds
                    && (IsAds ? !actionInterrupt && !sliding : canAds);

                switch (_phase)
                {
                    case AdsPhase.Idle:
                        TickIdle(intent, wantAds);
                        break;

                    case AdsPhase.Raising:
                        TickRaising(intent, wantAds, allowFaceFlip);
                        break;

                    case AdsPhase.Holding:
                        TickHolding(intent, wantAds, allowFaceFlip);
                        break;

                    case AdsPhase.Releasing:
                        TickRelease(intent, actionInterrupt, sliding, wantAds);
                        break;

                    case AdsPhase.CrouchAimTransition:
                        TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                        break;
                }
            }

            UpdateAimLaser();
        }

        /// <summary>
        /// Reload ignores RMB release and A/D. Melee / jump / evade / magic / slide / crouch
        /// cancel ADS and fail the reload (ammo unchanged).
        /// Moving stand keeps Aim_Standing (walk legs + GunSight, floor ADS_RELOAD); still/crouch
        /// drop the Aim layer so the Base Aim_SMG_Reload body animation shows.
        /// </summary>
        private void TickReloading(PlayerIntent intent, bool allowFaceFlip,
            bool actionInterrupt, bool sliding)
        {
            // Left crouch mid crouch-reload → yield so PlayerCrouch can BeginStandUp, same as
            // TickRelease's (_aimCrouch && !intent.Crouch) check. Without this, PlayerCrouch
            // already snapped Standing (ExitCrouch under adsActive, which — like the ADS-Holding
            // case — defers clearing the animator to the gun) while gun keeps holding the base
            // layer on Aim_SMG_Reload for several more frames, so a same-frame BackStep
            // (HandleReloadBackStep) hard-cancels with a stale crouch=true left behind (nothing
            // else — TickStanding does not — ever clears it once PlayerCrouch is Standing).
            if (_aimCrouch && !intent.Crouch)
            {
                CancelReload(keepCrouch: false);
                _anim.SetCrouch(false);
                return;
            }

            bool hardCancel = actionInterrupt || sliding
                || intent.SlashPressed || intent.JumpPressed
                || intent.EvadePressed || intent.CrouchPressed;
            if (hardCancel)
            {
                CancelReload(keepCrouch: intent.Crouch && _aimCrouch);
                return;
            }

            float dt = _motor.DeltaTime;
            _phaseTimer -= dt;
            _reloadElapsed += dt;
            TickReloadSe();
            _aimCrouch = _crouched;
            _anim.SetCrouch(_aimCrouch);
            UpdateAdsAim(intent, allowFaceFlip);

            // Moving stand: keep Aim_Standing (walking legs + GunSight line). floor Movement
            // State 4 (ADS_RELOAD) leaves the Aim layer at 1 — reload is audio-only while walking.
            // Still / crouch: drop to Base Aim_SMG_Reload so the reload body animation shows.
            bool moving = !_aimCrouch && Mathf.Abs(intent.Move) > 0.1f;
            bool onReload;
            if (moving)
            {
                SetAimWeightImmediate(1f);
                ParkBaseSmgHold();
                onReload = true;
                _reloadClipSeen = true;
            }
            else
            {
                SetAimWeightImmediate(0f);
                onReload = _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgReload);
                if (onReload)
                {
                    _reloadClipSeen = true;
                    _anim.SyncAim(PlayerAnimDriver.States.AimSmgReload);
                }
                else if (!_reloadClipSeen && _reloadElapsed < 0.12f)
                {
                    // Re-assist only briefly. ForcePlay every frame while Mecanim is mid-transition
                    // restarts the clip at t=0 and looks like "reload never plays".
                    _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgReload);
                }
            }

            // Finish only after Reload has actually played — never on stale Hold from move-ADS.
            float minPlay = PlayerAnimTimings.AimSmgReload.ClipLength * 0.85f;
            bool finished = _phaseTimer <= 0f
                || (_reloadClipSeen && onReload && _anim.AimFinished && _reloadElapsed >= minPlay)
                || (_reloadClipSeen && !onReload && _reloadElapsed >= minPlay);
            if (finished)
            {
                _ammo = _settings.magazineCapacity;
                TickReloadSe(forceFlush: true);
                _reloadClipSeen = false;
                EnterHold(forcePlay: true);
            }
        }

        /// <summary>Abort reload without refilling — leave ADS for the interrupting action.</summary>
        private void CancelReload(bool keepCrouch)
        {
            _reloadSeOffMagazine = false;
            _reloadSeSetMagazine = false;
            _reloadSeCocking = false;
            _reloadClipSeen = false;
            FinishRelease(keepCrouch);
        }

        /// <summary>
        /// Timed reload SEs matching Aim_*_SMG_Reload events (BlendTree skips anim SendEvent).
        /// </summary>
        private void TickReloadSe(bool forceFlush = false)
        {
            if (_audio == null)
            {
                return;
            }

            float t = forceFlush ? float.MaxValue : _reloadElapsed;
            if (!_reloadSeOffMagazine && t >= PlayerAnimTimings.AimSmgReload.SeOffMagazine)
            {
                _reloadSeOffMagazine = true;
                _audio.PlayMagazineOut();
            }

            if (!_reloadSeSetMagazine && t >= PlayerAnimTimings.AimSmgReload.SeSetMagazine)
            {
                _reloadSeSetMagazine = true;
                _audio.PlaySetMagazine();
            }

            if (!_reloadSeCocking && t >= PlayerAnimTimings.AimSmgReload.SeCocking)
            {
                _reloadSeCocking = true;
                _audio.PlayCocking();
            }
        }

        private void TickIdle(PlayerIntent intent, bool wantAds)
        {
            if (wantAds)
            {
                _aimCrouch = _crouched;
                EnterRaise();
                _anim.SetCrouch(_aimCrouch);
            }
            else if (intent.ReloadPressed && _ammo < _settings.magazineCapacity)
            {
                StartReload();
            }
            else
            {
                BlendAimWeight(0f);
                ResetAimPose();
                _anim.SetAimDir(1f);
            }
        }

        private void TickRaising(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            // During raise, snap crouch blend (transition clips play from Hold).
            _aimCrouch = _crouched;
            _anim.SetCrouch(_aimCrouch);
            // Raise uses Base Aim_SMG — keep Aim layer off so Aim_Standing cannot hide it.
            SetAimWeightImmediate(0f);
            UpdateAdsAim(intent, allowFaceFlip);
            _phaseTimer -= _motor.DeltaTime;
            if (!wantAds)
            {
                EnterRelease();
            }
            else if (_phaseTimer <= 0f || _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold)
                || (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmg) && _anim.AimFinished))
            {
                EnterHold(forcePlay: false);
            }
        }

        private void TickHolding(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            if (!wantAds)
            {
                EnterRelease();
                return;
            }

            // Stand Hold ↔ crouch Hold: hand off to CrouchAimTransition phase, which plays
            // Crouch_Aim_to_Stand_Aim and returns to Holding when the clip completes.
            bool wantCrouch = _crouched;
            if (wantCrouch != _aimCrouch && _vibrationTimer <= 0f)
            {
                BeginCrouchAimTransition(wantCrouch);
                TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                return;
            }

            _anim.SetCrouch(_aimCrouch);
            UpdateAdsAim(intent, allowFaceFlip);

            // Stand + moving (and not mid-recoil): Aim layer Aim_Standing (walk/stand/back).
            // Still / crouch / firing: Base SMG so Aim_SMG_Hold + fire vibration recoil show.
            // `moving` is stable across shots (not per-frame ForcePlay) → no move-shoot jitter.
            bool moving = !_aimCrouch && _vibrationTimer <= 0f
                && Mathf.Abs(intent.Move) > 0.1f;
            if (moving)
            {
                SetAimWeightImmediate(1f);
                ParkBaseSmgHold();
            }
            else
            {
                SetAimWeightImmediate(0f);
                TickHoldBaseSmg();
            }

            if (intent.ReloadPressed && _ammo < _settings.magazineCapacity)
            {
                StartReload();
                return;
            }

            if (intent.Fire && _fireCooldown <= 0f)
            {
                // Recoil only when not walking — vibration over Aim_Standing would flicker.
                TryFire(allowRecoil: !moving);
            }
        }

        /// <summary>Crouch hold: drive Base Aim_SMG_Hold / return from vibration.</summary>
        private void TickHoldBaseSmg()
        {
            if (_vibrationTimer > 0f)
            {
                if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgVibration))
                {
                    _anim.SyncAim(PlayerAnimDriver.States.AimSmgVibration);
                }

                return;
            }

            if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgVibration))
            {
                if (_anim.AimFinished)
                {
                    _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
                }

                return;
            }

            if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold))
            {
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgHold);
            }
            else if (!_anim.IsPlayingAim(PlayerAnimDriver.States.CrouchAimToStandAim))
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
            }
        }

        /// <summary>
        /// Stand hold parks Base under Aim_Standing without per-frame ForcePlay (avoids restart jitter).
        /// </summary>
        private void ParkBaseSmgHold()
        {
            if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold)
                || _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgVibration))
            {
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgHold);
                return;
            }

            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
        }

        /// <summary>
        /// SMG state Crouch_Aim_to_Stand_Aim BlendTree:
        /// crouching=0 → Aim_Aim_SMG_Hold_to_Crouch_Aim (stand→crouch),
        /// crouching=1 → Crouch_Crouch_Aim_to_Stand_Aim (crouch→stand).
        /// Weight is locked to the source pose for the whole clip.
        /// </summary>
        private void BeginCrouchAimTransition(bool toCrouch)
        {
            _phase = AdsPhase.CrouchAimTransition;
            _crouchAimTransitionTarget = toCrouch;
            _crouchAimClipSeen = false;
            _crouchAimTransitionTimer = PlayerAnimTimings.CrouchAimTransition.ClipLength + 0.05f;
            // Source pose selects the clip — NOT the destination (destination would invert them).
            _crouchAimBlend = toCrouch ? 0f : 1f;
            _anim.SetCrouchingWeight(_crouchAimBlend);
            _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
        }

        private void TickCrouchAimTransition(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            // ADS dropped / interrupted mid-swap: abandon the transition and release.
            if (!wantAds)
            {
                EnterRelease();
                return;
            }

            UpdateAdsAim(intent, allowFaceFlip);
            SetAimWeightImmediate(0f);
            // Keep BlendTree on the correct transition clip for the whole duration.
            _anim.SetCrouchingWeight(_crouchAimBlend);
            _crouchAimTransitionTimer -= _motor.DeltaTime;

            bool playing = _anim.IsPlayingAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            if (playing)
            {
                _crouchAimClipSeen = true;
                _anim.SyncAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }
            else if (!_crouchAimClipSeen)
            {
                // Play may not stick first frame — keep forcing until Mecanim reports the state.
                _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }

            // Duration-locked: do not use AimFinished from the previous Hold pose.
            float minPlay = PlayerAnimTimings.CrouchAimTransition.ClipLength * 0.85f;
            float elapsed = PlayerAnimTimings.CrouchAimTransition.ClipLength + 0.05f
                - _crouchAimTransitionTimer;
            bool finished = _crouchAimTransitionTimer <= 0f
                || (_crouchAimClipSeen && playing && _anim.AimFinished && elapsed >= minPlay)
                || (_crouchAimClipSeen && !playing && elapsed >= minPlay);
            if (!finished)
            {
                return;
            }

            _aimCrouch = _crouchAimTransitionTarget;
            _anim.SetCrouch(_aimCrouch);
            EnterHold(forcePlay: true);
        }

        /// <summary>
        /// Play Aim_SMG_Release to completion unless move / combat / jump / crouch / ADS cuts it.
        /// While crouched, keep crouching=1 so Crouch_Crouch_Aim_SMG_Release plays. Animator already
        /// transitions Release → Crouching; do not re-Play Release after that exit (cancel jitter).
        /// </summary>
        private void TickRelease(PlayerIntent intent, bool actionInterrupt, bool sliding, bool wantAds)
        {
            BlendAimWeight(0f);
            ResetAimPose();
            float dt = _motor.DeltaTime;
            _phaseTimer -= dt;
            _releaseElapsed += dt;
            TickReleaseSe();

            if (wantAds)
            {
                EnterRaise();
                return;
            }

            // Left crouch during crouch-ADS release → yield so PlayerCrouch can BeginStandUp.
            // PlayerCrouch's ExitCrouch(adsActive) snaps Standing without touching the animator
            // (it defers to the gun while ADS owns the base anim) — clear crouch/crouching here
            // so nothing leaves a stale crouch=true / crouching=1 behind (PlayerCrouch.TickStanding
            // never clears it once already Standing).
            if (_aimCrouch && !intent.Crouch)
            {
                FinishRelease(keepCrouch: false);
                _anim.SetCrouch(false);
                return;
            }

            // Stand release + crouch (held or pressed): yield so Crouch enter can play.
            if (!_aimCrouch && intent.Crouch)
            {
                FinishRelease(keepCrouch: false);
                return;
            }

            // Crouch-ADS release: ignore A/D (no strafe). Stand release still yields to move.
            bool inputInterrupt = actionInterrupt || sliding
                || intent.SlashPressed || intent.JumpPressed
                || intent.EvadePressed || intent.ReloadPressed
                || (!_aimCrouch && Mathf.Abs(intent.Move) > 0.1f);

            if (inputInterrupt)
            {
                // Yield base layer to locomotion / melee / jump — do not ForcePlay Idle here.
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
                return;
            }

            _releaseHoldsAnim = true;
            _anim.SetCrouch(_aimCrouch);
            bool onRelease = _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgRelease);
            bool onCrouching = _anim.IsPlaying(PlayerAnimDriver.States.Crouching);
            if (onRelease)
            {
                _releaseClipSeen = true;
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgRelease);
            }
            else if (!_releaseClipSeen && _phaseTimer > 0f && !onCrouching)
            {
                // First frames only — Mecanim may not report the state yet.
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);
                onRelease = true;
            }
            else if (_releaseClipSeen && !onRelease)
            {
                // exitTime handoff (Crouching) or SMG Exit — settle, do not restart Release.
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
                return;
            }

            if ((onRelease && _anim.AimFinished) || _phaseTimer <= 0f || onCrouching)
            {
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
            }
        }

        /// <summary>SE_foldSMG @ 0.5833 — same Cocking.ogg (BlendTree skips anim SendEvent).</summary>
        private void TickReleaseSe()
        {
            if (_audio == null || _releaseSeFoldSmg)
            {
                return;
            }

            if (_releaseElapsed >= PlayerAnimTimings.AimSmgRelease.SeFoldSmg)
            {
                _releaseSeFoldSmg = true;
                _audio.PlayCocking();
            }
        }

        private void FinishRelease(bool keepCrouch = false)
        {
            _phase = AdsPhase.Idle;
            _releaseHoldsAnim = false;
            _releaseClipSeen = false;
            _anim.SetAiming(false);
            SetAimWeightImmediate(0f);
            _anim.SetAimDir(1f);
            if (keepCrouch)
            {
                _anim.SetCrouch(true);
                // Prefer Mecanim Release→Crouching exit; only ForcePlay if we never landed there.
                if (_anim.IsPlaying(PlayerAnimDriver.States.Crouching))
                {
                    _anim.SyncCurrent(PlayerAnimDriver.States.Crouching);
                }
                else
                {
                    _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
                }
            }

            _aimCrouch = false;
        }

        /// <summary>
        /// Movement FaceCheck: aimDir = abs(facingSign - GAME_MOVE) → 0 walk / 1 stand / 2 back.
        /// Gun OnADS: Aim_Target follows mouse; AimPivot SmoothLookAt2d; flip when mouse crosses.
        /// </summary>
        private void UpdateAdsAim(PlayerIntent intent, bool allowFaceFlip)
        {
            ResolveRefs();

            // Movement FaceCheck: aimDir = abs(facing - move) → 0 walk / 1 stand / 2 back.
            // Crouch / ADSCrouch: ignore A/D — lock stand aimDir (_aimCrouch mirrors the crouch FSM).
            float move = _aimCrouch ? 0f : intent.Move;
            float aimLocomotion = Mathf.Abs(_motor.Facing - move);
            _anim.SetAimDir(aimLocomotion);

            if (!intent.HasAimPoint)
            {
                _aimDir = new Vector2(_motor.Facing, 0f);
                return;
            }

            Vector2 aimPoint = intent.AimPoint;
            Vector2 origin = aimPivot != null
                ? (Vector2)aimPivot.position
                : (Vector2)transform.position;

            if (aimTarget != null)
            {
                Vector3 cur = aimTarget.position;
                Vector3 target = new Vector3(aimPoint.x, aimPoint.y, cur.z);
                float t = Mathf.Clamp01(aimTargetSmoothing);
                // FollowMouse2D uses Lerp with smoothing as blend factor each frame.
                aimTarget.position = Vector3.Lerp(cur, target, t <= 0f ? 1f : t);
                aimPoint = aimTarget.position;
            }

            Vector2 toAim = aimPoint - origin;
            if (toAim.sqrMagnitude > 0.0001f)
            {
                _aimDir = toAim.normalized;
            }
            else
            {
                _aimDir = new Vector2(_motor.Facing, 0f);
            }

            if (aimPivot != null)
            {
                float angle = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg;
                // When facing left, localScale.x is negative; compensate so the pivot still
                // points at the mouse in world space (same idea as Gun Turn + SmoothLookAt2d).
                Quaternion desired = Quaternion.Euler(0f, 0f, angle);
                float step = Mathf.Clamp01(aimLookSpeed * _motor.DeltaTime);
                aimPivot.rotation = Quaternion.Slerp(aimPivot.rotation, desired, step);
            }

            // Face mouse only when FacingOwner is Gun (ground ADS).
            if (!allowFaceFlip || !_motor.IsGrounded)
            {
                return;
            }

            float dx = aimPoint.x - transform.position.x;
            if (dx > faceFlipSlop && _motor.Facing < 0)
            {
                _motor.ForceFacing(1);
            }
            else if (dx < -faceFlipSlop && _motor.Facing > 0)
            {
                _motor.ForceFacing(-1);
            }
        }

        /// <summary>
        /// GunSight red ray: shown while the gun is up (Holding / Reloading, not during raise),
        /// from the muzzle along the current aim direction; optionally clipped at obstacles.
        /// </summary>
        private void UpdateAimLaser()
        {
            if (!showAimLaser)
            {
                if (_laser != null)
                {
                    _laser.enabled = false;
                }

                return;
            }

            EnsureLaser();

            // GunSight shows once the gun is up: Holding + Reloading (floor keeps it during reload).
            bool visible = (_phase == AdsPhase.Holding || _phase == AdsPhase.Reloading)
                && _anim.IsInSmgAim();
            _laser.enabled = visible;
            if (!visible)
            {
                return;
            }

            Transform originT = gunMuzzle != null
                ? gunMuzzle
                : (aimPivot != null ? aimPivot : transform);
            Vector3 originPos = originT.position;
            Vector2 origin = originPos;
            Vector2 dir = _aimDir.sqrMagnitude > 0.0001f
                ? _aimDir.normalized
                : new Vector2(_motor.Facing, 0f);

            Vector2 end = origin + dir * laserMaxLength;
            if (laserBlockMask.value != 0)
            {
                var hit = Physics2D.Raycast(origin, dir, laserMaxLength, laserBlockMask);
                if (hit.collider != null && !hit.collider.isTrigger
                    && hit.collider.transform != transform
                    && !hit.collider.transform.IsChildOf(transform))
                {
                    end = hit.point;
                }
            }

            _laser.startWidth = laserWidth;
            _laser.endWidth = laserWidth;
            _laser.SetPosition(0, new Vector3(origin.x, origin.y, originPos.z));
            _laser.SetPosition(1, new Vector3(end.x, end.y, originPos.z));
        }

        private void EnsureLaser()
        {
            if (_laser != null)
            {
                return;
            }

            if (_aimingRoot == null)
            {
                _aimingRoot = FindChildTransform("Aiming");
            }

            var go = new GameObject("GunSightLaser");
            // Parent under Aiming (floor GunSight socket), not the Heroine root.
            go.transform.SetParent(_aimingRoot != null ? _aimingRoot : transform, false);
            _laser = go.AddComponent<LineRenderer>();
            _laser.useWorldSpace = true;
            _laser.positionCount = 2;
            _laser.numCapVertices = 1;
            _laser.textureMode = LineTextureMode.Stretch;
            _laser.alignment = LineAlignment.View;
            _laser.receiveShadows = false;
            _laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laser.startWidth = laserWidth;
            _laser.endWidth = laserWidth;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _laser.material = new Material(shader);
            }

            _laser.startColor = laserColor;
            _laser.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0f);
            // Render above the character sprites.
            _laser.sortingOrder = 100;
            _laser.enabled = false;
        }

        private void ResetAimPose()
        {
            if (aimPivot != null && _aimPivotRestStored)
            {
                aimPivot.localRotation = Quaternion.Slerp(
                    aimPivot.localRotation,
                    _aimPivotRestLocal,
                    Mathf.Clamp01(aimLookSpeed * _motor.DeltaTime));
            }

            _aimDir = new Vector2(_motor != null ? _motor.Facing : 1, 0f);
        }

        private void EnterRaise()
        {
            _phase = AdsPhase.Raising;
            _phaseTimer = _settings.adsBlendTime > 0f ? _settings.adsBlendTime : 0.25f;
            _anim.SetAiming(true);
            _anim.SetAimDir(1f);
            // Base/SMG owns the pose; keep Aim layer off so Aim_Standing cannot override.
            SetAimWeightImmediate(0f);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmg);
            if (!_playedAimSound)
            {
                _audio?.PlayAiming();
                _playedAimSound = true;
            }
        }

        private void EnterHold(bool forcePlay)
        {
            _phase = AdsPhase.Holding;
            _anim.SetAiming(true);
            _anim.SetCrouch(_aimCrouch);
            // Default to Base SMG (weight 0). TickHolding raises Aim layer to 1 only while
            // walking, so entering hold while still shows the Base aim pose (not a 1-frame walk).
            SetAimWeightImmediate(0f);

            if (forcePlay || !_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold))
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
            }
            else
            {
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgHold);
            }
        }

        private void EnterRelease()
        {
            _phase = AdsPhase.Releasing;
            // Aim_Aim_SMG_Release clip length; move/attack/jump may cut it short.
            _phaseTimer = _settings.adsReleaseDuration > 0f
                ? _settings.adsReleaseDuration
                : PlayerAnimTimings.AimSmgRelease.ClipLength;
            _releaseHoldsAnim = true;
            _playedAimSound = false;
            _vibrationTimer = 0f;
            _releaseElapsed = 0f;
            _releaseSeFoldSmg = false;
            _releaseClipSeen = false;
            _anim.SetCrouch(_aimCrouch);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);
        }

        private void BlendAimWeight(float target)
        {
            float blend = _settings.adsBlendTime > 0f
                ? _motor.DeltaTime / _settings.adsBlendTime
                : 1f;
            _aimWeight = Mathf.MoveTowards(_aimWeight, target, blend);
            _anim.SetAimLayerWeight(_aimWeight);
        }

        private void SetAimWeightImmediate(float weight)
        {
            _aimWeight = weight;
            _anim.SetAimLayerWeight(weight);
        }

        private void TryFire(bool allowRecoil = true)
        {
            if (_ammo <= 0)
            {
                StartReload();
                return;
            }

            _ammo--;
            _fireCooldown = _settings.fireInterval;
            _anim.SetTrigger("gunfire");
            // Recoil = Base Aim_SMG_vibration with Aim layer off (crouch, or standing still).
            // While walking (allowRecoil=false) keep Aim_Standing — recoil would flicker the walk.
            if (allowRecoil)
            {
                _vibrationTimer = 0.08f;
                SetAimWeightImmediate(0f);
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgVibration);
            }
            else
            {
                _vibrationTimer = 0f;
            }

            _audio?.PlayGunFire();
            SpawnFiringVfx();
        }

        /// <summary>
        /// GunFire Firing: CreateObject bullet + muzzle at Gun, cartridge at ejection port.
        /// </summary>
        private void SpawnFiringVfx()
        {
            ResolveRefs();
            Transform muzzle = gunMuzzle != null ? gunMuzzle : transform;
            Vector2 dir = _aimDir.sqrMagnitude > 0.0001f
                ? _aimDir.normalized
                : new Vector2(_motor.Facing, 0f);

            if (bulletPrefab != null)
            {
                // Spawn unrotated; PlayerBulletMover applies aim + prefab z=90 offset.
                var bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.identity);
                bullet.name = "_Bullet";
                var mover = bullet.GetComponent<PlayerBulletMover>();
                if (mover == null)
                {
                    mover = bullet.AddComponent<PlayerBulletMover>();
                }

                mover.Launch(dir, _settings.bulletSpeed);
            }

            if (muzzleFlashPrefab != null)
            {
                var flash = Instantiate(muzzleFlashPrefab, muzzle.position, Quaternion.identity);
                flash.name = "MuzzleFlashLight";
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                flash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                Destroy(flash, 0.35f);
            }

            if (cartridgePrefab != null && ejectionPort != null)
            {
                // floor CreateObject rotation euler (-90, 0, 0) at SMG_EjectionPort.
                var rot = ejectionPort.rotation * Quaternion.Euler(-90f, 0f, 0f);
                var casing = Instantiate(cartridgePrefab, ejectionPort.position, rot);
                casing.name = "SMG_Cartridge";
                Destroy(casing, 2f);
            }
        }

        private void StartReload()
        {
            _phase = AdsPhase.Reloading;
            _phaseTimer = _settings.reloadDuration > 0f
                ? _settings.reloadDuration
                : PlayerAnimTimings.AimSmgReload.ClipLength;
            _vibrationTimer = 0f;
            _reloadElapsed = 0f;
            _reloadSeOffMagazine = false;
            _reloadSeSetMagazine = false;
            _reloadSeCocking = false;
            _reloadClipSeen = false;
            _anim.SetAiming(true);
            // Drop Aim_Standing so Base Aim_SMG_Reload is visible (stand move + reload).
            SetAimWeightImmediate(0f);
            _anim.SetCrouch(_aimCrouch);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgReload);
            // Reload SEs are timed in TickReloadSe (Aim_* states are BlendTrees).
        }
    }
}
