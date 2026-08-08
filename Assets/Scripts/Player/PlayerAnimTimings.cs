namespace Player
{
    /// <summary>
    /// 时间点取自 Assets/AnimationClip（floor 女主）下 AnimationClip 的
    /// SendEvent 时刻 / 片段长度。请与片段保持同步。
    /// </summary>
    public static class PlayerAnimTimings
    {
        public static class Attack1
        {
            /// <summary>Attack.anim SendEvent E_Katana1 — 第一刀斩击 VFX。</summary>
            public const float E_Katana1 = 0.0667f;
            /// <summary>解锁 ADS / 后撤（PlayerMelee）。</summary>
            public const float Cancelable = 0.25f;
            /// <summary>连招 1→2 提交点（PlayerMelee）。Attack.anim SendEvent 与 Movable
            /// 同一时刻触发（0.6667）——曾被误抄成其一半（0.3333）；已修正为与片段一致，
            /// 使 Movable 的蹲/跑打断（在 PlayerMelee 每帧检查顺序中优先）始终比连招衔接
            /// 先获得否决权，同一帧内生效。</summary>
            public const float Attackable = 0.6667f;
            /// <summary>解锁奔跑 / 蹲下 / 跳跃（PlayerMelee）。</summary>
            public const float Movable = 0.6667f;
            public const float SeNoutou = 1.75f;
            public const float SeNoutou2 = 3.0f;
            public const float ClipLength = 4.0833f;
        }

        public static class Attack2
        {
            /// <summary>Attack2.anim SendEvent E_Katana2 — 第一刀斩击 VFX。</summary>
            public const float E_Katana2 = 0.0333f;
            /// <summary>Attack2.anim SendEvent E_Katana3 — 第二刀斩击 VFX（与连招第 3 下挥砍同预制体）。</summary>
            public const float E_Katana3 = 0.3167f;
            /// <summary>解锁 ADS / 后撤（PlayerMelee）。</summary>
            public const float Cancelable = 0.1167f;
            public const float Cancelable2 = 0.4f;
            public const float DoMelee = 0.3333f;
            /// <summary>连招 2→3 提交点（PlayerMelee）。</summary>
            public const float Attackable = 0.7667f;
            /// <summary>解锁奔跑 / 蹲下 / 跳跃（PlayerMelee）。</summary>
            public const float Movable = 1.3333f;
            public const float ClipLength = 2.3333f;
        }

        public static class Attack3
        {
            public const float Melee4Stop = 0.15f;
            /// <summary>Attack3.anim（Animator 状态 Attack4）SendEvent E_Katana4 —
            /// Effects State 15：Slash4 + Katana4_Smoke + _Melee4AfterSlash。</summary>
            public const float E_Katana4 = 0.1667f;
            /// <summary>
            /// _Melee4AfterSlash FSM State 1 ChronosWait → State 6 AudioPlay(Melee4After)。
            /// 从 E_Katana4 生成起算（与预制体的 0.5s 等待一致）。
            /// </summary>
            public const float Melee4AfterSe = 0.5f;
            /// <summary>解锁 ADS / 后撤（PlayerMelee）。</summary>
            public const float Cancelable = 0.3333f;
            public const float SeNoutouFast = 1.0167f;
            /// <summary>解锁奔跑 / 蹲下 / 跳跃；Melee INIT（连招重置）。新斩击从 Attack1 开始。</summary>
            public const float Movable = 1.1667f;
            public const float ClipLength = 2.3833f;
        }

        public static class SlideAttack
        {
            /// <summary>解锁 ADS / 后撤（PlayerMelee）。</summary>
            public const float Cancelable = 0.35f;
            public const float Attackable = 0.4f;
            /// <summary>解锁奔跑 / 蹲下 / 跳跃（PlayerMelee）。</summary>
            public const float Movable = 0.6667f;
            public const float SeNoutou = 1.75f;
            public const float ClipLength = 4.0833f;
        }

        public static class JumpAttackUp
        {
            /// <summary>解锁 ADS / 其他动作（PlayerMelee）。</summary>
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
        /// Crouch_Aim_to_Stand_Aim BlendTree 片段（Hold_to_Crouch / Crouch_to_Stand）— 0.2s。
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
            /// <summary>Aim_*_SMG_Reload SendEvent SE_OffMagazine。</summary>
            public const float SeOffMagazine = 0.05f;
            /// <summary>Aim_*_SMG_Reload SendEvent SE_SetMagazine。</summary>
            public const float SeSetMagazine = 0.5333f;
            /// <summary>Aim_*_SMG_Reload SendEvent SE_Cocking（退膛 / 拉栓）。</summary>
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
            /// <summary>首次 SE_Run — 解锁动作（Landing_to_Run*.anim）。</summary>
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
            /// <summary>Crouch_Crouch.anim SendEvent Attackable — 近战最早可打断
            /// 蹲下进入过渡的时间点（PlayerCrouch.CrouchEnterLocked）。</summary>
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

        public static class Roll //Slide后接翻滚然后缓慢调整蹲姿
        {
            public const float Cancelable = 0.13333f;
            public const float Movable = 0.4176f;
            public const float ClipLength = 1.5f;

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
        public static class StepForward2
        {
            
            public const float SteppedForward = 8f / 60f;
            
            public const float ClipLength = 22f / 60f;
        }
    }
}
