namespace Player
{
    /// <summary>
    /// Timings taken from AnimationClip SendEvent times / clip lengths
    /// under Assets/AnimationClip (floor heroine). Keep in sync with clips.
    /// </summary>
    public static class PlayerAnimTimings
    {
        public static class Attack1
        {
            /// <summary>Attack.anim SendEvent E_Katana1 — first slash VFX.</summary>
            public const float E_Katana1 = 0.0667f;
            /// <summary>Unlock ADS / backstep (PlayerMelee).</summary>
            public const float Cancelable = 0.25f;
            /// <summary>Combo 1→2 commit (PlayerMelee). Attack.anim SendEvent fires this at the same
            /// time as Movable (0.6667) — was mis-copied as half that (0.3333); fixed to match
            /// the clip so the Movable crouch/run interrupt (checked first in PlayerMelee's
            /// per-frame order) always gets first refusal over combo chaining, same frame.</summary>
            public const float Attackable = 0.6667f;
            /// <summary>Unlock run / crouch / jump (PlayerMelee).</summary>
            public const float Movable = 0.6667f;
            public const float SeNoutou = 1.75f;
            public const float SeNoutou2 = 3.0f;
            public const float ClipLength = 4.0833f;
        }

        public static class Attack2
        {
            /// <summary>Attack2.anim SendEvent E_Katana2 — first slash VFX.</summary>
            public const float E_Katana2 = 0.0333f;
            /// <summary>Attack2.anim SendEvent E_Katana3 — second slash VFX (same prefab as combo-3 swing).</summary>
            public const float E_Katana3 = 0.3167f;
            /// <summary>Unlock ADS / backstep (PlayerMelee).</summary>
            public const float Cancelable = 0.1167f;
            public const float Cancelable2 = 0.4f;
            public const float DoMelee = 0.3333f;
            /// <summary>Combo 2→3 commit (PlayerMelee).</summary>
            public const float Attackable = 0.7667f;
            /// <summary>Unlock run / crouch / jump (PlayerMelee).</summary>
            public const float Movable = 1.3333f;
            public const float ClipLength = 2.3333f;
        }

        public static class Attack3
        {
            public const float Melee4Stop = 0.15f;
            /// <summary>Attack3.anim (Animator state Attack4) SendEvent E_Katana4 —
            /// Effects State 15: Slash4 + Katana4_Smoke + _Melee4AfterSlash.</summary>
            public const float E_Katana4 = 0.1667f;
            /// <summary>
            /// _Melee4AfterSlash FSM State 1 ChronosWait → State 6 AudioPlay(Melee4After).
            /// Measured from E_Katana4 spawn (same as the prefab's 0.5s wait).
            /// </summary>
            public const float Melee4AfterSe = 0.5f;
            /// <summary>Unlock ADS / backstep (PlayerMelee).</summary>
            public const float Cancelable = 0.3333f;
            public const float SeNoutouFast = 1.0167f;
            /// <summary>Unlock run / crouch / jump; Melee INIT (combo reset). Fresh slash starts Attack1.</summary>
            public const float Movable = 1.1667f;
            public const float ClipLength = 2.3833f;
        }

        public static class SlideAttack
        {
            /// <summary>Unlock ADS / backstep (PlayerMelee).</summary>
            public const float Cancelable = 0.35f;
            public const float Attackable = 0.4f;
            /// <summary>Unlock run / crouch / jump (PlayerMelee).</summary>
            public const float Movable = 0.6667f;
            public const float SeNoutou = 1.75f;
            public const float ClipLength = 4.0833f;
        }

        public static class JumpAttackUp
        {
            /// <summary>Unlock ADS / other actions (PlayerMelee).</summary>
            public const float JCancelable = 0.3333f;
            public const float ClipLength = 0.8833f;
        }

        public static class JumpAttackDown
        {
            public const float ClipLength = 0.8f;
        }

        public static class RunTurning
        {
            public const float Runnable = 0.1333f;
            public const float ClipLength = 0.1333f;
        }

        public static class RunToIdle
        {
            public const float Runnable = 0.1333f;
            public const float ClipLength = 1.25f;
        }

        public static class AimSmg
        {
            public const float ClipLength = 0.25f;
        }

        /// <summary>
        /// Crouch_Aim_to_Stand_Aim BlendTree clips (Hold_to_Crouch / Crouch_to_Stand) — 0.2s.
        /// </summary>
        public static class CrouchAimTransition
        {
            public const float ClipLength = 0.2f;
        }

        public static class AimSmgRelease
        {
            public const float SeFoldSmg = 0.5833f;
            public const float ClipLength = 2.3333f;
        }

        public static class AimSmgReload
        {
            /// <summary>Aim_*_SMG_Reload SendEvent SE_OffMagazine.</summary>
            public const float SeOffMagazine = 0.05f;
            /// <summary>Aim_*_SMG_Reload SendEvent SE_SetMagazine.</summary>
            public const float SeSetMagazine = 0.5333f;
            /// <summary>Aim_*_SMG_Reload SendEvent SE_Cocking (退膛 / bolt).</summary>
            public const float SeCocking = 0.8667f;
            public const float Reloaded = 1.0f;
            public const float ReloadDone = 1.3333f;
            public const float ClipLength = 1.3333f;
        }

        public static class BackStep
        {
            public const float Movable = 0.3f;
            public const float ClipLength = 1.2167f;
        }

        public static class Landing
        {
            public const float ClipLength = 1.6667f;
        }

        public static class LandingToRun
        {
            /// <summary>First SE_Run — unlock actions (Landing_to_Run*.anim).</summary>
            public const float SeRun = 0.25f;
            public const float SeRun2 = 0.6667f;
            public const float ClipLength = 0.95f;
        }

        public static class BackFlipLand
        {
            public const float ClipLength = 1.8333f;
        }

        public static class CrouchEnter
        {
            /// <summary>Crouch_Crouch.anim SendEvent Attackable — earliest point melee is allowed
            /// to cut the crouch-enter transition short (PlayerCrouch.CrouchEnterLocked).</summary>
            public const float Attackable = 0.3333f;
            public const float ClipLength = 0.7333f;
        }

        public static class CrouchToIdle
        {
            public const float ClipLength = 0.7333f;
        }

        public static class SlideToIdle
        {
            public const float ClipLength = 0.5333f;
        }

        public static class SlideToCrouch
        {
            public const float ClipLength = 0.6f;
        }

        public static class MagicChannel
        {
            public const float ClipLength = 0.6333f;
        }

        public static class MagicChannelOnAir
        {
            public const float ClipLength = 0.6333f;
        }

        public static class MagicChanneling
        {
            public const float ClipLength = 0.4667f;
        }

        public static class MagicFrontCast
        {
            public const float ClipLength = 1.1f;
        }

        public static class MagicChannelCancel
        {
            public const float ClipLength = 0.6667f;
        }

        public static class MagicChannelCancelOnAir
        {
            public const float ClipLength = 0.6667f;
        }
    }
}
