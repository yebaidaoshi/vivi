using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    public enum PlayerMeleePhase
    {
        Idle,
        /// <summary>Before Cancelable — fully locked.</summary>
        WindupLocked,
        /// <summary>After Cancelable — ADS / behind-A/D backstep may cut in.</summary>
        ActionCancelable,
        /// <summary>After Movable — run / jump / crouch may also cut in.</summary>
        MovableSheath
    }

    /// <summary>
    /// Ground combo: chain on <c>Attackable</c>;
    /// <c>Cancelable</c> unlocks ADS / behind-A/D backstep (jump too, see <see cref="LocksActions"/>);
    /// <c>Movable</c> unlocks run / crouch.
    /// Attack3 Movable = Melee INIT (combo reset); new Attack1 needs a fresh SlashPressed after that.
    /// </summary>
    public class PlayerMelee : MonoBehaviour
    {
        private struct AttackTiming
        {
            public float Attackable;
            public float Cancelable;
            public float Movable;
            public float SheathEnd;
        }

        private static readonly AttackTiming[] GroundTimings =
        {
            default,
            new AttackTiming
            {
                Attackable = PlayerAnimTimings.Attack1.Attackable,
                Cancelable = PlayerAnimTimings.Attack1.Cancelable,
                Movable = PlayerAnimTimings.Attack1.Movable,
                SheathEnd = PlayerAnimTimings.Attack1.ClipLength
            },
            new AttackTiming
            {
                Attackable = PlayerAnimTimings.Attack2.Attackable,
                Cancelable = PlayerAnimTimings.Attack2.Cancelable,
                Movable = PlayerAnimTimings.Attack2.Movable,
                SheathEnd = PlayerAnimTimings.Attack2.ClipLength
            },
            new AttackTiming
            {
                Attackable = -1f,
                Cancelable = PlayerAnimTimings.Attack3.Cancelable,
                Movable = PlayerAnimTimings.Attack3.Movable,
                SheathEnd = PlayerAnimTimings.Attack3.ClipLength
            },
        };

        [Header("Slash VFX (Effects E_Katana1/2/3)")]
        [SerializeField] private GameObject slash1Prefab;
        [SerializeField] private GameObject slash2Prefab;
        [SerializeField] private GameObject slash3Prefab;
        [SerializeField] private GameObject slideSlashPrefab;
        [SerializeField] private GameObject jumpSlashUpPrefab;
        [SerializeField] private GameObject jumpSlashDownPrefab;

        [Header("Attack4 / Melee4 VFX (Effects E_Katana4 → State 15)")]
        [Tooltip("Primary crescent — CreateObject #1.")]
        [SerializeField] private GameObject slash4Prefab;
        [Tooltip("Dust/smoke — CreateObject #2, then SetScale x = heroineScale * -2.")]
        [SerializeField] private GameObject katana4SmokePrefab;
        [Tooltip("Afterwind driver — CreateObject #3 (_Melee4AfterSlash FSM spawns Melee4After / AfterShock).")]
        [SerializeField] private GameObject melee4AfterSlashPrefab;
        [Tooltip("World offset from heroine root: (facing * x, y). Matches floor GetScale→*3→SetVector3XYZ when |scale|=1.")]
        [SerializeField] private float slash4OffsetXScale = 3f;
        [SerializeField] private float slash4OffsetY = 3.6f;
        [Tooltip("_Melee4AfterSlash must outlive its ChronosWait / Melee4After chain.")]
        [SerializeField] private float melee4AfterLifetime = 4f;

        [Tooltip("Spawn parent. floor.unity CreateObject uses the _Heroine root; leave null for that.")]
        [SerializeField] private Transform slashSpawn;
        [Tooltip("LOCAL offset on the heroine root (parented). +x is always forward and the arc " +
            "auto-mirrors when facing left. The body box is ~8 units tall (feet y0 / torso y4 / " +
            "head y8), so torso height is ~4. Tune in the inspector to sit on the blade.")]
        [SerializeField] private Vector2 slashOffset = new Vector2(0.7f, 4f);
        [SerializeField] private float slashLifetime = 1.2f;

        private PlayerContext _ctx;
        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;
        private PlayerSlashVfx _slashVfx;

        private PlayerMeleePhase _phase = PlayerMeleePhase.Idle;
        private int _combo;
        private float _elapsed;
        private float _attackableAt;
        private float _cancelableAt;
        private float _movableAt;
        private float _sheathEndAt;
        private float _overrideVx;
        private bool _hasOverrideVx;
        private string _activeState;
        /// <summary>Attack2 fires a second slash on anim event E_Katana3 @ 0.3167s.</summary>
        private bool _pendingAttack2Katana3;
        /// <summary>Attack3/Attack4 fires Melee4 VFX on anim event E_Katana4 @ 0.1667s.</summary>
        private bool _pendingAttack3Katana4;
        /// <summary>Play Melee4After SE after _Melee4AfterSlash State 1 wait (0.5s from Katana4).</summary>
        private bool _pendingMelee4AfterSe;
        private float _melee4AfterSeAt;

        public PlayerMeleePhase Phase => _phase;
        public bool IsAttacking => _phase != PlayerMeleePhase.Idle;
        /// <summary>Until Cancelable — blocks ADS / backstep / jump
        /// (PlayerArbiter.CanJump gates on this, not on <see cref="LocksMovement"/>).</summary>
        public bool LocksActions => _phase == PlayerMeleePhase.WindupLocked;
        /// <summary>Until Movable — blocks run / crouch (PlayerArbiter.CanCrouch gates on this)
        /// (and pins velocity to 0 while grounded).</summary>
        public bool LocksMovement => _phase == PlayerMeleePhase.WindupLocked
            || _phase == PlayerMeleePhase.ActionCancelable;
        public int Combo => _combo;
        public bool HasVelocityOverride => _hasOverrideVx && LocksMovement;

        public void Init(PlayerContext context)
        {
            _ctx = context;
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolveVfx();
        }

        private void ResolveVfx()
        {
            // Procedural fallback (刀光) — renders when an authored slash prefab is missing.
            _slashVfx = GetComponent<PlayerSlashVfx>() ?? gameObject.AddComponent<PlayerSlashVfx>();

#if UNITY_EDITOR
			// Editor convenience (modules are composed at runtime with no serialized refs).
			if (slash1Prefab == null) slash1Prefab = LoadPrefab("Slash1_1");
			if (slash2Prefab == null) slash2Prefab = LoadPrefab("Slash2");
			if (slash3Prefab == null) slash3Prefab = LoadPrefab("Slash3");
			if (slideSlashPrefab == null) slideSlashPrefab = LoadPrefab("Slash1_Slide");
			if (jumpSlashUpPrefab == null) jumpSlashUpPrefab = LoadPrefab("JumpSlashUp");
			if (jumpSlashDownPrefab == null) jumpSlashDownPrefab = LoadPrefab("JumpSlash_Down");
			if (slash4Prefab == null) slash4Prefab = LoadPrefab("Slash4");
			if (katana4SmokePrefab == null) katana4SmokePrefab = LoadPrefab("Katana4_Smoke");
			if (melee4AfterSlashPrefab == null) melee4AfterSlashPrefab = LoadPrefab("_Melee4AfterSlash");
#endif
        }

#if UNITY_EDITOR
		private static GameObject LoadPrefab(string prefabName)
		{
			return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/" + prefabName + ".prefab");
		}
#endif

        /// <summary>
        /// Spawn a slash on the heroine. The slash is PARENTED to the _Heroine root
        /// (<c>PlayerMelee.transform</c>) — the same transform whose <c>localScale.x</c> the motor
        /// flips to ±1 for facing (<see cref="PlayerMotor.ForceFacing"/>). Because of that:
        ///   • <see cref="slashOffset"/> is a LOCAL offset, so +x is always "forward" and the whole
        ///     arc auto-mirrors when the heroine faces left — no manual facing math, no wrong side;
        ///   • the prefab keeps its authored local scale/rotation, inheriting only the ±1 flip;
        ///   • it tracks the heroine for its short lifetime (matches floor State 22 SetParent, and is
        ///     harmless for the others since a slash only lives ~1s).
        /// This replaces the earlier world-space spawn, which never mirrored and needed guessed
        /// offsets that could not be recovered from floor.unity's runtime FSM variables.
        /// </summary>
        private GameObject SpawnSlash(GameObject prefab, PlayerSlashVfx.SlashKind kind,
            Vector2 extraOffset = default)
        {
            Transform spawnRoot = slashSpawn != null ? slashSpawn : transform;
            int facing = _motor.Facing;
            Vector3 localPos = new Vector3(
                slashOffset.x + extraOffset.x,
                slashOffset.y + extraOffset.y,
                0f);

            if (prefab == null)
            {
                // Procedural crescent: it builds its own object, so hand it the mirrored world pose.
                Vector3 worldPos = spawnRoot.TransformPoint(localPos);
                return _slashVfx != null
                    ? _slashVfx.Play(kind, worldPos, spawnRoot.rotation, facing, slashLifetime)
                    : null;
            }

            var fx = Instantiate(prefab);
            // Parent under the heroine (localScale.x = ±facing) → position + arc mirror for free.
            fx.transform.SetParent(spawnRoot, false);
            fx.transform.localPosition = localPos;

            if (slashLifetime > 0f)
            {
                Destroy(fx, slashLifetime);
            }

            return fx;
        }

        private void SpawnComboSlash(int index)
        {
            _pendingAttack2Katana3 = false;
            _pendingAttack3Katana4 = false;
            _pendingMelee4AfterSe = false;
            switch (index)
            {
                case 2:
                    // Attack2.anim: E_Katana2 @ 0.0333 (near start) + E_Katana3 @ 0.3167.
                    SpawnSlash(slash2Prefab, PlayerSlashVfx.SlashKind.Attack2);
                    _pendingAttack2Katana3 = true;
                    break;
                case 3:
                    // Attack3.anim (Animator Attack4): only E_Katana4 @ 0.1667 — deferred.
                    _pendingAttack3Katana4 = true;
                    break;
                default:
                    SpawnSlash(slash1Prefab, PlayerSlashVfx.SlashKind.Attack1);
                    break;
            }
        }

        /// <summary>
        /// Timed slash VFX / SE matching clip SendEvents that fire after attack start.
        /// </summary>
        private void TickPendingSlashVfx()
        {
            if (_pendingAttack2Katana3 && _combo == 2
                && _elapsed >= PlayerAnimTimings.Attack2.E_Katana3)
            {
                _pendingAttack2Katana3 = false;
                SpawnSlash(slash3Prefab, PlayerSlashVfx.SlashKind.Attack3);
                _audio?.PlayKatana(3);
            }

            if (_pendingAttack3Katana4 && _combo == 3
                && _elapsed >= PlayerAnimTimings.Attack3.E_Katana4)
            {
                _pendingAttack3Katana4 = false;
                SpawnMelee4Vfx();
            }

        }

        private void TickPendingMelee4AfterSe()
        {
            if (!_pendingMelee4AfterSe || Time.time < _melee4AfterSeAt)
            {
                return;
            }

            _pendingMelee4AfterSe = false;
            _audio?.PlayMelee4After();
        }

        /// <summary>
        /// floor Effects State 15 (E_Katana4): world-space CreateObject of Slash4
        /// (offset = (facing * 3, 3.6)), Katana4_Smoke (scale.x = |prefab.x| * -facing), and
        /// _Melee4AfterSlash (scale.x = facing; FSM drives afterwind + Melee4After SE).
        /// </summary>
        private void SpawnMelee4Vfx()
        {
            // Immediate SEs: Melee4_2 + Meleeplus + Melee4Afterwind (Effects State 15).
            _audio?.PlayMelee4();
            // Delayed SE: Melee4After — originally _Melee4AfterSlash State1 ChronosWait(0.5) → State6.
            // Do not rely on that FSM: ChronosWait stalls without a Clock, and AudioPlay needs AudioMaster.
            // Use Time.time so the SE still fires if the attack ends before 0.5s (prefab is unparented).
            _pendingMelee4AfterSe = true;
            _melee4AfterSeAt = Time.time + PlayerAnimTimings.Attack3.Melee4AfterSe;

            Transform spawnRoot = slashSpawn != null ? slashSpawn : transform;
            int facing = _motor.Facing;
            Vector3 rootPos = spawnRoot.position;
            Quaternion rootRot = spawnRoot.rotation;

            // CreateObject #1 — Slash4. Keep authored scale (9,8,9); facing-left = Y+180 Flip
            // (floor SetFsmFloat → FloatSignTest → SetRotation Y+=180). Do NOT overwrite scale
            // with heroine localScale (that collapsed 9→±1 and broke the mesh).
            var slash4 = SpawnWorldFx(
                slash4Prefab,
                PlayerSlashVfx.SlashKind.Attack3,
                rootPos + new Vector3(facing * slash4OffsetXScale, slash4OffsetY, 0f),
                rootRot,
                facing,
                slashLifetime);
            if (slash4 != null && facing < 0)
            {
                slash4.transform.Rotate(0f, 180f, 0f, Space.Self);
            }

            // CreateObject #2 — Katana4_Smoke at root; SetScale x = facing * -2 on |prefab.x|=2.
            var smoke = SpawnWorldFx(
                katana4SmokePrefab,
                PlayerSlashVfx.SlashKind.After,
                rootPos,
                rootRot,
                facing,
                slashLifetime);
            MirrorScaleX(smoke != null ? smoke.transform : null, -facing);

            // CreateObject #3 — _Melee4AfterSlash VFX/hitbox driver (SE is owned by PlayerAudio above).
            var after = SpawnWorldFx(
                melee4AfterSlashPrefab,
                PlayerSlashVfx.SlashKind.After,
                rootPos,
                rootRot,
                facing,
                melee4AfterLifetime);
            MirrorScaleX(after != null ? after.transform : null, facing);
        }

        /// <summary>
        /// Unparented world spawn (matches floor CreateObject spawnPoint + position, no SetParent).
        /// Keeps the prefab's authored localScale; only position/rotation are assigned.
        /// </summary>
        private GameObject SpawnWorldFx(GameObject prefab, PlayerSlashVfx.SlashKind kind,
            Vector3 worldPos, Quaternion worldRot, int facing, float lifetime)
        {
            if (prefab == null)
            {
                return _slashVfx != null
                    ? _slashVfx.Play(kind, worldPos, worldRot, facing, lifetime)
                    : null;
            }

            var fx = Instantiate(prefab);
            fx.transform.SetParent(null, false);
            fx.transform.position = worldPos;
            fx.transform.rotation = worldRot;

            if (lifetime > 0f)
            {
                Destroy(fx, lifetime);
            }

            return fx;
        }

        /// <summary>Keep authored |x| magnitude; apply facing sign (floor GetScale → SetScale pattern).</summary>
        private static void MirrorScaleX(Transform t, int sign)
        {
            if (t == null)
            {
                return;
            }

            int s = sign >= 0 ? 1 : -1;
            Vector3 scale = t.localScale;
            scale.x = Mathf.Abs(scale.x) * s;
            if (Mathf.Abs(scale.x) < 0.0001f)
            {
                scale.x = s;
            }

            t.localScale = scale;
        }

        public void Tick(PlayerIntent intent, bool canMelee)
        {
            _hasOverrideVx = false;
            TickPendingMelee4AfterSe();

            if (_phase != PlayerMeleePhase.Idle)
            {
                TickActiveAttack(intent, !canMelee);
                return;
            }

            // Sliding: no attack of any kind (incl. the former Slide_Attack special) — see
            // PlayerArbiter.CanMelee, which now also gates on !CrouchIsSliding.
            if (!canMelee || !intent.SlashPressed)
            {
                return;
            }

            if (!_ctx.IsGrounded || _ctx.OnAir)
            {
                BeginJumpAttack();
                return;
            }

            DoGroundCombo(1);
        }

        /// <summary>Starts a jump attack, picking Up/Down by the current vertical velocity
        /// (mirrors the initial-trigger check in <see cref="Tick"/>).</summary>
        private void BeginJumpAttack()
        {
            float vy = _motor.GetVelocity().y;
            if (vy >= 0f)
            {
                BeginAttack(
                    PlayerAnimDriver.States.JumpAttackUp,
                    -1f,
                    PlayerAnimTimings.JumpAttackUp.JCancelable,
                    PlayerAnimTimings.JumpAttackUp.ClipLength,
                    PlayerAnimTimings.JumpAttackUp.ClipLength,
                    0);
                _ctx.NotifyJumpAttack?.Invoke();
                _audio?.PlayMeleeSwing(0);
                SpawnSlash(jumpSlashUpPrefab, PlayerSlashVfx.SlashKind.JumpUp);
            }
            else
            {
                BeginAttack(
                    PlayerAnimDriver.States.JumpAttackDown,
                    -1f,
                    PlayerAnimTimings.JumpAttackDown.ClipLength,
                    PlayerAnimTimings.JumpAttackDown.ClipLength,
                    PlayerAnimTimings.JumpAttackDown.ClipLength,
                    0);
                _motor.SetVelocityY(-Mathf.Abs(_settings.jumpAttackDownYVelocity));
                _ctx.NotifyJumpAttack?.Invoke();
                _audio?.PlayJumpAttackDown();
                SpawnSlash(jumpSlashDownPrefab, PlayerSlashVfx.SlashKind.JumpDown);
            }
        }

        public void ApplyFixedVelocity()
        {
            if (_hasOverrideVx)
            {
                _motor.SetImmediateVelocityX(_overrideVx);
            }
        }

        /// <summary>
        /// floor SE_JumpAttack(Down) / EventLand: touching ground while Jump_Attack_Up or
        /// Jump_Attack_Down is playing cuts straight to the Landing clip instead of finishing the
        /// slash pose on the ground. Ending the attack here — before PlayerJump.Tick runs later
        /// this frame — hands movement/anim ownership straight back to PlayerJump so its normal
        /// Land() plays this same frame instead of the silent ClearAirOnOwnedLand() it uses while
        /// melee still owns anim.
        /// </summary>
        private bool TickJumpAttackLanding()
        {
            bool isJumpAttack = _activeState == PlayerAnimDriver.States.JumpAttackUp
                || _activeState == PlayerAnimDriver.States.JumpAttackDown;
            if (!isJumpAttack || !_ctx.IsGrounded)
            {
                return false;
            }

            EndAttack();
            return true;
        }

        private void TickActiveAttack(PlayerIntent intent, bool gunBusy)
        {
            if (TickJumpAttackLanding())
            {
                return;
            }

            float dt = _motor.DeltaTime;
            _elapsed += dt;

            TickLunge(dt);
            TickPendingSlashVfx();

            // Movable unlocks crouch / run — the most-recently-unlocked action takes priority
            // over simply continuing to swing, so check it before a held Slash gets to chain
            // the ground combo (TickComboChain) or restart it (TickAttack3Restart). Grounded-only:
            // airborne / jump-attack chaining is untouched, still resolved further below.
            if (TickMovableGroundInterrupt(intent))
            {
                return;
            }

            if (TickComboChain(intent))
            {
                return;
            }

            if (TickJumpAttackChain(intent))
            {
                return;
            }

            AdvancePhase();

            if (TickAttack3Restart(intent, gunBusy))
            {
                return;
            }

            if (TickCancelableInterrupts(intent, gunBusy))
            {
                return;
            }

            TickSheathEnd();
        }

        /// <summary>
        /// Movable unlocks crouch (PlayerArbiter.CanCrouch) / run (CanMove) — check them first so
        /// the newest-unlocked action wins over the attack continuing. Mirrors the crouch / move
        /// conditions in <see cref="TickCancelableInterrupts"/>, just evaluated earlier and keyed
        /// off <see cref="_elapsed"/> directly (no AdvancePhase lag) so it lands the same frame
        /// Movable fires. Grounded-only — airborne/jump-attack OnAir handling stays untouched,
        /// still covered later by TickCancelableInterrupts.
        /// </summary>
        private bool TickMovableGroundInterrupt(PlayerIntent intent)
        {
            if (!_ctx.IsGrounded || _elapsed < _movableAt)
            {
                return false;
            }

            bool behind = PlayerJump.IsMoveBehind(intent.Move, _motor.Facing);
            bool crouchInterrupt = intent.CrouchPressed || _ctx.IsCrouchBusy;
            bool moveInterrupt = !behind && Mathf.Abs(intent.Move) > 0.1f;
            if (!crouchInterrupt && !moveInterrupt)
            {
                return false;
            }

            EndAttack();
            return true;
        }

        /// <summary>
        /// While movement is locked by melee, pin horizontal velocity to 0 so the character
        /// does not keep sliding from a prior run.
        /// </summary>
        private void TickLunge(float dt)
        {
            if (!LocksMovement || !_motor.IsGrounded)
            {
                return;
            }

            _overrideVx = 0f;
            _hasOverrideVx = true;
        }

        /// <summary>Returns true when a chained hit started (caller should stop this frame).</summary>
        private bool TickComboChain(PlayerIntent intent)
        {
            bool groundCombo = _combo >= 1 && _combo <= _settings.maxMeleeCombo;
            bool canChain = groundCombo && _attackableAt >= 0f
                && _combo < _settings.maxMeleeCombo
                && _ctx.IsGrounded && !_ctx.IsSliding;

            // No buffering: input BEFORE Attackable does nothing. From the Attackable event until
            // the clip ends, ANY frame with the attack key down (held or a fresh press in that
            // window) advances the combo (1→2 / 2→3). A single tap released before Attackable can
            // never chain, so one click = one attack.
            if (!canChain || _elapsed < _attackableAt)
            {
                return false;
            }

            if (intent.Slash)
            {
                DoGroundCombo(_combo + 1);
                return true;
            }

            return false;
        }

        /// <summary>Returns true when a new jump attack chained in (caller should stop this frame).
        /// Jump attacks are not part of the ground combo (<see cref="_combo"/> stays 0), so they
        /// chain on their own: once JCancelable fires (<see cref="_cancelableAt"/>) the swing lock
        /// lifts, and holding the attack key while still airborne throws another jump attack
        /// (picked Up/Down by the current vertical velocity, same as the initial trigger).</summary>
        private bool TickJumpAttackChain(PlayerIntent intent)
        {
            bool isJumpAttack = _activeState == PlayerAnimDriver.States.JumpAttackUp
                || _activeState == PlayerAnimDriver.States.JumpAttackDown;
            bool airborne = !_ctx.IsGrounded || _ctx.OnAir;
            if (!isJumpAttack || !airborne || !intent.Slash || _elapsed < _cancelableAt)
            {
                return false;
            }

            BeginJumpAttack();
            return true;
        }

        private void AdvancePhase()
        {
            if (_phase == PlayerMeleePhase.WindupLocked && _elapsed >= _cancelableAt)
            {
                _phase = PlayerMeleePhase.ActionCancelable;
            }

            if (_phase == PlayerMeleePhase.ActionCancelable && _elapsed >= _movableAt)
            {
                _phase = PlayerMeleePhase.MovableSheath;
            }
        }

        /// <summary>After Attack3 Movable: a held or fresh attack key loops back to Attack1
        /// (Attack1→2→3→1 cycle while held); returns true when a new combo started.</summary>
        private bool TickAttack3Restart(PlayerIntent intent, bool gunBusy)
        {
            if (_combo == 3 && _elapsed >= _movableAt && intent.Slash
                && _ctx.IsGrounded && !_ctx.IsSliding && !gunBusy)
            {
                DoGroundCombo(1);
                return true;
            }

            return false;
        }

        /// <summary>Cancelable / Movable action + move interrupts. Returns true if the attack ended.</summary>
        private bool TickCancelableInterrupts(PlayerIntent intent, bool gunBusy)
        {
            if (_phase != PlayerMeleePhase.ActionCancelable
                && _phase != PlayerMeleePhase.MovableSheath)
            {
                return false;
            }

            bool actionInterrupt = gunBusy || intent.WantsAds || intent.ReloadPressed;

            // Behind A/D after Cancelable → BackStep (Controller); do not soft-end first.
            bool behind = PlayerJump.IsMoveBehind(intent.Move, _motor.Facing);

            // Crouch is Movable-gated (PlayerArbiter.CanCrouch) — same point run unlocks —
            // so only end the attack for crouch once MovableSheath, not from Cancelable.
            // Sliding itself force-ends via actionInterrupt (gunBusy = !CanMelee, which is
            // now also gated on !CrouchIsSliding) — this covers the plain crouch/stand-up case.
            bool crouchInterrupt = _phase == PlayerMeleePhase.MovableSheath
                && (intent.CrouchPressed || _ctx.IsCrouchBusy);

            // Jump is Cancelable-gated (PlayerArbiter.CanJump uses MeleeLocksActions), so a jump
            // press ends a GROUND attack from ActionCancelable onward — not just after Movable.
            // Grounded-only: jump attacks (already airborne) chain via TickJumpAttackChain on the
            // Slash key, not the Jump key, so this must not touch them.
            bool jumpInterrupt = _ctx.IsGrounded && !behind && (intent.JumpPressed || intent.Jump);

            // Run / general movement still waits for Movable.
            bool moveInterrupt = _phase == PlayerMeleePhase.MovableSheath
                && !behind
                && (Mathf.Abs(intent.Move) > 0.1f || _ctx.OnAir);

            if (actionInterrupt || crouchInterrupt || jumpInterrupt || moveInterrupt)
            {
                EndAttack();
                return true;
            }

            if (!string.IsNullOrEmpty(_activeState) && _anim.IsPlaying(_activeState))
            {
                _anim.SyncCurrent(_activeState);
            }

            return false;
        }

        private void TickSheathEnd()
        {
            bool animDone = !string.IsNullOrEmpty(_activeState)
                && _anim.IsPlaying(_activeState)
                && _anim.BaseFinished;

            if (_elapsed >= _sheathEndAt || animDone)
            {
                // Sheath SEs (SE_Noutou / SE_Noutou2 / SE_NoutouFast) come from the clip's
                // baked animation events → PlayerAudio.SendEvent; no timer-driven SE here.
                EndAttack();
            }
        }

        private void DoGroundCombo(int index)
        {
            index = Mathf.Clamp(index, 1, _settings.maxMeleeCombo);
            string state;
            switch (index)
            {
                case 2:
                    state = PlayerAnimDriver.States.Attack2;
                    break;
                case 3:
                    state = PlayerAnimDriver.States.Attack3;
                    break;
                default:
                    state = PlayerAnimDriver.States.Attack1;
                    break;
            }

            AttackTiming timing = GroundTimings[index];
            BeginAttack(state, timing.Attackable, timing.Cancelable, timing.Movable,
                timing.SheathEnd, index);
            // Attack3 / Attack4 SE is played with E_Katana4 VFX (PlayMelee4), not at windup.
            // combo1 → E_Katana1 (Attack), combo2 → E_Katana2 (Attack2).
            if (index != 3)
            {
                _audio?.PlayKatana(index);
            }

            SpawnComboSlash(index);
        }

        private void BeginAttack(string state, float attackableAt, float cancelableAt,
            float movableAt, float sheathEndAt, int combo)
        {
            _phase = PlayerMeleePhase.WindupLocked;
            _elapsed = 0f;
            _attackableAt = attackableAt;
            _cancelableAt = Mathf.Max(cancelableAt, 0.05f);
            _movableAt = Mathf.Max(movableAt, _cancelableAt);
            _sheathEndAt = Mathf.Max(sheathEndAt, _movableAt);
            _combo = combo;
            _activeState = state;
            _anim.ForcePlay(state);
            // Kill any incoming run/slide momentum so the hit doesn't carry sideways.
            _overrideVx = 0f;
            _hasOverrideVx = true;
            _motor.SetImmediateVelocityX(0f);
        }

        private void EndAttack()
        {
            _phase = PlayerMeleePhase.Idle;
            _activeState = null;
            _combo = 0;
            _hasOverrideVx = false;
            _pendingAttack2Katana3 = false;
            _pendingAttack3Katana4 = false;
            // Keep _pendingMelee4AfterSe — afterslash SE is timed from Time.time and outlives the attack.
        }

        public void Cancel()
        {
            EndAttack();
        }
    }
}
