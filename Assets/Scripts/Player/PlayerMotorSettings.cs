using UnityEngine;

namespace Player
{
    [System.Serializable]
    public class PlayerMotorSettings
    {
        [Header("速度（单位/秒）")]
        public float runSpeed = 20f;
        public float airSpeed = 20f;
        public float adsWalkSpeed = 4f;

        [Header("加速度（移动未使用 — 地面直接写入 vx）")]
        public float groundAcceleration = 220f;
        public float airAcceleration = 220f;
        public float deceleration = 260f;

        [Header("跳跃")]
        public float jumpForce = 40f;
        public float coyoteTime = 0.08f;
        public float jumpBuffer = 0.12f;
        public float jumpTakeoffLock = 0.1f;
        public float airFloatDampTime = 0.12f;
        public float landLockIdle = 1.6667f;
        public float landLockRun = 0.95f;

        [Header("重力 / 下落")]
        public float gravityScale = 12f;
        public float maxFallSpeed = 70f;

        [Header("地面探测（局部脚底）")]
        public Vector2 groundCheckOffset = new Vector2(0f, 0f);
        public float groundCastRadius = 0.22f;
        public float groundCastDistance = 0.35f;
        public LayerMask groundMask = 0;

        [Header("墙壁探测（局部躯干）")]
        public Vector2 wallCheckOffset = new Vector2(0f, 1.5f);
        public float wallCastRadius = 0.3f;
        public float wallCastDistance = 0.7f;

        [Header("后撤步 / 滑铲")]
        public float backStepForce = 20f;
        public float backStepImpulse = 50f;
        public float backStepDuration = 0.3f;
        public float slideForce = 40f;
        public float slideDuration = 0.45f;
        public float runTimeToSlide = 0.15f;

        [Header("后空翻（W + 背后 A/D → Jump State 7）")]
        public float backFlipJumpForce = 30f;
        public float backFlipForce = 30f;
        public float backFlipMinAir = 0.2f;

        [Header("战斗")]
        public float fireInterval = 0.09f;
        public int magazineCapacity = 20;
        public float adsBlendTime = 0.25f;
        public float adsReleaseDuration = 2.3333f;
        public float reloadDuration = 1.3333f;
        public float bulletSpeed = 75f;
        public int bulletDamage = 5;
        public float jumpAttackDownYVelocity = 5f;
        public float meleeComboWindow = 0.45f;
        public int maxMeleeCombo = 3;
        public float meleeLungeSpeed = 0f;

        [Tooltip("受击")]
        public int maxHealth = 200;
        public float invincibleTime = 0.2f;

        // =========================================================
        // ★ 新增：滑铲跳
        // =========================================================
        [Header("滑铲跳")]
        [Tooltip("滑铲跳保留的水平速度倍率（× runSpeed）。")]
        public float slideJumpSpeedMultiplier = 1.2f;

        // =========================================================
        // ★ 新增：越敌跳
        // =========================================================
        [Header("越敌跳（Evade）")]
        [Tooltip("越敌跳垂直初速。")]
        public float evadeForceY = 40f;
        [Tooltip("越敌跳水平速度（朝面向方向）。")]
        public float evadeForceX = 18f;
        [Tooltip("检测附近敌人攻击前摇的半径。")]
        public float evadeDetectRadius = 3.5f;
        [Tooltip("越敌跳动画时长（Jump_Evade = 0.883s）。")]
        public float evadeDuration = 0.883f;
        [Tooltip("无敌窗口起始时刻（第 18 帧 / 60fps ≈ 0.3s）。")]
        public float evadeInvincibleStart = 18f / 60f;
        [Tooltip("无敌窗口结束时刻（第 24 帧 / 60fps = 0.4s）。")]
        public float evadeInvincibleEnd = 24f / 60f;
    }
}