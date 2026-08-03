namespace Player
{
    /// <summary>
    /// Single source for lock / ownership rules. Keep gameplay gate changes here.
    /// </summary>
    public static class PlayerArbiter
    {
        public const float RunTurnSpeed = 8f;

        public static PlayerCapabilities Resolve(in PlayerLayerSnapshot s)
        {
            var c = new PlayerCapabilities();

            // Idle Landing is soft-hold; Landing_to_Run hard-locks until first SE_Run.
            // Melee Cancelable → ADS/backstep/jump; Melee Movable → run/crouch.
            // BackStep: hard until its Movable @ 0.3s; soft recovery afterward (interruptible).
            bool backStepHard = s.BackStepActive;
            // Jump uses MeleeLocksActions (Cancelable-gated) — jump is allowed as soon as the
            // swing lock lifts. Crouch is Movable-gated (see MeleeLocksMovement below), same as run.
            bool jumpLocked = s.MeleeLocksActions /*近战正处于“不可取消阶段”*/|| s.CrouchIsSliding//正在滑铲
                || s.JumpIsBackFlipping /*正在后空翻中*/ || backStepHard//后撤步的硬直中
                || s.JumpLandToRunLocksActions//落地冲向跑的过渡锁定中
                || s.MagicBusy;//正在魔法蓄力/施法中

            // Reload: other actions may cancel ADS (reload fails). Hold still locks jump otherwise.
            bool adsLocksActions = s.GunIsAds && !s.GunIsReloading;
            c.JumpLocked = jumpLocked || adsLocksActions;//没瞄准加上前面的条件
            c.CanJump = !c.JumpLocked;
            // floor Crouching ADS ↔ ADSCrouch: crouch and gun ADS stack (GAME_PICKUP toggles).
            // Crouch unlocks at Movable (same gate as run), not at Cancelable.
            c.CanCrouch = !s.MeleeLocksMovement && !backStepHard
                && !s.JumpLandToRunLocksActions && !s.MagicBusy;//能蹲下的条件是：近战不锁移动 + 没有后撤步硬直 + 没有落地跑锁定 + 没有魔法。
                                                                // Sliding blocks all melee (incl. the former Slide_Attack special), same as
                                                                // CanAds/CanEvade/CanMagic below — no attack is allowed while sliding.
                                                                // CrouchEnterLocked: before the crouch-enter clip's own Attackable event, melee must
                                                                // not cut the transition short (other interrupts — release S, magic, backstep/
                                                                // backflip — are unaffected, see PlayerCrouch.CrouchEnterLocked).
            c.CanMelee = !adsLocksActions && !backStepHard && !s.JumpLandToRunLocksActions
                && !s.MagicBusy && !s.CrouchIsSliding && !s.CrouchEnterLocked;
            // ADS entry: allow while crouched; slide / enter-from-slide still blocks.
            c.CanAds = !s.MeleeLocksActions && !backStepHard && !s.JumpOnAir
                && !s.CrouchIsSliding
                && !s.JumpLandToRunLocksActions && !s.MagicBusy;
            c.CanEvade = !s.CrouchIsSliding && !adsLocksActions && !backStepHard
                && !s.MeleeLocksActions && !s.JumpLandToRunLocksActions && !s.MagicBusy;
            // floor Magic Idle BoolTest GAME_SKILL — block while ADS / melee / backstep / land-run.
            c.CanMagic = !adsLocksActions && !s.MeleeLocksActions && !backStepHard
                && !s.CrouchIsSliding && !s.JumpLandToRunLocksActions && !s.MagicBusy;

            c.OverrideSpeed = s.CrouchIsSliding || s.MeleeLocksMovement
                || s.JumpIsBackFlipping || backStepHard || s.MagicBusy;
            c.VelocityOwner = c.OverrideSpeed
                ? PlayerVelocityOwner.ImmediateOverride
                : PlayerVelocityOwner.LocomotionRamp;

            // Soft A_to_B (Crouch_To_Idle / Run_to_Idle / ADS release / BackStep recovery)
            // must NOT block loco — Locomotion soft-holds the clip until interrupt.
            // Idle Landing soft-hold still uses JumpLandingLocked (interrupt clears same frame).
            c.BlockLocomotionAnim = s.MeleeIsAttacking || s.GunIsBusy || s.CrouchIsBusy
                || s.JumpOnAir || s.JumpLandingLocked || s.JumpLandToRunLocksActions
                || s.JumpIsBackFlipping || backStepHard || s.MagicBusy;

            c.AnimOwner = ResolveAnimOwner(s);
            c.FacingOwner = ResolveFacingOwner(s);
            // Facing flip while running is owned by Locomotion Turn (GAME_MOVE vs XScale),
            // not free UpdateFacing — keep CanFlip only when slow / idle steer.
            c.CanFlip = c.FacingOwner == PlayerFacingOwner.Locomotion
                && !s.JumpOnAir
                && !s.MeleeLocksMovement
                && !s.CrouchIsBusy
                && s.AbsSmoothedVelocityX <= RunTurnSpeed;
            c.CanMove = !s.MeleeLocksMovement && !s.CrouchIsSliding && !backStepHard
                && !s.JumpIsBackFlipping && !s.MagicBusy;

            return c;
        }

        private static PlayerAnimOwner ResolveAnimOwner(in PlayerLayerSnapshot s)
        {
            if (s.MagicBusy)
            {
                return PlayerAnimOwner.Magic;
            }

            if (s.JumpIsBackFlipping || s.JumpOnAir || s.JumpLandingLocked
                || s.JumpLandToRunLocksActions)
            {
                return PlayerAnimOwner.Jump;
            }

            // Hard coast only — soft recovery yields to loco / melee.
            if (s.BackStepActive)
            {
                return PlayerAnimOwner.BackStep;
            }

            if (s.MeleeLocksMovement || s.MeleeIsAttacking)
            {
                return PlayerAnimOwner.Melee;
            }

            // Gun ADS owns Aim layer; crouch only blends via animator float `crouching`.
            if (s.GunIsBusy || s.GunIsAds)
            {
                return PlayerAnimOwner.Gun;
            }

            // StandingUp (Crouch_To_Idle / Slide_To_Idle) is soft — loco owns the clip.
            if (s.CrouchIsSliding || s.CrouchIsBusy)
            {
                return PlayerAnimOwner.Crouch;
            }

            return PlayerAnimOwner.Locomotion;
        }

        private static PlayerFacingOwner ResolveFacingOwner(in PlayerLayerSnapshot s)
        {
            // Stand-up / idle land do not lock facing; Landing_to_Run locks until SE_Run.
            // Keep facing through full attack (incl. MovableSheath) so behind-A/D backstep works.
            // Crouch+ADS: mouse aim still owns facing (floor Gun OnADS during ADSCrouch).
            if (s.GunIsAds)
            {
                return PlayerFacingOwner.Gun;
            }

            if (s.JumpOnAir || s.JumpLandToRunLocksActions || s.MeleeLocksMovement
                || s.MeleeIsAttacking || s.CrouchIsBusy
                || s.BackStepActive || s.JumpIsBackFlipping || s.MagicBusy)
            {
                return PlayerFacingOwner.Locked;
            }

            return PlayerFacingOwner.Locomotion;
        }
    }
}
