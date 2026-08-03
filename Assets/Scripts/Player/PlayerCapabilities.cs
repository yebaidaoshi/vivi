namespace Player
{
    public enum PlayerAnimOwner
    {
        None,
        Locomotion,
        Jump,
        Crouch,
        Melee,
        BackStep,
        Gun,
        Magic
    }

    public enum PlayerVelocityOwner
    {
        LocomotionRamp,
        ImmediateOverride
    }

    public enum PlayerFacingOwner
    {
        Locomotion,
        Gun,
        Locked
    }

    /// <summary>
    /// Per-frame capability / ownership snapshot. Produced only by <see cref="PlayerArbiter"/>.
    /// </summary>
    public struct PlayerCapabilities
    {
        public bool CanMove;
        public bool CanFlip;
        public bool CanJump;
        public bool CanCrouch;
        public bool CanMelee;
        public bool CanAds;
        public bool CanEvade;
        public bool CanMagic;

        /// <summary>Jump Tick movementLocked (includes ADS).</summary>
        public bool JumpLocked;

        /// <summary>Blocks Idle/Run/Turn/Stop anim updates.</summary>
        public bool BlockLocomotionAnim;

        public bool OverrideSpeed;

        public PlayerAnimOwner AnimOwner;
        public PlayerVelocityOwner VelocityOwner;
        public PlayerFacingOwner FacingOwner;
    }

    /// <summary>Layer flags fed into <see cref="PlayerArbiter.Resolve"/>.</summary>
    public struct PlayerLayerSnapshot
    {
        /// <summary>Until Movable — blocks run / crouch (jump uses <see cref="MeleeLocksActions"/> instead).</summary>
        public bool MeleeLocksMovement;
        /// <summary>Until Cancelable — blocks ADS / backstep / jump.</summary>
        public bool MeleeLocksActions;
        public bool MeleeIsAttacking;
        /// <summary>
        /// Crouch authority (PlayerCrouch FSM) as a single state. The bool accessors below derive
        /// the arbiter rules from it — no more parallel crouch flags to keep in sync.
        /// </summary>
        public PlayerCrouchState CrouchState;
        public bool CrouchIsSliding => CrouchState == PlayerCrouchState.Sliding;//=>只读
        /// <summary>Entering / crouching / sliding — gates ADS / facing / crouch walk.</summary>
        public bool CrouchIsBusy => CrouchState == PlayerCrouchState.Entering
            || CrouchState == PlayerCrouchState.Crouching
            || CrouchState == PlayerCrouchState.Sliding
            || CrouchState == PlayerCrouchState.SlideToCrouch;
        /// <summary>Stand-up clip hold; yield immediately on other actions.</summary>
        public bool CrouchIsStandingUp => CrouchState == PlayerCrouchState.StandingUp;
        /// <summary>Before the crouch-enter clip's own Attackable event — melee may not cut it
        /// short (PlayerCrouch.CrouchEnterLocked). Raw flag: depends on elapsed time within
        /// Entering, not derivable from <see cref="CrouchState"/> alone.</summary>
        public bool CrouchEnterLocked;
        public bool JumpOnAir;
        /// <summary>Soft land-anim hold; not a hard action lock.</summary>
        public bool JumpLandingLocked;
        /// <summary>Landing_to_Run until first SE_Run — hard-locks actions.</summary>
        public bool JumpLandToRunLocksActions;
        /// <summary>Air backflip only.</summary>
        public bool JumpIsBackFlipping;
        public bool JumpCanBackFlip;
        /// <summary>BackStep hard coast until Movable @ 0.3s.</summary>
        public bool BackStepActive;
        /// <summary>BackStep hard or soft recovery still playing.</summary>
        public bool BackStepBusy;
        public bool GunIsAds;
        public bool GunIsBusy;
        /// <summary>Aim_SMG_Reload — other actions may cancel ADS; RMB release / A-D do not.</summary>
        public bool GunIsReloading;
        /// <summary>Magic channel / cast / cancel — hard action lock.</summary>
        public bool MagicBusy;
        public bool LocoTurnLock;
        public bool Grounded;
        public float AbsSmoothedVelocityX;
    }
}
