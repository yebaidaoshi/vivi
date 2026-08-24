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
        private Vector2 _reloadFrozenAimDir = Vector2.right;
        private bool _releaseHoldsAnim;
        private LineRenderer _laser;
        private Transform _aimingRoot;
        private Transform _laserPointer;
        private bool _aimPhysicsSuppressed;

        private float _reloadElapsed;
        private bool _reloadSeOffMagazine;
        private bool _reloadSeSetMagazine;
        private bool _reloadSeCocking;
        private bool _reloadClipSeen;
        private float _releaseElapsed;
        private bool _releaseSeFoldSmg;
        private bool _releaseClipSeen;

        private bool _aimCrouch;
        private bool _crouched;
        private bool _crouchAimTransitionTarget;
        private float _crouchAimTransitionTimer;
        private float _crouchAimBlend;
        private bool _crouchAimClipSeen;

        // ★ 新增：弹药变化事件
        public event System.Action OnAmmoChanged;

        public bool IsAds => _phase == AdsPhase.Raising || _phase == AdsPhase.Holding
            || _phase == AdsPhase.Reloading
            || _phase == AdsPhase.CrouchAimTransition;
        public bool IsReloading => _phase == AdsPhase.Reloading;
        public bool OwnsCrouchBaseAnim => IsAds
            || (_phase == AdsPhase.Releasing && _releaseHoldsAnim);
        public bool IsBusy => _phase == AdsPhase.Raising || _phase == AdsPhase.Holding
            || _phase == AdsPhase.Reloading
            || _phase == AdsPhase.CrouchAimTransition
            || (_phase == AdsPhase.Releasing && _releaseHoldsAnim);
        public int Ammo => _ammo;

        // ★ 新增：最大弹药容量（从 Settings 中读取，若没有则默认 30）
        public int MaxAmmo => _settings != null ? _settings.magazineCapacity : 30;

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
        }

        private void LateUpdate()
        {
            UpdateAimLaser();
        }

        private void TickReloading(PlayerIntent intent, bool allowFaceFlip,
            bool actionInterrupt, bool sliding)
        {
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
            UpdateReloadAimLocomotion(intent);

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
                    _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgReload);
                }
            }

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
                // ★ 新增：换弹完成，弹药恢复满，触发事件
                OnAmmoChanged?.Invoke();
            }
        }

        private void CancelReload(bool keepCrouch)
        {
            _reloadSeOffMagazine = false;
            _reloadSeSetMagazine = false;
            _reloadSeCocking = false;
            _reloadClipSeen = false;
            FinishRelease(keepCrouch);
        }

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
            _aimCrouch = _crouched;
            _anim.SetCrouch(_aimCrouch);
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

            bool wantCrouch = _crouched;
            if (wantCrouch != _aimCrouch && _vibrationTimer <= 0f)
            {
                BeginCrouchAimTransition(wantCrouch);
                TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                return;
            }

            _anim.SetCrouch(_aimCrouch);
            UpdateAdsAim(intent, allowFaceFlip);

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
                TryFire(allowRecoil: !moving);
            }
        }

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

        private void BeginCrouchAimTransition(bool toCrouch)
        {
            _phase = AdsPhase.CrouchAimTransition;
            _crouchAimTransitionTarget = toCrouch;
            _crouchAimClipSeen = false;
            _crouchAimTransitionTimer = PlayerAnimTimings.CrouchAimTransition.ClipLength + 0.05f;
            _crouchAimBlend = toCrouch ? 0f : 1f;
            _anim.SetCrouchingWeight(_crouchAimBlend);
            _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
        }

        private void TickCrouchAimTransition(PlayerIntent intent, bool wantAds, bool allowFaceFlip)
        {
            if (!wantAds)
            {
                EnterRelease();
                return;
            }

            UpdateAdsAim(intent, allowFaceFlip);
            SetAimWeightImmediate(0f);
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
                _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }

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

            if (_aimCrouch && !intent.Crouch)
            {
                FinishRelease(keepCrouch: false);
                _anim.SetCrouch(false);
                return;
            }

            if (!_aimCrouch && intent.Crouch)
            {
                FinishRelease(keepCrouch: false);
                return;
            }

            bool inputInterrupt = actionInterrupt || sliding
                || intent.SlashPressed || intent.JumpPressed
                || intent.EvadePressed || intent.ReloadPressed
                || (!_aimCrouch && Mathf.Abs(intent.Move) > 0.1f);

            if (inputInterrupt)
            {
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
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);
                onRelease = true;
            }
            else if (_releaseClipSeen && !onRelease)
            {
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
                return;
            }

            if ((onRelease && _anim.AimFinished) || _phaseTimer <= 0f || onCrouching)
            {
                FinishRelease(keepCrouch: intent.Crouch && _aimCrouch);
            }
        }

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

        private void UpdateReloadAimLocomotion(PlayerIntent intent)
        {
            float move = _aimCrouch ? 0f : intent.Move;
            float aimLocomotion = Mathf.Abs(_motor.Facing - move);
            _anim.SetAimDir(aimLocomotion);
            _aimDir = _reloadFrozenAimDir.sqrMagnitude > 0.0001f
                ? _reloadFrozenAimDir.normalized
                : new Vector2(_motor.Facing, 0f);
        }

        private void UpdateAdsAim(PlayerIntent intent, bool allowFaceFlip)
        {
            ResolveRefs();

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
                Quaternion desired = Quaternion.Euler(0f, 0f, angle);
                float step = Mathf.Clamp01(aimLookSpeed * _motor.DeltaTime);
                aimPivot.rotation = Quaternion.Slerp(aimPivot.rotation, desired, step);
            }

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

            // ★ 新增：开火消耗子弹后触发事件
            OnAmmoChanged?.Invoke();
        }

        private void SpawnFiringVfx()
        {
            ResolveRefs();
            Transform muzzle = gunMuzzle != null ? gunMuzzle : transform;
            Vector2 dir = _aimDir.sqrMagnitude > 0.0001f
                ? _aimDir.normalized
                : new Vector2(_motor.Facing, 0f);

            if (bulletPrefab != null)
            {
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
            _reloadFrozenAimDir = _aimDir.sqrMagnitude > 0.0001f
                ? _aimDir.normalized
                : new Vector2(_motor != null ? _motor.Facing : 1, 0f);
            _aimDir = _reloadFrozenAimDir;
            _anim.SetAiming(true);
            SetAimWeightImmediate(0f);
            _anim.SetCrouch(_aimCrouch);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgReload);
        }
    }
}