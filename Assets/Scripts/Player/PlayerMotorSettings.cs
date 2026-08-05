using UnityEngine;

namespace Player
{
    /// <summary>
    /// 纯脚本女主角的可调参数。速度/力来自原版
    /// floor.unity 的 PlayMaker FSM（Movement / Jump / VelocityControl / GunFire / Crouching）；
    /// 加速度与探测值在此为独立场景编写。
    /// </summary>
    [System.Serializable]
    public class PlayerMotorSettings
    {
        [Header("速度（单位/秒）")]
        [Tooltip("Movement _XSpeed — Input Ch. 每帧 SetVelocity。")]
        public float runSpeed = 20f;
        [Tooltip("Movement _AirXSpeed — ONAIR 每帧 SetVelocity。")]
        public float airSpeed = 20f;
        [Tooltip("Movement _ADSXSpeed — FaceCheck 每帧 SetVelocity。")]
        public float adsWalkSpeed = 4f;

        [Header("加速度（移动未使用 — 地面直接写入 vx）")]
        [Tooltip("遗留；地面移动不再做加速斜坡。")]
        public float groundAcceleration = 220f;
        [Tooltip("遗留；空中移动不再做加速斜坡。")]
        public float airAcceleration = 220f;
        [Tooltip("遗留；仅部分特殊滑行衰减若存在时使用。")]
        public float deceleration = 260f;

        [Header("跳跃")]
        public float jumpForce = 40f;
        public float coyoteTime = 0.08f;
        public float jumpBuffer = 0.12f;
        [Tooltip("Jump FSM 起飞后 ChronosWait，之后才允许落地。")]
        public float jumpTakeoffLock = 0.1f;
        [Tooltip("OnAir airfloat Animator dampTime（仅倾斜；空中从不翻转网格）。")]
        public float airFloatDampTime = 0.12f;
        [Tooltip("Landing.anim 保持（回退值；Jump 使用 PlayerAnimTimings.Landing）。")]
        public float landLockIdle = 1.6667f;
        [Tooltip("Landing_to_Run 保持（回退值；Jump 使用 PlayerAnimTimings.LandingToRun）。")]
        public float landLockRun = 0.95f;

        [Header("重力 / 下落")]
        [Tooltip("仅在无 Chronos Timeline 驱动时应用到 Rigidbody2D。")]
        public float gravityScale = 12f;
        [Tooltip("向下末端速度钳制（0 = 不钳制）。")]
        public float maxFallSpeed = 70f;

        [Header("地面探测（局部脚底）")]
        public Vector2 groundCheckOffset = new Vector2(0f, 0f);
        public float groundCastRadius = 0.22f;
        public float groundCastDistance = 0.35f;
        [Tooltip("0 = 自动（Ground + GroundCollider 层）。")]
        public LayerMask groundMask = 0;

        [Header("墙壁探测（局部躯干）")]
        public Vector2 wallCheckOffset = new Vector2(0f, 1.5f);
        public float wallCastRadius = 0.3f;
        public float wallCastDistance = 0.7f;

        [Header("后撤步 / 滑铲")]
        [Tooltip("后撤步距离未使用 — 保留以兼容检视器。")]
        public float backStepForce = 20f;
        [Tooltip("Movement BackStep：SetFloat -50 * facingSign → AddForce Impulse。")]
        public float backStepImpulse = 50f;
        [Tooltip("BackStep.anim 的 Movable 事件 — 滑行在此结束（地面）。")]
        public float backStepDuration = 0.3f;
        public float slideForce = 40f;
        public float slideDuration = 0.45f;
        [Tooltip("跑步多少秒后蹲下变为滑铲（Crouching RunningCheck）。")]
        public float runTimeToSlide = 0.15f;//原0.3f太严格 不够丝滑

        [Header("后空翻（W + 背后 A/D → Jump State 7）")]
        [Tooltip("State 7 在 SetVelocity(0,0) 后 AddForce Impulse Y。")]
        public float backFlipJumpForce = 30f;
        [Tooltip("State 7 BackStepForce 大小（-30 * facingSign）→ AddForce X。")]
        public float backFlipForce = 30f;
        [Tooltip("State 7 ChronosWait，之后才做落地检测（State 8）。")]
        public float backFlipMinAir = 0.2f;

        [Header("战斗")]
        public float fireInterval = 0.09f;
        public int magazineCapacity = 20;
        [Tooltip("Aim_Aim_SMG 抬枪片段长度。")]
        public float adsBlendTime = 0.25f;
        [Tooltip("Aim_Aim_SMG_Release 片段长度（SE_foldSMG @ 0.5833）。")]
        public float adsReleaseDuration = 2.3333f;
        [Tooltip("Aim_Aim_SMG_Reload 直到 Reload_Done @ 1.3333。")]
        public float reloadDuration = 1.3333f;
        [Tooltip("_Bullet PlayMaker 飞行速度（SetVelocity）。")]
        public float bulletSpeed = 75f;
        public float jumpAttackDownYVelocity = 5f;
        public float meleeComboWindow = 0.45f;
        public int maxMeleeCombo = 3;
        [Tooltip("未使用：地面近战不再施加前冲（攻击保持原地）。")]
        public float meleeLungeSpeed = 0f;
    }
}
