using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    /// <summary>
    /// ADS / 开火 / 换弹 — 移植自 GunFire FSM + Gun OnADS 瞄准（floor.unity）。
    /// 开火：在 Gun 处 CreateObject _Bullet + MuzzleFlashLight，在抛壳口创建Object SMG_Cartridge。
    /// 瞄准：FollowMouse2D(Aim_Target) + SmoothLookAt2d(AimPivot) + 鼠标越过时翻转朝向。
    /// 移动叠加：Movement ADS 设置 aimDir = abs(facingScale - GAME_MOVE)
    ///   （0 走路 / 1 站立 / 2 后退走），供 Aim 层 Aim_Standing 混合树使用。
    /// </summary>
    // 晚于 BoneFollower / SkeletonUtilityBone（默认 0），换弹时才能读到本帧枪口位姿。
    [DefaultExecutionOrder(1000)]
    public class PlayerGun : MonoBehaviour
    {
        private enum AdsPhase
        {
            Idle,
            Raising,
            Holding,
            Releasing,
            Reloading,
            // 站立瞄准 ↔ 蹲姿瞄准姿势切换（Crouch_Aim_to_Stand_Aim BlendTree）。
            // 瞄准的一等公民子阶段；从 Holding 进入，结束后回到 Holding。
            CrouchAimTransition
        }

        [Header("预制体（GunFire Firing CreateObject）")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject cartridgePrefab;

        [Header("生成点（女主子物体）")]
        [SerializeField] private Transform gunMuzzle;
        [SerializeField] private Transform ejectionPort;

        [Header("鼠标瞄准（Gun FSM OnADS）")]
        [SerializeField] private Transform aimTarget;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private float aimLookSpeed = 12f;
        [SerializeField] private float aimTargetSmoothing = 0.35f;
        [Tooltip("面向鼠标翻转前的世界 X 容差（Gun OnADS FloatCompare）。")]
        [SerializeField] private float faceFlipSlop = 0.15f;

        [Header("瞄准激光（GunSight）")]
        [Tooltip("举枪时绘制红色瞄准射线（floor.unity 中的 GunSight）。")]
        [SerializeField] private bool showAimLaser = true;
        [SerializeField] private Color laserColor = new Color(1f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private float laserWidth = 0.04f;
        [SerializeField] private float laserMaxLength = 30f;
        [Tooltip("阻挡射线的障碍层。为空则绘制全长。")]
        [SerializeField] private LayerMask laserBlockMask;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private AdsPhase _phase = AdsPhase.Idle;
        private float _phaseTimer;
        private float _aimWeight;
        private float _fireCooldown;
        private float _vibrationTimer;
        private int _ammo;
        private bool _playedAimSound;
        private Quaternion _aimPivotRestLocal = Quaternion.identity;
        private bool _aimPivotRestStored;
        private Vector2 _aimDir = Vector2.right;
        /// <summary>换弹开始时锁存的瞄准方向 — 换弹期间枪线 / AimPivot 不再跟随鼠标。</summary>
        private Vector2 _reloadFrozenAimDir = Vector2.right;
        /// <summary>收枪过程中，仅当没有打断时为 true — 允许 Idle/Run/近战切断收枪。</summary>
        private bool _releaseHoldsAnim;
        private LineRenderer _laser;
        private Transform _aimingRoot;
        /// <summary>AimPivot 下的 LaserPointer（SkeletonUtilityBone），换弹时枪线挂点。</summary>
        private Transform _laserPointer;
        private bool _aimPhysicsSuppressed;

        // Aim_*_SMG 状态使用 BlendTree — Unity 不会可靠触发片段 SendEvent。
        // 按与烘焙事件时间匹配的已用时间驱动换弹 / 收枪音效。
        private float _reloadElapsed;
        private bool _reloadSeOffMagazine;
        private bool _reloadSeSetMagazine;
        private bool _reloadSeCocking;
        /// <summary>
        /// 不要把残留的 Aim_SMG_Hold 当成换弹完成（移动 ADS → 换弹曾在首帧
        /// EnterHold，从而跳过 Aim_SMG_Reload）。
        /// </summary>
        private bool _reloadClipSeen;
        private float _releaseElapsed;
        private bool _releaseSeFoldSmg;
        /// <summary>
        /// Mecanim Aim_SMG_Release → Crouching（crouching&gt;0.9，exitTime 1）。一旦已见过
        /// Release 状态，就不要再 ForcePlay — 那一帧的重开就是蹲姿 ADS 取消时的抖动。
        /// </summary>
        private bool _releaseClipSeen;

        /// <summary>
        /// 已提交的 Aim BlendTree 蹲姿（0 站立 / 1 蹲下）。镜像 PlayerCrouch FSM
        /// （作为 <c>crouched</c> 传入）用于进入/持枪/换弹；在收枪开始时锁存，以便即使收枪中途松开蹲键
        /// 也能正确选择蹲姿收枪片段。
        /// PlayerGun 不拥有蹲姿状态 — PlayerCrouch 才是权威。
        /// </summary>
        private bool _aimCrouch;
        /// <summary>本帧的 PlayerCrouch.IsCrouching 镜像（在 Tick 顶部设置）。</summary>
        private bool _crouched;
        private bool _crouchAimTransitionTarget;
        private float _crouchAimTransitionTimer;
        private float _crouchAimBlend;
        private bool _crouchAimClipSeen;

        public bool IsAds => _phase == AdsPhase.Raising || _phase == AdsPhase.Holding
            || _phase == AdsPhase.Reloading
            || _phase == AdsPhase.CrouchAimTransition;
        public bool IsReloading => _phase == AdsPhase.Reloading;
        /// <summary>
        /// 基础层 Aim_SMG*（含收枪）— 蹲姿不得用 PlayBase Crouching 覆盖它。
        /// 与 IsAds 不同，Releasing 时仍为 true，避免蹲姿 ADS → 蹲姿待机抖动。
        /// </summary>
        public bool OwnsCrouchBaseAnim => IsAds
            || (_phase == AdsPhase.Releasing && _releaseHoldsAnim);
        /// <summary>锁定移动动画。收枪可打断，一旦让出则不再锁定。</summary>
        public bool IsBusy => _phase == AdsPhase.Raising || _phase == AdsPhase.Holding
            || _phase == AdsPhase.Reloading
            || _phase == AdsPhase.CrouchAimTransition
            || (_phase == AdsPhase.Releasing && _releaseHoldsAnim);
        public int Ammo => _ammo;
        public Vector2 AimDirection => _aimDir;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            _ammo = _settings.magazineCapacity;
            _anim.SetAimLayerWeight(0f);
            _anim.SetAimDir(1f);
            ResolveRefs();
        }

        private void ResolveRefs()
        {
            if (gunMuzzle == null)
            {
                gunMuzzle = FindChildTransform("Gun");
            }

            if (ejectionPort == null)
            {
                ejectionPort = FindChildTransform("SMG_EjectionPort");
            }

            if (aimTarget == null)
            {
                aimTarget = FindChildTransform("Aim_Target");
            }

            if (aimPivot == null)
            {
                aimPivot = FindChildTransform("AimPivot");
            }

            if (aimPivot != null && !_aimPivotRestStored)
            {
                _aimPivotRestLocal = aimPivot.localRotation;
                _aimPivotRestStored = true;
            }

            // Prefabs/HeroineParent 的 AimPivot 链带非运动学 Rigidbody+HingeJoint，
            // 会与 SkeletonUtilityBone 抢位姿 → _aimDir 每帧抖动（双线明暗不同、子弹左右打）。
            // GameObject/HeroineParent（Player 场景）无此物理，故正常。
            if (!_aimPhysicsSuppressed && aimPivot != null)
            {
                SuppressAimHierarchyPhysics(aimPivot);
                _aimPhysicsSuppressed = true;
            }

            if (_aimingRoot == null)
            {
                _aimingRoot = FindChildTransform("Aiming");
            }

            if (_laserPointer == null)
            {
                _laserPointer = FindChildTransform("LaserPointer");
            }

            // 初始化时挂到 Aiming 下，使层级与 floor GunSight 放置一致。
            if (showAimLaser)
            {
                EnsureLaser();
            }

#if UNITY_EDITOR
            if (bulletPrefab == null)
            {
                bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/_Bullet.prefab");
            }

            if (muzzleFlashPrefab == null)
            {
                muzzleFlashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/MuzzleFlashLight.prefab");
            }

            if (cartridgePrefab == null)
            {
                cartridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/SMG_Cartridge.prefab");
            }
#endif
        }

        private Transform FindChildTransform(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>
        /// 关掉瞄准骨骼树上的铰链物理，只保留 SkeletonUtilityBone / BoneFollower 驱动。
        /// </summary>
        private static void SuppressAimHierarchyPhysics(Transform aimPivotRoot)
        {
            var hinges = aimPivotRoot.GetComponentsInChildren<HingeJoint>(true);
            for (int i = 0; i < hinges.Length; i++)
            {
                if (hinges[i] != null)
                {
                    Destroy(hinges[i]);
                }
            }

            var bodies = aimPivotRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                {
                    continue;
                }

                rb.isKinematic = true;
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.detectCollisions = false;
            }
        }

        /// <param name="actionInterrupt">
        /// 近战 / 跳跃 / 蹲下 / 后撤已占用角色 — 中止收枪动画。
        /// </param>
        /// <param name="crouched">PlayerCrouch.IsCrouching — 枪械镜像的蹲姿权威。</param>
        public void Tick(PlayerIntent intent, bool canAds, bool actionInterrupt, bool sliding,
            bool allowFaceFlip, bool crouched)
        {
            _crouched = crouched;

            if (_fireCooldown > 0f)
            {
                _fireCooldown -= _motor.DeltaTime;
            }

            if (_vibrationTimer > 0f)
            {
                _vibrationTimer -= _motor.DeltaTime;
            }

            if (_phase == AdsPhase.Reloading)
            {
                TickReloading(intent, allowFaceFlip, actionInterrupt, sliding);
            }
            else
            {
                // 通过 canAds 进入；已在瞄准时，打断条件与原先的 actionInterrupt 一致。
                bool wantAds = intent.WantsAds
                    && (IsAds ? !actionInterrupt && !sliding : canAds);

                switch (_phase)
                {
                    case AdsPhase.Idle:
                        TickIdle(intent, wantAds);
                        break;

                    case AdsPhase.Raising:
                        TickRaising(intent, wantAds, allowFaceFlip);
                        break;

                    case AdsPhase.Holding:
                        TickHolding(intent, wantAds, allowFaceFlip);
                        break;

                    case AdsPhase.Releasing:
                        TickRelease(intent, actionInterrupt, sliding, wantAds);
                        break;

                    case AdsPhase.CrouchAimTransition:
                        TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                        break;
                }
            }

            // 枪线改在 LateUpdate：等 Spine BoneFollower / UtilityBone 写完枪口后再画。
        }

        private void LateUpdate()
        {
            UpdateAimLaser();
        }

        /// <summary>
        /// 换弹忽略 RMB 松开与 A/D。近战 / 跳跃 / 闪避 / 魔法 / 滑铲 / 蹲下
        /// 会取消 ADS 并使换弹失败（弹药不变）。
        /// 移动站立保持 Aim_Standing（走路腿部 + GunSight，floor ADS_RELOAD）；静止/蹲姿
        /// 关闭 Aim 层，以便显示基础层 Aim_SMG_Reload 身体动画。
        /// </summary>
        private void TickReloading(PlayerIntent intent, bool allowFaceFlip,
            bool actionInterrupt, bool sliding)
        {
            // 蹲姿换弹中途起身 → 让出，使 PlayerCrouch 能 BeginStandUp，与
            // TickRelease 的 (_aimCrouch && !intent.Crouch) 检查相同。若无此逻辑，PlayerCrouch
            // 已在 adsActive 下 ExitCrouch 切到 Standing（与 ADS-Holding 情况一样，
            // 把清理动画器的工作交给枪），而枪仍把基础层停在 Aim_SMG_Reload 若干帧，
            // 同帧 BackStep（HandleReloadBackStep）硬取消时会留下过期的 crouch=true
            //（PlayerCrouch 一旦 Standing，TickStanding 不会再清除它）。
            if (_aimCrouch && !intent.Crouch)
            {
                CancelReload(keepCrouch: false);
                _anim.SetCrouch(false);
                return;
            }

            bool hardCancel = actionInterrupt || sliding
                || intent.SlashPressed || intent.JumpPressed
                || intent.EvadePressed || intent.CrouchPressed;
            if (hardCancel)
            {
                CancelReload(keepCrouch: intent.Crouch && _aimCrouch);
                return;
            }

            float dt = _motor.DeltaTime;
            _phaseTimer -= dt;
            _reloadElapsed += dt;
            TickReloadSe();
            _aimCrouch = _crouched;
            _anim.SetCrouch(_aimCrouch);
            // 换弹：只更新走路/站立 locomotion aimDir；冻结枪线与 AimPivot，不跟鼠标。
            UpdateReloadAimLocomotion(intent);

            // 移动站立：保持 Aim_Standing（走路腿部 + GunSight 线）。floor Movement
            // State 4（ADS_RELOAD）把 Aim 层留在 1 — 走路换弹仅有音频。
            // 静止 / 蹲姿：降到基础层 Aim_SMG_Reload 以显示换弹身体动画。
            bool moving = !_aimCrouch && Mathf.Abs(intent.Move) > 0.1f;
            bool onReload;
            if (moving)
            {
                SetAimWeightImmediate(1f);
                ParkBaseSmgHold();
                onReload = true;
                _reloadClipSeen = true;
            }
            else
            {
                SetAimWeightImmediate(0f);
                onReload = _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgReload);
                if (onReload)
                {
                    _reloadClipSeen = true;
                    _anim.SyncAim(PlayerAnimDriver.States.AimSmgReload);
                }
                else if (!_reloadClipSeen && _reloadElapsed < 0.12f)
                {
                    // 仅短暂再辅助。Mecanim 过渡中每帧 ForcePlay
                    // 会把片段重置到 t=0，看起来像「换弹从未播放」。
                    _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgReload);
                }
            }

            // 仅在 Reload 实际播放后才结束 — 绝不用移动 ADS 残留的 Hold。
            float minPlay = PlayerAnimTimings.AimSmgReload.ClipLength * 0.85f;
            bool finished = _phaseTimer <= 0f
                || (_reloadClipSeen && onReload && _anim.AimFinished && _reloadElapsed >= minPlay)
                || (_reloadClipSeen && !onReload && _reloadElapsed >= minPlay);
            if (finished)
            {
                _ammo = _settings.magazineCapacity;
                TickReloadSe(forceFlush: true);
                _reloadClipSeen = false;
                EnterHold(forcePlay: true);
            }
        }

        /// <summary>中止换弹且不装填 — 为打断动作离开 ADS。</summary>
        private void CancelReload(bool keepCrouch)
        {
            _reloadSeOffMagazine = false;
            _reloadSeSetMagazine = false;
            _reloadSeCocking = false;
            _reloadClipSeen = false;
            FinishRelease(keepCrouch);
        }

        /// <summary>
        /// 与 Aim_*_SMG_Reload 事件匹配的定时换弹音效（BlendTree 会跳过动画 SendEvent）。
        /// </summary>
        private void TickReloadSe(bool forceFlush = false)
        {
            if (_audio == null)
            {
                return;
            }

            float t = forceFlush ? float.MaxValue : _reloadElapsed;
            if (!_reloadSeOffMagazine && t >= PlayerAnimTimings.AimSmgReload.SeOffMagazine)
            {
                _reloadSeOffMagazine = true;
                _audio.PlayMagazineOut();
            }

            if (!_reloadSeSetMagazine && t >= PlayerAnimTimings.AimSmgReload.SeSetMagazine)
            {
                _reloadSeSetMagazine = true;
                _audio.PlaySetMagazine();
            }

            if (!_reloadSeCocking && t >= PlayerAnimTimings.AimSmgReload.SeCocking)
            {
                _reloadSeCocking = true;
                _audio.PlayCocking();
            }
        }

        private void TickIdle(PlayerIntent intent, bool wantAds)
        {
            if (wantAds)
            {
                _aimCrouch = _crouched;
                EnterRaise();
                _anim.SetCrouch(_aimCrouch);
            }
            else if (intent.ReloadPressed && _ammo < _settings.magazineCapacity)
            {
                StartReload();
            }
            else
            {
                BlendAimWeight(0f);
                ResetAimPose();
                _anim.SetAimDir(1f);
            }
        }

        private void TickRaising(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            // 举枪期间，立即对齐蹲姿混合（过渡片段从 Hold 播放）。
            _aimCrouch = _crouched;
            _anim.SetCrouch(_aimCrouch);
            // 举枪使用基础层 Aim_SMG — 保持 Aim 层关闭，以免 Aim_Standing 盖住它。
            SetAimWeightImmediate(0f);
            UpdateAdsAim(intent, allowFaceFlip);
            _phaseTimer -= _motor.DeltaTime;
            if (!wantAds)
            {
                EnterRelease();
            }
            else if (_phaseTimer <= 0f || _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold)
                || (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmg) && _anim.AimFinished))
            {
                EnterHold(forcePlay: false);
            }
        }

        private void TickHolding(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            if (!wantAds)
            {
                EnterRelease();
                return;
            }

            // 站立 Hold ↔ 蹲姿 Hold：交给 CrouchAimTransition 阶段，播放
            // Crouch_Aim_to_Stand_Aim，片段完成后回到 Holding。
            bool wantCrouch = _crouched;
            if (wantCrouch != _aimCrouch && _vibrationTimer <= 0f)
            {
                BeginCrouchAimTransition(wantCrouch);
                TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                return;
            }

            _anim.SetCrouch(_aimCrouch);
            UpdateAdsAim(intent, allowFaceFlip);

            // 站立 + 移动（且非后坐力中）：Aim 层 Aim_Standing（走/站/退）。
            // 静止 / 蹲姿 / 开火：基础层 SMG，以便 Aim_SMG_Hold + 开火振动后坐力显示。
            // `moving` 跨射击保持稳定（非每帧 ForcePlay）→ 无移动射击抖动。
            bool moving = !_aimCrouch && _vibrationTimer <= 0f
                && Mathf.Abs(intent.Move) > 0.1f;
            if (moving)
            {
                SetAimWeightImmediate(1f);
                ParkBaseSmgHold();
            }
            else
            {
                SetAimWeightImmediate(0f);
                TickHoldBaseSmg();
            }

            if (intent.ReloadPressed && _ammo < _settings.magazineCapacity)
            {
                StartReload();
                return;
            }

            if (intent.Fire && _fireCooldown <= 0f)
            {
                // 仅在非走路时后坐力 — 振动盖在 Aim_Standing 上会闪烁。
                TryFire(allowRecoil: !moving);
            }
        }

        /// <summary>蹲姿持枪：驱动基础层 Aim_SMG_Hold / 从振动返回。</summary>
        private void TickHoldBaseSmg()
        {
            if (_vibrationTimer > 0f)
            {
                if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgVibration))
                {
                    _anim.SyncAim(PlayerAnimDriver.States.AimSmgVibration);
                }

                return;
            }

            if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgVibration))
            {
                if (_anim.AimFinished)
                {
                    _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
                }

                return;
            }

            if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold))
            {
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgHold);
            }
            else if (!_anim.IsPlayingAim(PlayerAnimDriver.States.CrouchAimToStandAim))
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
            }
        }

        /// <summary>
        /// 站立持枪把基础层停在 Aim_Standing 下，避免每帧 ForcePlay（防止重启抖动）。
        /// </summary>
        private void ParkBaseSmgHold()
        {
            if (_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold)
                || _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgVibration))
            {
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgHold);
                return;
            }

            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
        }

        /// <summary>
        /// SMG 状态 Crouch_Aim_to_Stand_Aim BlendTree：
        /// crouching=0 → Aim_Aim_SMG_Hold_to_Crouch_Aim（站→蹲），
        /// crouching=1 → Crouch_Crouch_Aim_to_Stand_Aim（蹲→站）。
        /// 整段片段权重锁定为源姿势。
        /// </summary>
        private void BeginCrouchAimTransition(bool toCrouch)
        {
            _phase = AdsPhase.CrouchAimTransition;
            _crouchAimTransitionTarget = toCrouch;
            _crouchAimClipSeen = false;
            _crouchAimTransitionTimer = PlayerAnimTimings.CrouchAimTransition.ClipLength + 0.05f;
            // 源姿势选择片段 — 不是目标姿势（目标会把两者反转）。
            _crouchAimBlend = toCrouch ? 0f : 1f;
            _anim.SetCrouchingWeight(_crouchAimBlend);
            _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
        }

        private void TickCrouchAimTransition(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            // 切换中途松开 ADS / 被打断：放弃过渡并收枪。
            if (!wantAds)
            {
                EnterRelease();
                return;
            }

            UpdateAdsAim(intent, allowFaceFlip);
            SetAimWeightImmediate(0f);
            // 整段持续期间把 BlendTree 锁在正确的过渡片段上。
            _anim.SetCrouchingWeight(_crouchAimBlend);
            _crouchAimTransitionTimer -= _motor.DeltaTime;

            bool playing = _anim.IsPlayingAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            if (playing)
            {
                _crouchAimClipSeen = true;
                _anim.SyncAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }
            else if (!_crouchAimClipSeen)
            {
                // 首帧 Play 可能未生效 — 持续 Force 直到 Mecanim 报告该状态。
                _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }

            // 按时长锁定：不要用上一 Hold 姿势的 AimFinished。
            float minPlay = PlayerAnimTimings.CrouchAimTransition.ClipLength * 0.85f;
            float elapsed = PlayerAnimTimings.CrouchAimTransition.ClipLength + 0.05f
                - _crouchAimTransitionTimer;
            bool finished = _crouchAimTransitionTimer <= 0f
                || (_crouchAimClipSeen && playing && _anim.AimFinished && elapsed >= minPlay)
                || (_crouchAimClipSeen && !playing && elapsed >= minPlay);
            if (!finished)
            {
                return;
            }

            _aimCrouch = _crouchAimTransitionTarget;
            _anim.SetCrouch(_aimCrouch);
            EnterHold(forcePlay: true);
        }

        /// <summary>
        /// 播放 Aim_SMG_Release 至完成，除非移动 / 战斗 / 跳跃 / 蹲下 / ADS 切断。
        /// 蹲姿时保持 crouching=1，以便播放 Crouch_Crouch_Aim_SMG_Release。动画器已会
        /// 从 Release → Crouching；退出后不要再 Play Release（取消抖动）。
        /// </summary>
        private void TickRelease(PlayerIntent intent, bool actionInterrupt, bool sliding, bool wantAds)
        {
            BlendAimWeight(0f);
            ResetAimPose();
            float dt = _motor.DeltaTime;
            _phaseTimer -= dt;
            _releaseElapsed += dt;
            TickReleaseSe();

            if (wantAds)
            {
                EnterRaise();
                return;
            }

            // 蹲姿 ADS 收枪中起身 → 让出，使 PlayerCrouch 能 BeginStandUp。
            // PlayerCrouch 的 ExitCrouch(adsActive) 切到 Standing 且不碰动画器
            //（ADS 占用基础动画时 defer 给枪）— 在此清除 crouch/crouching，
            // 以免留下过期的 crouch=true / crouching=1（PlayerCrouch.TickStanding
            // 一旦已是 Standing 不会再清除）。
            if (_aimCrouch && !intent.Crouch)
            {
                FinishRelease(keepCrouch: false);
                _anim.SetCrouch(false);
                return;
            }

            // 站立收枪 + 蹲下（按住或按下）：让出，以便播放进入蹲姿。
            if (!_aimCrouch && intent.Crouch)
            {
                FinishRelease(keepCrouch: false);
                return;
            }

            // 蹲姿 ADS 收枪：忽略 A/D（无侧移）。站立收枪仍对移动让出。
            bool inputInterrupt = actionInterrupt || sliding
                || intent.SlashPressed || intent.JumpPressed
                || intent.EvadePressed || intent.ReloadPressed
                || (!_aimCrouch && Mathf.Abs(intent.Move) > 0.1f);

            if (inputInterrupt)
            {
                // actionInterrupt 为 true 时，表明近战/跳跃/后撤步已声明基础层所有权
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch, skipBaseAnim: actionInterrupt);
                return;
            }

            _releaseHoldsAnim = true;
            _anim.SetCrouch(_aimCrouch);
            bool onRelease = _anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgRelease);
            bool onCrouching = _anim.IsPlaying(PlayerAnimDriver.States.Crouching);
            if (onRelease)
            {
                _releaseClipSeen = true;
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgRelease);
            }
            else if (!_releaseClipSeen && _phaseTimer > 0f && !onCrouching)
            {
                // 仅前几帧 — Mecanim 可能尚未报告该状态。
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);
                onRelease = true;
            }
            else if (_releaseClipSeen && !onRelease)
            {
                // exitTime 交接（Crouching）或 SMG Exit — 落定，不要重启 Release。
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
                return;
            }

            if ((onRelease && _anim.AimFinished) || _phaseTimer <= 0f || onCrouching)
            {
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
            }
        }

        /// <summary>SE_foldSMG @ 0.5833 — 同一 Cocking.ogg（BlendTree 跳过动画 SendEvent）。</summary>
        private void TickReleaseSe()
        {
            if (_audio == null || _releaseSeFoldSmg)
            {
                return;
            }

            if (_releaseElapsed >= PlayerAnimTimings.AimSmgRelease.SeFoldSmg)
            {
                _releaseSeFoldSmg = true;
                _audio.PlayCocking();
            }
        }

        private void FinishRelease(bool keepCrouch = false, bool skipBaseAnim = false)
        {
            _phase = AdsPhase.Idle;
            _releaseHoldsAnim = false;
            _releaseClipSeen = false;
            _anim.SetAiming(false);
            SetAimWeightImmediate(0f);
            _anim.SetAimDir(1f);
            if (keepCrouch)
            {
                _anim.SetCrouch(true);

                if (!skipBaseAnim)
                {
                    if (_anim.IsPlaying(PlayerAnimDriver.States.Crouching))
                    {
                        _anim.SyncCurrent(PlayerAnimDriver.States.Crouching);
                    }
                    else
                    {
                        _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
                    }
                }
            }
            _aimCrouch = false;
        }

        /// <summary>
        /// 换弹期间：仅刷动画 locomotion aimDir；不跟鼠标转 AimPivot。
        /// 枪线仍在 LateUpdate 用世界空间绘制（起点/朝向读枪口骨骼）。
        /// </summary>
        private void UpdateReloadAimLocomotion(PlayerIntent intent)
        {
            float move = _aimCrouch ? 0f : intent.Move;
            float aimLocomotion = Mathf.Abs(_motor.Facing - move);
            _anim.SetAimDir(aimLocomotion);
            _aimDir = _reloadFrozenAimDir.sqrMagnitude > 0.0001f
                ? _reloadFrozenAimDir.normalized
                : new Vector2(_motor.Facing, 0f);
        }

        /// <summary>
        /// Movement FaceCheck：aimDir = abs(facingSign - GAME_MOVE) → 0 走 / 1 站 / 2 退。
        /// Gun OnADS：Aim_Target 跟随鼠标；AimPivot SmoothLookAt2d；鼠标越过时翻转。
        /// </summary>
        private void UpdateAdsAim(PlayerIntent intent, bool allowFaceFlip)
        {
            ResolveRefs();

            // Movement FaceCheck：aimDir = abs(facing - move) → 0 走 / 1 站 / 2 退。
            // Crouch / ADSCrouch：忽略 A/D — 锁定站立 aimDir（_aimCrouch 镜像蹲姿 FSM）。
            float move = _aimCrouch ? 0f : intent.Move;
            float aimLocomotion = Mathf.Abs(_motor.Facing - move);
            _anim.SetAimDir(aimLocomotion);

            if (!intent.HasAimPoint)
            {
                _aimDir = new Vector2(_motor.Facing, 0f);
                return;
            }

            Vector2 aimPoint = intent.AimPoint;
            Vector2 origin = aimPivot != null
                ? (Vector2)aimPivot.position
                : (Vector2)transform.position;

            if (aimTarget != null)
            {
                Vector3 cur = aimTarget.position;
                Vector3 target = new Vector3(aimPoint.x, aimPoint.y, cur.z);
                float t = Mathf.Clamp01(aimTargetSmoothing);
                // FollowMouse2D 每帧用 Lerp，smoothing 作为混合系数。
                aimTarget.position = Vector3.Lerp(cur, target, t <= 0f ? 1f : t);
                aimPoint = aimTarget.position;
            }

            Vector2 toAim = aimPoint - origin;
            if (toAim.sqrMagnitude > 0.0001f)
            {
                _aimDir = toAim.normalized;
            }
            else
            {
                _aimDir = new Vector2(_motor.Facing, 0f);
            }

            if (aimPivot != null)
            {
                float angle = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg;
                // 朝左时 localScale.x 为负；补偿使枢轴在世界空间仍指向鼠标
                //（与 Gun Turn + SmoothLookAt2d 思路相同）。
                Quaternion desired = Quaternion.Euler(0f, 0f, angle);
                float step = Mathf.Clamp01(aimLookSpeed * _motor.DeltaTime);
                aimPivot.rotation = Quaternion.Slerp(aimPivot.rotation, desired, step);
            }

            // 仅当 FacingOwner 为 Gun（地面 ADS）时面向鼠标。
            if (!allowFaceFlip || !_motor.IsGrounded)
            {
                return;
            }

            float dx = aimPoint.x - transform.position.x;
            if (dx > faceFlipSlop && _motor.Facing < 0)
            {
                _motor.ForceFacing(1);
            }
            else if (dx < -faceFlipSlop && _motor.Facing > 0)
            {
                _motor.ForceFacing(-1);
            }
        }

        /// <summary>
        /// GunSight 红色射线：Holding 跟鼠标；Reloading 起点跟枪口、方向用枪口朝向（不跟鼠标）。
        /// 始终一条世界空间 LineRenderer，不挂到骨骼上（避免残留双线）。
        /// </summary>
        private void UpdateAimLaser()
        {
            if (!showAimLaser)
            {
                if (_laser != null)
                {
                    _laser.enabled = false;
                }

                return;
            }

            EnsureLaser();

            bool visible = (_phase == AdsPhase.Holding || _phase == AdsPhase.Reloading)
                && _anim != null && _anim.IsInSmgAim();
            _laser.enabled = visible;
            if (!visible)
            {
                return;
            }

            ResolveRefs();

            Transform originT = gunMuzzle != null
                ? gunMuzzle
                : (_laserPointer != null ? _laserPointer : (aimPivot != null ? aimPivot : transform));
            Vector3 originPos = originT.position;
            Vector2 origin = originPos;

            Vector2 dir;
            if (_phase == AdsPhase.Reloading)
            {
                // 换弹：不跟鼠标。朝向优先枪口骨骼，但角色靠 localScale.x 翻转时
                // transform.right 会指反，需与冻结瞄准 / Facing 对齐。
                Vector2 frozen = _reloadFrozenAimDir.sqrMagnitude > 0.0001f
                    ? _reloadFrozenAimDir.normalized
                    : new Vector2(_motor != null ? _motor.Facing : 1, 0f);
                Vector2 muzzleDir = new Vector2(originT.right.x, originT.right.y);
                if (muzzleDir.sqrMagnitude > 0.0001f)
                {
                    muzzleDir.Normalize();
                    if (Vector2.Dot(muzzleDir, frozen) < 0f)
                    {
                        muzzleDir = -muzzleDir;
                    }

                    dir = muzzleDir;
                }
                else
                {
                    dir = frozen;
                }
            }
            else if (_aimDir.sqrMagnitude > 0.0001f)
            {
                dir = _aimDir.normalized;
            }
            else
            {
                dir = new Vector2(_motor.Facing, 0f);
            }

            Vector2 end = origin + dir * laserMaxLength;
            if (laserBlockMask.value != 0)
            {
                var hit = Physics2D.Raycast(origin, dir, laserMaxLength, laserBlockMask);
                if (hit.collider != null && !hit.collider.isTrigger
                    && hit.collider.transform != transform
                    && !hit.collider.transform.IsChildOf(transform))
                {
                    end = hit.point;
                }
            }

            _laser.useWorldSpace = true;
            _laser.startWidth = laserWidth;
            _laser.endWidth = laserWidth;
            _laser.SetPosition(0, new Vector3(origin.x, origin.y, originPos.z));
            _laser.SetPosition(1, new Vector3(end.x, end.y, originPos.z));
        }

        private void EnsureLaser()
        {
            if (_aimingRoot == null)
            {
                _aimingRoot = FindChildTransform("Aiming");
            }

            // 全场景只保留一条 GunSightLaser：优先复用，再删多余。
            // （挂在骨骼/铰链链下的残留不在 Heroine 子层级时，GetComponentsInChildren 会漏掉。）
            LineRenderer kept = _laser;
#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var existing = Object.FindObjectsOfType<LineRenderer>(true);
#endif
            for (int i = 0; i < existing.Length; i++)
            {
                var lr = existing[i];
                if (lr == null || lr.name != "GunSightLaser")
                {
                    continue;
                }

                if (kept == null)
                {
                    kept = lr;
                    continue;
                }

                if (lr != kept)
                {
                    Destroy(lr.gameObject);
                }
            }

            _laser = kept;
            if (_laser != null)
            {
                // 不要挂在角色下：Heroine localScale.x 翻转时，部分管线会把
                // 同一条 LineRenderer 画成“双线”观感；世界根 + useWorldSpace 即可。
                if (_laser.transform.parent != null)
                {
                    _laser.transform.SetParent(null, true);
                }

                _laser.useWorldSpace = true;
                return;
            }

            var go = new GameObject("GunSightLaser");
            go.transform.SetParent(null, false);
            _laser = go.AddComponent<LineRenderer>();
            _laser.useWorldSpace = true;
            _laser.positionCount = 2;
            _laser.numCapVertices = 1;
            _laser.textureMode = LineTextureMode.Stretch;
            _laser.alignment = LineAlignment.View;
            _laser.receiveShadows = false;
            _laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laser.startWidth = laserWidth;
            _laser.endWidth = laserWidth;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _laser.material = new Material(shader);
            }

            _laser.startColor = laserColor;
            _laser.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0f);
            _laser.sortingOrder = 100;
            _laser.enabled = false;
        }

        private void OnDestroy()
        {
            if (_laser != null)
            {
                Destroy(_laser.gameObject);
                _laser = null;
            }
        }

        private void ResetAimPose()
        {
            if (aimPivot != null && _aimPivotRestStored)
            {
                aimPivot.localRotation = Quaternion.Slerp(
                    aimPivot.localRotation,
                    _aimPivotRestLocal,
                    Mathf.Clamp01(aimLookSpeed * _motor.DeltaTime));
            }

            _aimDir = new Vector2(_motor != null ? _motor.Facing : 1, 0f);
        }

        private void EnterRaise()
        {
            _phase = AdsPhase.Raising;
            _phaseTimer = _settings.adsBlendTime > 0f ? _settings.adsBlendTime : 0.25f;
            _anim.SetAiming(true);
            _anim.SetAimDir(1f);
            // 基础层/SMG 拥有姿势；保持 Aim 层关闭，以免 Aim_Standing 覆盖。
            SetAimWeightImmediate(0f);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmg);
            if (!_playedAimSound)
            {
                _audio?.PlayAiming();
                _playedAimSound = true;
            }
        }

        private void EnterHold(bool forcePlay)
        {
            _phase = AdsPhase.Holding;
            _anim.SetAiming(true);
            _anim.SetCrouch(_aimCrouch);
            // 默认基础层 SMG（权重 0）。TickHolding 仅在走路时把 Aim 层提到 1，
            // 因此静止进入持枪显示基础层瞄准姿势（而非一帧走路）。
            SetAimWeightImmediate(0f);

            if (forcePlay || !_anim.IsPlayingAim(PlayerAnimDriver.States.AimSmgHold))
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgHold);
            }
            else
            {
                _anim.SyncAim(PlayerAnimDriver.States.AimSmgHold);
            }
        }

        private void EnterRelease()
        {
            _phase = AdsPhase.Releasing;
            // Aim_Aim_SMG_Release 片段长度；移动/攻击/跳跃可能提前切断。
            _phaseTimer = _settings.adsReleaseDuration > 0f
                ? _settings.adsReleaseDuration
                : PlayerAnimTimings.AimSmgRelease.ClipLength;
            _releaseHoldsAnim = true;
            _playedAimSound = false;
            _vibrationTimer = 0f;
            _releaseElapsed = 0f;
            _releaseSeFoldSmg = false;
            _releaseClipSeen = false;
            _anim.SetCrouch(_aimCrouch);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);
        }

        private void BlendAimWeight(float target)
        {
            float blend = _settings.adsBlendTime > 0f
                ? _motor.DeltaTime / _settings.adsBlendTime
                : 1f;
            _aimWeight = Mathf.MoveTowards(_aimWeight, target, blend);
            _anim.SetAimLayerWeight(_aimWeight);
        }

        private void SetAimWeightImmediate(float weight)
        {
            _aimWeight = weight;
            _anim.SetAimLayerWeight(weight);
        }

        private void TryFire(bool allowRecoil = true)
        {
            if (_ammo <= 0)
            {
                StartReload();
                return;
            }

            _ammo--;
            _fireCooldown = _settings.fireInterval;
            _anim.SetTrigger("gunfire");
            // 后坐力 = 基础层 Aim_SMG_vibration 且 Aim 层关闭（蹲姿，或站立静止）。
            // 走路时（allowRecoil=false）保持 Aim_Standing — 后坐力会使走路闪烁。
            if (allowRecoil)
            {
                _vibrationTimer = 0.08f;
                SetAimWeightImmediate(0f);
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgVibration);
            }
            else
            {
                _vibrationTimer = 0f;
            }

            _audio?.PlayGunFire();
            SpawnFiringVfx();
        }

        /// <summary>
        /// GunFire Firing：在 Gun 处 CreateObject 子弹 + 枪口闪光，在抛壳口 CreateObject 弹壳。
        /// </summary>
        private void SpawnFiringVfx()
        {
            ResolveRefs();
            Transform muzzle = gunMuzzle != null ? gunMuzzle : transform;
            Vector2 dir = _aimDir.sqrMagnitude > 0.0001f
                ? _aimDir.normalized
                : new Vector2(_motor.Facing, 0f);

            if (bulletPrefab != null)
            {
                // 略向前于枪口生成，避免首发（Gun 骨骼仍在落入瞄准姿势）
                // 与女主碰撞体重叠而立刻销毁。
                Vector3 spawnPos = muzzle.position + (Vector3)(dir * 0.35f);
                var bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                bullet.name = "_Bullet";
                var mover = bullet.GetComponent<PlayerBulletMover>();
                if (mover == null)
                {
                    mover = bullet.AddComponent<PlayerBulletMover>();
                }

                mover.SetOwner(transform);
                mover.Damage = _settings.bulletDamage;   
                mover.Launch(dir, _settings.bulletSpeed);
            }
            if (muzzleFlashPrefab != null)
            {
                var flash = Instantiate(muzzleFlashPrefab, muzzle.position, Quaternion.identity);
                flash.name = "MuzzleFlashLight";
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                flash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                Destroy(flash, 0.35f);
            }

            if (cartridgePrefab != null && ejectionPort != null)
            {
                // floor CreateObject 在 SMG_EjectionPort 处旋转欧拉角 (-90, 0, 0)。
                var rot = ejectionPort.rotation * Quaternion.Euler(-90f, 0f, 0f);
                var casing = Instantiate(cartridgePrefab, ejectionPort.position, rot);
                casing.name = "SMG_Cartridge";
                Destroy(casing, 2f);
            }
        }

        private void StartReload()
        {
            _phase = AdsPhase.Reloading;
            _phaseTimer = _settings.reloadDuration > 0f
                ? _settings.reloadDuration
                : PlayerAnimTimings.AimSmgReload.ClipLength;
            _vibrationTimer = 0f;
            _reloadElapsed = 0f;
            _reloadSeOffMagazine = false;
            _reloadSeSetMagazine = false;
            _reloadSeCocking = false;
            _reloadClipSeen = false;
            // 锁存当前瞄准，换弹期间枪线不再跟随鼠标。
            _reloadFrozenAimDir = _aimDir.sqrMagnitude > 0.0001f
                ? _aimDir.normalized
                : new Vector2(_motor != null ? _motor.Facing : 1, 0f);
            _aimDir = _reloadFrozenAimDir;
            _anim.SetAiming(true);
            // 关闭 Aim_Standing，使基础层 Aim_SMG_Reload 可见（站立移动 + 换弹）。
            SetAimWeightImmediate(0f);
            _anim.SetCrouch(_aimCrouch);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgReload);
            // 换弹音效在 TickReloadSe 中定时（Aim_* 状态为 BlendTree）。
        }
    }
}
