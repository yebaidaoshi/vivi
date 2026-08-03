using UnityEngine;

namespace Player
{
    /// <summary>
    /// Tunables for the pure-script heroine. Speeds/forces come from the original
    /// floor.unity PlayMaker FSMs (Movement / Jump / VelocityControl / GunFire / Crouching);
    /// acceleration & probe values are authored here for the standalone scene.
    /// </summary>
    [System.Serializable]
    public class PlayerMotorSettings
    {
        [Header("Speed (units/sec)")]
        [Tooltip("Movement _XSpeed — Input Ch. SetVelocity every frame.")]
        public float runSpeed = 20f;
        [Tooltip("Movement _AirXSpeed — ONAIR SetVelocity every frame.")]
        public float airSpeed = 20f;
        [Tooltip("Movement _ADSXSpeed — FaceCheck SetVelocity every frame.")]
        public float adsWalkSpeed = 4f;

        [Header("Acceleration (unused for loco — floor writes vx directly)")]
        [Tooltip("Legacy; ground loco no longer ramps.")]
        public float groundAcceleration = 220f;
        [Tooltip("Legacy; air loco no longer ramps.")]
        public float airAcceleration = 220f;
        [Tooltip("Legacy; used only by some special coast fades if any.")]
        public float deceleration = 260f;

        [Header("Jump")]
        public float jumpForce = 40f;
        public float coyoteTime = 0.08f;
        public float jumpBuffer = 0.12f;
        [Tooltip("Jump FSM ChronosWait after takeoff before landing is allowed.")]
        public float jumpTakeoffLock = 0.1f;
        [Tooltip("OnAir airfloat Animator dampTime (lean only; air never flips mesh).")]
        public float airFloatDampTime = 0.12f;
        [Tooltip("Landing.anim hold (fallback; Jump uses PlayerAnimTimings.Landing).")]
        public float landLockIdle = 1.6667f;
        [Tooltip("Landing_to_Run hold (fallback; Jump uses PlayerAnimTimings.LandingToRun).")]
        public float landLockRun = 0.95f;

        [Header("Gravity / fall")]
        [Tooltip("Applied to the Rigidbody2D only when no Chronos Timeline drives it.")]
        public float gravityScale = 12f;
        [Tooltip("Terminal downward speed clamp (0 = no clamp).")]
        public float maxFallSpeed = 70f;

        [Header("Ground probe (local feet)")]
        public Vector2 groundCheckOffset = new Vector2(0f, 0f);
        public float groundCastRadius = 0.22f;
        public float groundCastDistance = 0.35f;
        [Tooltip("0 = auto (Ground + GroundCollider layers).")]
        public LayerMask groundMask = 0;

        [Header("Wall probe (local body)")]
        public Vector2 wallCheckOffset = new Vector2(0f, 1.5f);
        public float wallCastRadius = 0.3f;
        public float wallCastDistance = 0.7f;

        [Header("Backstep / Slide")]
        [Tooltip("Unused for backstep distance — kept for inspector compat.")]
        public float backStepForce = 20f;
        [Tooltip("Movement BackStep: SetFloat -50 * facingSign → AddForce Impulse.")]
        public float backStepImpulse = 50f;
        [Tooltip("BackStep.anim Movable event — coast ends here (floor).")]
        public float backStepDuration = 0.3f;
        public float slideForce = 40f;
        public float slideDuration = 0.45f;
        [Tooltip("Seconds of running required before crouch becomes a slide (Crouching RunningCheck).")]
        public float runTimeToSlide = 0.3f;

        [Header("Backflip (W + behind A/D → Jump State 7)")]
        [Tooltip("State 7 AddForce Impulse Y after SetVelocity(0,0).")]
        public float backFlipJumpForce = 30f;
        [Tooltip("State 7 BackStepForce magnitude (-30 * facingSign) → AddForce X.")]
        public float backFlipForce = 30f;
        [Tooltip("State 7 ChronosWait before land checks (State 8).")]
        public float backFlipMinAir = 0.2f;

        [Header("Combat")]
        public float fireInterval = 0.09f;
        public int magazineCapacity = 20;
        [Tooltip("Aim_Aim_SMG raise clip length.")]
        public float adsBlendTime = 0.25f;
        [Tooltip("Aim_Aim_SMG_Release clip length (SE_foldSMG @ 0.5833).")]
        public float adsReleaseDuration = 2.3333f;
        [Tooltip("Aim_Aim_SMG_Reload until Reload_Done @ 1.3333.")]
        public float reloadDuration = 1.3333f;
        [Tooltip("_Bullet PlayMaker fly speed (SetVelocity).")]
        public float bulletSpeed = 75f;
        public float jumpAttackDownYVelocity = 5f;
        public float meleeComboWindow = 0.45f;
        public int maxMeleeCombo = 3;
        [Tooltip("Unused: ground melee no longer applies a forward lunge (attacks stay planted).")]
        public float meleeLungeSpeed = 0f;
    }
}
