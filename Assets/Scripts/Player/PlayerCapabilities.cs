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
    /// 每帧能力 / 所有权快照。仅由 <see cref="PlayerArbiter"/> 产出。
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

        /// <summary>Jump.Tick 的 movementLocked（含 ADS）。</summary>
        public bool JumpLocked;

        /// <summary>阻止 Idle/Run/Turn/Stop 动画更新。</summary>
        public bool BlockLocomotionAnim;

        public bool OverrideSpeed;

        public PlayerAnimOwner AnimOwner;
        public PlayerVelocityOwner VelocityOwner;
        public PlayerFacingOwner FacingOwner;
    }

    /// <summary>送入 <see cref="PlayerArbiter.Resolve"/> 的层级标志。</summary>
    public struct PlayerLayerSnapshot
    {
        /// <summary>直到 Movable —— 阻止跑 / 蹲（跳跃改用 <see cref="MeleeLocksActions"/>）。</summary>
        public bool MeleeLocksMovement;
        /// <summary>直到 Cancelable —— 阻止 ADS / 后撤步 / 跳跃。</summary>
        public bool MeleeLocksActions;
        public bool MeleeIsAttacking;
        /// <summary>
        /// 蹲下权威（PlayerCrouch FSM）以单一状态表示。下方布尔访问器由此推导
        /// 仲裁规则——不再维护需同步的并行蹲下标志。
        /// </summary>
        public PlayerCrouchState CrouchState;
        public bool CrouchIsSliding => CrouchState == PlayerCrouchState.Sliding;//=>只读
        /// <summary>进入 / 蹲着 / 滑铲 —— 门控 ADS / 朝向 / 蹲走。</summary>
        public bool CrouchIsBusy => CrouchState == PlayerCrouchState.Entering
            || CrouchState == PlayerCrouchState.Crouching
            || CrouchState == PlayerCrouchState.Sliding
            || CrouchState == PlayerCrouchState.SlideToCrouch;
        /// <summary>起身片段保持；其他动作应立即让出。</summary>
        public bool CrouchIsStandingUp => CrouchState == PlayerCrouchState.StandingUp;
        /// <summary>在蹲下进入片段自身的 Attackable 事件之前——近战不得打断该过渡
        ///（PlayerCrouch.CrouchEnterLocked）。原始标志：取决于 Entering 内已过时间，
        /// 无法仅从 <see cref="CrouchState"/> 推导。</summary>
        public bool CrouchEnterLocked;
        public bool JumpOnAir;
        /// <summary>软落地动画保持；非硬动作锁。</summary>
        public bool JumpLandingLocked;
        /// <summary>Landing_to_Run 直到首次 SE_Run —— 硬锁动作。</summary>
        public bool JumpLandToRunLocksActions;
        /// <summary>仅空中后空翻。</summary>
        public bool JumpIsBackFlipping;
        public bool JumpCanBackFlip;
        /// <summary>BackStep 硬滑行直到 Movable @ 0.3s。</summary>
        public bool BackStepActive;
        /// <summary>BackStep 硬直或软恢复仍在播放。</summary>
        public bool BackStepBusy;
        public bool GunIsAds;
        public bool GunIsBusy;
        /// <summary>Aim_SMG_Reload —— 其他动作可取消 ADS；松开 RMB / A-D 不会。</summary>
        public bool GunIsReloading;
        /// <summary>魔法蓄力 / 施放 / 取消 —— 硬动作锁。</summary>
        public bool MagicBusy;
        public bool LocoTurnLock;
        public bool Grounded;
        public float AbsSmoothedVelocityX;
    }
}
