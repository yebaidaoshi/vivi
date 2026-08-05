namespace Player
{
    /// <summary>
    /// 锁定 / 所有权规则的唯一来源。玩法门控变更请集中改这里。
    /// </summary>
    public static class PlayerArbiter
    {
        public const float RunTurnSpeed = 8f;

        public static PlayerCapabilities Resolve(in PlayerLayerSnapshot s)
        {
            var c = new PlayerCapabilities();

            // Idle Landing 为软保持；Landing_to_Run 硬锁直到首次 SE_Run。
            // 近战 Cancelable → ADS/后撤步/跳跃；近战 Movable → 跑/蹲。
            // BackStep：硬直直到其 Movable @ 0.3s；之后为软恢复（可打断）。
            bool backStepHard = s.BackStepActive;
            // 跳跃使用 MeleeLocksActions（由 Cancelable 门控）——挥砍锁一解除即可跳。
            // 蹲下与跑一样由 Movable 门控（见下方 MeleeLocksMovement）。
            bool jumpLocked = s.MeleeLocksActions /*近战正处于“不可取消阶段”*/|| s.CrouchIsSliding//正在滑铲
                || s.JumpIsBackFlipping /*正在后空翻中*/ || backStepHard//后撤步的硬直中
                || s.JumpLandToRunLocksActions//落地冲向跑的过渡锁定中
                || s.MagicBusy;//正在魔法蓄力/施法中

            // 换弹：其他动作可取消 ADS（换弹失败）。否则按住瞄准仍会锁跳跃。
            bool adsLocksActions = s.GunIsAds && !s.GunIsReloading;
            c.JumpLocked = jumpLocked || adsLocksActions;//没瞄准加上前面的条件
            c.CanJump = !c.JumpLocked;
            // 地面 Crouching ADS ↔ ADSCrouch：蹲下与枪 ADS 可叠加（GAME_PICKUP 切换）。
            // 蹲下在 Movable 解锁（与跑同一门控），而非 Cancelable。
            c.CanCrouch = !s.MeleeLocksMovement && !backStepHard
                && !s.JumpLandToRunLocksActions && !s.MagicBusy;//能蹲下的条件是：近战不锁移动 + 没有后撤步硬直 + 没有落地跑锁定 + 没有魔法。
                                                                // 滑铲阻止一切近战（含原先的 Slide_Attack 特例），与下方
                                                                // CanAds/CanEvade/CanMagic 相同——滑铲中不允许任何攻击。
                                                                // CrouchEnterLocked：在蹲下进入片段自身的 Attackable 事件之前，近战不得
                                                                // 打断该过渡（其他打断——松开 S、魔法、后撤步/
                                                                // 后空翻——不受影响，见 PlayerCrouch.CrouchEnterLocked）。
            c.CanMelee = !adsLocksActions && !backStepHard && !s.JumpLandToRunLocksActions
                && !s.MagicBusy && !s.CrouchIsSliding && !s.CrouchEnterLocked;
            // ADS 进入：蹲着时允许；滑铲 / 从滑铲进入仍阻止。
            c.CanAds = !s.MeleeLocksActions && !backStepHard && !s.JumpOnAir
                && !s.CrouchIsSliding
                && !s.JumpLandToRunLocksActions && !s.MagicBusy;
            c.CanEvade = !s.CrouchIsSliding && !adsLocksActions && !backStepHard
                && !s.MeleeLocksActions && !s.JumpLandToRunLocksActions && !s.MagicBusy;
            // 地面 Magic Idle BoolTest GAME_SKILL —— ADS / 近战 / 后撤步 / 落地跑期间阻止。
            c.CanMagic = !adsLocksActions && !s.MeleeLocksActions && !backStepHard
                && !s.CrouchIsSliding && !s.JumpLandToRunLocksActions && !s.MagicBusy;

            bool crouchRolling = s.CrouchState == PlayerCrouchState.SlideToCrouch;
            c.OverrideSpeed = s.CrouchIsSliding || crouchRolling || s.MeleeLocksMovement
                || s.JumpIsBackFlipping || backStepHard || s.MagicBusy;
            c.VelocityOwner = c.OverrideSpeed
                ? PlayerVelocityOwner.ImmediateOverride
                : PlayerVelocityOwner.LocomotionRamp;

            // 软 A_to_B（Crouch_To_Idle / Run_to_Idle / ADS 收枪 / BackStep 恢复）
            // 不得阻止 loco —— Locomotion 软保持片段直到被打断。
            // Idle Landing 软保持仍用 JumpLandingLocked（打断当帧清除）。
            c.BlockLocomotionAnim = s.MeleeIsAttacking || s.GunIsBusy || s.CrouchIsBusy
                || s.JumpOnAir || s.JumpLandingLocked || s.JumpLandToRunLocksActions
                || s.JumpIsBackFlipping || backStepHard || s.MagicBusy;

            c.AnimOwner = ResolveAnimOwner(s);
            c.FacingOwner = ResolveFacingOwner(s);
            // 跑步中的朝向翻转由 Locomotion Turn（GAME_MOVE vs XScale）拥有，
            // 而非自由 UpdateFacing —— 仅在慢速 / 待机转向时保留 CanFlip。
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

            // 仅硬滑行——软恢复让出给 loco / 近战。
            if (s.BackStepActive)
            {
                return PlayerAnimOwner.BackStep;
            }

            if (s.MeleeLocksMovement || s.MeleeIsAttacking)
            {
                return PlayerAnimOwner.Melee;
            }

            // 枪 ADS 拥有 Aim 层；蹲下仅通过动画器浮点 `crouching` 混合。
            if (s.GunIsBusy || s.GunIsAds)
            {
                return PlayerAnimOwner.Gun;
            }

            // StandingUp（Crouch_To_Idle / Slide_To_Idle）为软——loco 拥有该片段。
            if (s.CrouchIsSliding || s.CrouchIsBusy)
            {
                return PlayerAnimOwner.Crouch;
            }

            return PlayerAnimOwner.Locomotion;
        }

        private static PlayerFacingOwner ResolveFacingOwner(in PlayerLayerSnapshot s)
        {
            // 起身 / 待机落地不锁朝向；Landing_to_Run 锁到 SE_Run。
            // 全程攻击（含 MovableSheath）保持朝向，以便背后 A/D 后撤步生效。
            // 蹲+ADS：鼠标瞄准仍拥有朝向（地面 Gun OnADS during ADSCrouch）。
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
