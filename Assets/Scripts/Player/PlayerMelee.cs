using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    public enum PlayerMeleePhase
    {
        Idle,
        /// <summary>Cancelable 之前 — 完全锁定。</summary>
        WindupLocked,
        /// <summary>Cancelable 之后 — ADS / 背后 A/D 后撤可切入。</summary>
        ActionCancelable,
        /// <summary>Movable 之后 — 跑步 / 跳跃 / 蹲下也可切入。</summary>
        MovableSheath
    }

    /// <summary>
    /// 地面连招：在 <c>Attackable</c> 上衔接；
    /// <c>Cancelable</c> 解锁 ADS / 背后 A/D 后撤（跳跃亦然，见 <see cref="LocksActions"/>）；
    /// <c>Movable</c> 解锁跑步 / 蹲下。
    /// Attack3 Movable = 近战 INIT（连招重置）；之后需要新的 SlashPressed 才能开 Attack1。
    /// </summary>
    public class PlayerMelee : MonoBehaviour
    {
        private struct AttackTiming
        {
            public float Attackable;
            public float Cancelable;
            public float Movable;
            public float SheathEnd;
        }

        private static readonly AttackTiming[] GroundTimings =
        {
            default,
            new AttackTiming
            {
                Attackable = PlayerAnimTimings.Attack1.Attackable,
                Cancelable = PlayerAnimTimings.Attack1.Cancelable,
                Movable = PlayerAnimTimings.Attack1.Movable,
                SheathEnd = PlayerAnimTimings.Attack1.ClipLength
            },
            new AttackTiming
            {
                Attackable = PlayerAnimTimings.Attack2.Attackable,
                Cancelable = PlayerAnimTimings.Attack2.Cancelable,
                Movable = PlayerAnimTimings.Attack2.Movable,
                SheathEnd = PlayerAnimTimings.Attack2.ClipLength
            },
            new AttackTiming
            {
                Attackable = -1f,
                Cancelable = PlayerAnimTimings.Attack3.Cancelable,
                Movable = PlayerAnimTimings.Attack3.Movable,
                SheathEnd = PlayerAnimTimings.Attack3.ClipLength
            },
        };

        [Header("斩击特效（Effects E_Katana1/2/3）")]
        [SerializeField] private GameObject slash1Prefab;
        [SerializeField] private GameObject slash2Prefab;
        [SerializeField] private GameObject slash3Prefab;
        [SerializeField] private GameObject slideSlashPrefab;
        [SerializeField] private GameObject jumpSlashUpPrefab;
        [SerializeField] private GameObject jumpSlashDownPrefab;

        [Header("Attack4 / Melee4 特效（Effects E_Katana4 → State 15）")]
        [Tooltip("主新月 — CreateObject #1。")]
        [SerializeField] private GameObject slash4Prefab;
        [Tooltip("尘烟 — CreateObject #2，随后 SetScale x = heroineScale * -2。")]
        [SerializeField] private GameObject katana4SmokePrefab;
        [Tooltip("余风驱动 — CreateObject #3（_Melee4AfterSlash FSM；无 PlayMaker 时仅 hitbox）。")]
        [SerializeField] private GameObject melee4AfterSlashPrefab;
        [Tooltip("可见余斩 — 原先由 _Melee4AfterSlash State 6 CreateObject 生成。")]
        [SerializeField] private GameObject melee4AfterPrefab;
        [Tooltip("相对女主根的世界偏移：(facing * x, y)。匹配 floor GetScale→*3→SetVector3XYZ，当 |scale|=1。")]
        [SerializeField] private float slash4OffsetXScale = 3f;
        [SerializeField] private float slash4OffsetY = 3.6f;
        [Tooltip("_Melee4AfterSlash 必须长于其 ChronosWait / Melee4After 链。")]
        [SerializeField] private float melee4AfterLifetime = 4f;

        [Tooltip("生成父节点。floor.unity CreateObject 使用 _Heroine 根；留空即如此。")]
        [SerializeField] private Transform slashSpawn;
        [Tooltip("女主根上的本地偏移（已挂接）。+x 始终为前方，弧线朝左时自动镜像。" +
            "身体盒约高 8 单位（脚 y0 / 躯干 y4 / 头 y8），故躯干高度约 4。" +
            "在检视器中微调以贴合刀刃。")]
        [SerializeField] private Vector2 slashOffset = new Vector2(0.7f, 4f);
        [SerializeField] private float slashLifetime = 1.2f;

        private PlayerContext _ctx;
        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;
        private PlayerSlashVfx _slashVfx;

        private PlayerDash _dash;
        private bool _isDashing;
        private int _pendingComboAfterDash;
        private bool _dashInputBuffered;

        private PlayerMeleePhase _phase = PlayerMeleePhase.Idle;
        private int _combo;
        private float _elapsed;
        private float _attackableAt;
        private float _cancelableAt;
        private float _movableAt;
        private float _sheathEndAt;
        private float _overrideVx;
        private bool _hasOverrideVx;
        private string _activeState;
        /// <summary>Attack2 在动画事件 E_Katana3 @ 0.3167s 再发第二道斩击。</summary>
        private bool _pendingAttack2Katana3;
        /// <summary>Attack3/Attack4 在动画事件 E_Katana4 @ 0.1667s 发出 Melee4 特效。</summary>
        private bool _pendingAttack3Katana4;
        /// <summary>_Melee4AfterSlash State 1 等待后播放 Melee4After 音效（距 Katana4 0.5s）。</summary>
        private bool _pendingMelee4AfterSe;
        private float _melee4AfterSeAt;
        private bool _isCrouchAttack;
        private bool _hasSpawnedCrouchSlash;  // 防止下蹲攻击每帧重复触发特效

        public PlayerMeleePhase Phase => _phase;
        public bool IsAttacking => _phase != PlayerMeleePhase.Idle;
        /// <summary>直到 Cancelable — 阻挡 ADS / 后撤 / 跳跃
        ///（PlayerArbiter.CanJump 据此门控，而非 <see cref="LocksMovement"/>）。</summary>
        public bool LocksActions => _phase == PlayerMeleePhase.WindupLocked;
        /// <summary>直到 Movable — 阻挡跑步 / 蹲下（PlayerArbiter.CanCrouch 据此门控）
        ///（地面时并把速度钉为 0）。</summary>
        public bool LocksMovement => _phase == PlayerMeleePhase.WindupLocked
            || _phase == PlayerMeleePhase.ActionCancelable;
        public int Combo => _combo;
        public bool HasVelocityOverride => _isDashing || (_hasOverrideVx && LocksMovement);

        public void Init(PlayerContext context)
        {
            _ctx = context;
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolveVfx();
            _dash = GetComponent<PlayerDash>() ?? gameObject.AddComponent<PlayerDash>();
            _dash.Init(context);

        }

        private void ResolveVfx()
        {
            // 程序化回退（刀光）— 缺少已制作斩击预制体时渲染。
            _slashVfx = GetComponent<PlayerSlashVfx>() ?? gameObject.AddComponent<PlayerSlashVfx>();

#if UNITY_EDITOR
			// 编辑器便利（模块在运行时组合，无序列化引用）。
			if (slash1Prefab == null) slash1Prefab = LoadPrefab("Slash1_1");
			if (slash2Prefab == null) slash2Prefab = LoadPrefab("Slash2");
			if (slash3Prefab == null) slash3Prefab = LoadPrefab("Slash3");
			if (slideSlashPrefab == null) slideSlashPrefab = LoadPrefab("Slash1_Slide");
			if (jumpSlashUpPrefab == null) jumpSlashUpPrefab = LoadPrefab("JumpSlashUp");
			if (jumpSlashDownPrefab == null) jumpSlashDownPrefab = LoadPrefab("JumpSlash_Down");
			if (slash4Prefab == null) slash4Prefab = LoadPrefab("Slash4");
			if (katana4SmokePrefab == null) katana4SmokePrefab = LoadPrefab("Katana4_Smoke");
			if (melee4AfterSlashPrefab == null) melee4AfterSlashPrefab = LoadPrefab("_Melee4AfterSlash");
			if (melee4AfterPrefab == null) melee4AfterPrefab = LoadPrefab("Melee4After");
#endif
        }

#if UNITY_EDITOR
		private static GameObject LoadPrefab(string prefabName)
		{
			return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/" + prefabName + ".prefab");
		}
#endif

        /// <summary>
        /// 在女主上生成斩击。斩击挂接到 _Heroine 根
        /// （<c>PlayerMelee.transform</c>）— 即电机翻转 <c>localScale.x</c> 为 ±1 以表示朝向的同一变换
        /// （<see cref="PlayerMotor.ForceFacing"/>）。因此：
        ///   • <see cref="slashOffset"/> 为本地偏移，故 +x 始终为「前方」，整段弧线朝左时自动镜像 —
        ///     无需手动朝向计算，也不会跑到错误一侧；
        ///   • 预制体保留其制作时的本地缩放/旋转，仅继承 ±1 翻转；
        ///   • 在短生命周期内跟随女主（匹配 floor State 22 SetParent，对其余斩击也无害，
        ///     因为斩击仅存活约 1s）。
        /// 这取代了早先的世界空间生成（从不镜像，且依赖无法从 floor.unity 运行时 FSM
        /// 变量还原的猜测偏移）。
        /// </summary>
        private GameObject SpawnSlash(GameObject prefab, PlayerSlashVfx.SlashKind kind,
            Vector2 extraOffset = default)
        {
            Transform spawnRoot = slashSpawn != null ? slashSpawn : transform;
            int facing = _motor.Facing;
            Vector3 localPos = new Vector3(
                slashOffset.x + extraOffset.x,
                slashOffset.y + extraOffset.y,
                0f);

            if (prefab == null)
            {
                // 程序化新月：自行构建对象，因此传入已镜像的世界姿势。
                Vector3 worldPos = spawnRoot.TransformPoint(localPos);
                return _slashVfx != null
                    ? _slashVfx.Play(kind, worldPos, spawnRoot.rotation, facing, slashLifetime)
                    : null;
            }

            var fx = Instantiate(prefab);
            // 挂到女主下（localScale.x = ±facing）→ 位置 + 弧线免费镜像。
            fx.transform.SetParent(spawnRoot, false);
            fx.transform.localPosition = localPos;

            if (slashLifetime > 0f)
            {
                Destroy(fx, slashLifetime);
            }

            return fx;
        }

        private void SpawnComboSlash(int index)
        {
            _pendingAttack2Katana3 = false;
            _pendingAttack3Katana4 = false;
            _pendingMelee4AfterSe = false;
            switch (index)
            {
                case 2:
                    // Attack2.anim：E_Katana2 @ 0.0333（近起点）+ E_Katana3 @ 0.3167。
                    SpawnSlash(slash2Prefab, PlayerSlashVfx.SlashKind.Attack2);
                    _pendingAttack2Katana3 = true;
                    break;
                case 3:
                    // Attack3.anim（动画器 Attack4）：仅 E_Katana4 @ 0.1667 — 延迟。
                    _pendingAttack3Katana4 = true;
                    break;
                default:
                    SpawnSlash(slash1Prefab, PlayerSlashVfx.SlashKind.Attack1);
                    break;
            }
        }

        /// <summary>
        /// 匹配攻击开始后触发的片段 SendEvent 的定时斩击特效 / 音效。
        /// </summary>
        private void TickPendingSlashVfx()
        {
            if (_pendingAttack2Katana3 && _combo == 2
                && _elapsed >= PlayerAnimTimings.Attack2.E_Katana3)
            {
                _pendingAttack2Katana3 = false;
                SpawnSlash(slash3Prefab, PlayerSlashVfx.SlashKind.Attack3);
                _audio?.PlayKatana(3);
            }

            if (_pendingAttack3Katana4 && _combo == 3
                && _elapsed >= PlayerAnimTimings.Attack3.E_Katana4)
            {
                _pendingAttack3Katana4 = false;
                SpawnMelee4Vfx();
            }

        }

        private void TickPendingMelee4AfterSe()
        {
            if (!_pendingMelee4AfterSe || Time.time < _melee4AfterSeAt)
            {
                return;
            }

            _pendingMelee4AfterSe = false;
            _audio?.PlayMelee4After();
            SpawnMelee4AfterSlashFx();
        }

        /// <summary>
        /// _Melee4AfterSlash FSM State 6：在所有者处 CreateObject(Melee4After) + SetScale x = facing。
        /// 无 PlayMaker，故在此与延迟音效一起生成可见斩击。
        /// </summary>
        private void SpawnMelee4AfterSlashFx()
        {
            Transform spawnRoot = slashSpawn != null ? slashSpawn : transform;
            int facing = _motor != null ? _motor.Facing : 1;
            var after = SpawnWorldFx(
                melee4AfterPrefab,
                PlayerSlashVfx.SlashKind.After,
                spawnRoot.position,
                spawnRoot.rotation,
                facing,
                slashLifetime > 0f ? slashLifetime : 1.2f);
            MirrorScaleX(after != null ? after.transform : null, facing);
        }

        /// <summary>
        /// floor Effects State 15（E_Katana4）：世界空间 CreateObject Slash4
        ///（偏移 = (facing * 3, 3.6)）、Katana4_Smoke（scale.x = |prefab.x| * -facing），以及
        /// _Melee4AfterSlash（scale.x = facing；FSM 驱动余风 + Melee4After 音效）。
        /// </summary>
        private void SpawnMelee4Vfx()
        {
            // 立即音效：Melee4_2 + Meleeplus + Melee4Afterwind（Effects State 15）。
            _audio?.PlayMelee4();
            // 延迟音效：Melee4After — 原先 _Melee4AfterSlash State1 ChronosWait(0.5) → State6。
            // 不依赖该 FSM：无 Clock 时 ChronosWait 会卡住，且 AudioPlay 需要 AudioMaster。
            // 使用 Time.time，以便攻击在 0.5s 前结束时音效仍会触发（预制体未挂接）。
            _pendingMelee4AfterSe = true;
            _melee4AfterSeAt = Time.time + PlayerAnimTimings.Attack3.Melee4AfterSe;

            Transform spawnRoot = slashSpawn != null ? slashSpawn : transform;
            int facing = _motor.Facing;
            Vector3 rootPos = spawnRoot.position;
            Quaternion rootRot = spawnRoot.rotation;

            // CreateObject #1 — Slash4。保留制作缩放 (9,8,9)；朝左 = Y+180 Flip
            //（floor SetFsmFloat → FloatSignTest → SetRotation Y+=180）。不要用女主
            // localScale 覆盖缩放（那会把 9→±1 并弄坏网格）。
            var slash4 = SpawnWorldFx(
                slash4Prefab,
                PlayerSlashVfx.SlashKind.Attack3,
                rootPos + new Vector3(facing * slash4OffsetXScale, slash4OffsetY, 0f),
                rootRot,
                facing,
                slashLifetime);
            if (slash4 != null && facing < 0)
            {
                slash4.transform.Rotate(0f, 180f, 0f, Space.Self);
            }

            // CreateObject #2 — Katana4_Smoke 在根处；SetScale x = facing * -2，当 |prefab.x|=2。
            var smoke = SpawnWorldFx(
                katana4SmokePrefab,
                PlayerSlashVfx.SlashKind.After,
                rootPos,
                rootRot,
                facing,
                slashLifetime);
            MirrorScaleX(smoke != null ? smoke.transform : null, -facing);

            // CreateObject #3 — _Melee4AfterSlash 特效/hitbox 驱动（音效由上方 PlayerAudio 拥有）。
            var after = SpawnWorldFx(
                melee4AfterSlashPrefab,
                PlayerSlashVfx.SlashKind.After,
                rootPos,
                rootRot,
                facing,
                melee4AfterLifetime);
            MirrorScaleX(after != null ? after.transform : null, facing);
        }

        /// <summary>
        /// 未挂接的世界空间生成（匹配 floor CreateObject spawnPoint + position，无 SetParent）。
        /// 保留预制体制作时的 localScale；仅赋值位置/旋转。
        /// </summary>
        private GameObject SpawnWorldFx(GameObject prefab, PlayerSlashVfx.SlashKind kind,
            Vector3 worldPos, Quaternion worldRot, int facing, float lifetime)
        {
            if (prefab == null)
            {
                return _slashVfx != null
                    ? _slashVfx.Play(kind, worldPos, worldRot, facing, lifetime)
                    : null;
            }

            var fx = Instantiate(prefab);
            fx.transform.SetParent(null, false);
            fx.transform.position = worldPos;
            fx.transform.rotation = worldRot;

            if (lifetime > 0f)
            {
                Destroy(fx, lifetime);
            }

            return fx;
        }

        /// <summary>保留制作时的 |x| 幅度；应用朝向符号（floor GetScale → SetScale 模式）。</summary>
        private static void MirrorScaleX(Transform t, int sign)
        {
            if (t == null)
            {
                return;
            }

            int s = sign >= 0 ? 1 : -1;
            Vector3 scale = t.localScale;
            scale.x = Mathf.Abs(scale.x) * s;
            if (Mathf.Abs(scale.x) < 0.0001f)
            {
                scale.x = s;
            }

            t.localScale = scale;
        }

        public void Tick(PlayerIntent intent, bool canMelee)
        {
            _hasOverrideVx = false;
            TickPendingMelee4AfterSe();

            if (_phase != PlayerMeleePhase.Idle)
            {
                TickActiveAttack(intent, !canMelee);
                return;
            }

            // 滑铲：禁止任何攻击（含原先的 Slide_Attack 特殊招）— 见
            // PlayerArbiter.CanMelee，现亦对 !CrouchIsSliding 门控。
            if (!canMelee || !intent.SlashPressed)
            {
                return;
            }

            if (!_ctx.IsGrounded || _ctx.OnAir)
            {
                BeginJumpAttack();
                return;
            }
            if (_ctx.IsCrouchBusy)
            {
                DoCrouchAttack();
                return;
            }
            else
            {
             DoGroundCombo(1);
            }
                
        }

        /// <summary>开始跳跃攻击，按当前竖直速度选择 Up/Down
        ///（镜像 <see cref="Tick"/> 中的初始触发检查）。</summary>
        private void BeginJumpAttack()
        {
            float vy = _motor.GetVelocity().y;
            if (vy >= 0f)
            {
                BeginAttack(
                    PlayerAnimDriver.States.JumpAttackUp,
                    -1f,
                    PlayerAnimTimings.JumpAttackUp.JCancelable,
                    PlayerAnimTimings.JumpAttackUp.ClipLength,
                    PlayerAnimTimings.JumpAttackUp.ClipLength,
                    0);
                _ctx.NotifyJumpAttack?.Invoke();
                _audio?.PlayMeleeSwing(0);
                SpawnSlash(jumpSlashUpPrefab, PlayerSlashVfx.SlashKind.JumpUp);
            }
            else
            {
                BeginAttack(
                    PlayerAnimDriver.States.JumpAttackDown,
                    -1f,
                    PlayerAnimTimings.JumpAttackDown.ClipLength,
                    PlayerAnimTimings.JumpAttackDown.ClipLength,
                    PlayerAnimTimings.JumpAttackDown.ClipLength,
                    0);
                _motor.SetVelocityY(-Mathf.Abs(_settings.jumpAttackDownYVelocity));
                _ctx.NotifyJumpAttack?.Invoke();
                _audio?.PlayJumpAttackDown();
                SpawnSlash(jumpSlashDownPrefab, PlayerSlashVfx.SlashKind.JumpDown);
            }
        }

        public void ApplyFixedVelocity()
        {
            if (_isDashing)
            {
                _dash.ApplyFixedVelocity();
                return;
            }
            if (_hasOverrideVx)
            {
                _motor.SetImmediateVelocityX(_overrideVx);
            }
        }

        /// <summary>
        /// floor SE_JumpAttack(Down) / EventLand：Jump_Attack_Up 或 Jump_Attack_Down 播放中
        /// 触地会直接切到 Landing 片段，而不是在地面上播完斩击姿势。在此结束攻击 —
        /// 本帧稍后 PlayerJump.Tick 运行之前 — 把移动/动画所有权交回 PlayerJump，使其
        /// 正常 Land() 在本帧播放，而非近战仍占用动画时使用的静默 ClearAirOnOwnedLand()。
        /// </summary>
        private bool TickJumpAttackLanding()
        {
            bool isJumpAttack = _activeState == PlayerAnimDriver.States.JumpAttackUp
                || _activeState == PlayerAnimDriver.States.JumpAttackDown;
            if (!isJumpAttack || !_ctx.IsGrounded)
            {
                return false;
            }

            EndAttack();
            return true;
        }

        private void TickActiveAttack(PlayerIntent intent, bool gunBusy)
        {   
            float dt = _motor.DeltaTime;
            if (_isDashing)
            {
                TickDash(intent, dt);
                return;
            }


            if (_isCrouchAttack)
            {

                _elapsed += dt;
                TickLunge(dt);
                if (!_hasSpawnedCrouchSlash && _elapsed >= 0.0667f)
                {
                    _hasSpawnedCrouchSlash = true;
                    SpawnSlash(slideSlashPrefab, PlayerSlashVfx.SlashKind.Slide);
                    _audio?.PlayKatana(1);
                }
                AdvancePhase();
                if (TickMovableGroundInterrupt(intent)) return;
                if (TickCrouchAttackRestart(intent, gunBusy)) return;   // 新增
                if (TickCancelableInterrupts(intent, gunBusy)) return;
                TickSheathEnd();
                return;
            }



            if (TickJumpAttackLanding())
            {
                return;
            }

            
            _elapsed += dt;

            TickLunge(dt);
            TickPendingSlashVfx();
            TickDashBuffer(intent);          
            if (TickDashChain(intent))
            {
                return;
            }
            if (TickMovableGroundInterrupt(intent))
            {
                return;
            }
            if (TickComboChain(intent))
            {
                return;
            }
            if (TickJumpAttackChain(intent))
            {
                return;
            }
            // Movable 解锁蹲下 / 跑步 — 最近解锁的动作优先于继续挥砍，
            // 故在按住 Slash 衔接地面连招（TickComboChain）或重启（TickAttack3Restart）之前检查。
            // 仅地面：空中 / 跳跃攻击衔接不受影响，仍在下方处理。
            AdvancePhase();

            if (TickAttack3Restart(intent, gunBusy))
            {
                return;
            }

            if (TickCancelableInterrupts(intent, gunBusy))
            {
                return;
            }

            TickSheathEnd();
        }

        /// <summary>
        /// Movable 解锁蹲下（PlayerArbiter.CanCrouch）/ 跑步（CanMove）— 先检查它们，
        /// 使最新解锁的动作优先于攻击继续。镜像 <see cref="TickCancelableInterrupts"/> 中的
        /// 蹲下 / 移动条件，但更早求值并直接按 <see cref="_elapsed"/> 键控
        ///（无 AdvancePhase 滞后），以便 Movable 触发的同一帧生效。仅地面 —
        /// 空中/跳跃攻击 OnAir 处理不变，稍后仍由 TickCancelableInterrupts 覆盖。
        /// </summary>
        private bool TickMovableGroundInterrupt(PlayerIntent intent)
        {
            if (!_ctx.IsGrounded || _elapsed < _movableAt)
            {
                return false;
            }

            bool behind = PlayerJump.IsMoveBehind(intent.Move, _motor.Facing);
            bool crouchInterrupt = !_isCrouchAttack && (intent.CrouchPressed || _ctx.IsCrouchBusy);
            bool moveInterrupt = !behind && Mathf.Abs(intent.Move) > 0.1f;
            if (!crouchInterrupt && !moveInterrupt)
            {
                return false;
            }

            EndAttack();
            return true;
        }

        /// <summary>
        /// 近战锁定移动时，把水平速度钉为 0，以免角色继续滑行（先前跑步惯性）。
        /// </summary>
        private void TickLunge(float dt)
        {
            if (!LocksMovement || !_motor.IsGrounded)
            {
                return;
            }

            _overrideVx = 0f;
            _hasOverrideVx = true;
        }

        /// <summary>衔接攻击已开始时返回 true（调用方应停止本帧）。</summary>
        private bool TickComboChain(PlayerIntent intent)
        {
            bool groundCombo = _combo >= 1 && _combo <= _settings.maxMeleeCombo;
            bool canChain = groundCombo && _attackableAt >= 0f
                && _combo < _settings.maxMeleeCombo
                && _ctx.IsGrounded && !_ctx.IsSliding;

            // 无缓冲：Attackable 之前的输入无效。从 Attackable 事件到片段结束，
            // 任意一帧攻击键按下（该窗口内按住或新按下）都会推进连招（1→2 / 2→3）。
            // Attackable 前松开的单击无法衔接，故一次点击 = 一次攻击。
            if (!canChain || _elapsed < _attackableAt)
            {
                return false;
            }

            if (intent.Slash)
            {
                DoGroundCombo(_combo + 1);
                return true;
            }

            return false;

        }

        private bool TickDashChain(PlayerIntent intent)
        {
            bool groundCombo = _combo >= 1 && _combo <= _settings.maxMeleeCombo;
            bool canChain = groundCombo && _combo < _settings.maxMeleeCombo
                && _ctx.IsGrounded && !_ctx.IsSliding;

            if (!canChain)
            {
                return false;
            }

            // 核心窗口：Cancelable 之后，Movable 之前
            if (_elapsed < _cancelableAt || _elapsed >= _movableAt)
            {
                return false;
            }

            if (intent.ForwardPressed(_motor.Facing))
            {
                BeginDash(_combo + 1);
                return true;
            }

            return false;
        }
        /// <summary>新的跳跃攻击已衔接时返回 true（调用方应停止本帧）。
        /// 跳跃攻击不属于地面连招（<see cref="_combo"/> 保持 0），故自行衔接：
        /// 一旦 JCancelable 触发（<see cref="_cancelableAt"/>）挥砍锁定解除，
        /// 仍在空中时按住攻击键再抛出一次跳跃攻击
        ///（按当前竖直速度选 Up/Down，与初始触发相同）。</summary>
        private bool TickJumpAttackChain(PlayerIntent intent)
        {
            bool isJumpAttack = _activeState == PlayerAnimDriver.States.JumpAttackUp
                || _activeState == PlayerAnimDriver.States.JumpAttackDown;
            bool airborne = !_ctx.IsGrounded || _ctx.OnAir;
            if (!isJumpAttack || !airborne || !intent.Slash || _elapsed < _cancelableAt)
            {
                return false;
            }

            BeginJumpAttack();
            return true;
        }

        private void AdvancePhase()
        {
            if (_phase == PlayerMeleePhase.WindupLocked && _elapsed >= _cancelableAt)
            {
                _phase = PlayerMeleePhase.ActionCancelable;
            }

            if (_phase == PlayerMeleePhase.ActionCancelable && _elapsed >= _movableAt)
            {
                _phase = PlayerMeleePhase.MovableSheath;
            }
        }

        /// <summary>Attack3 Movable 之后：按住或新按攻击键循环回 Attack1
        ///（按住时 Attack1→2→3→1 循环）；新连招开始时返回 true。</summary>
        private bool TickAttack3Restart(PlayerIntent intent, bool gunBusy)
        {
            if (_combo == 3 && _elapsed >= _movableAt && intent.Slash
                && _ctx.IsGrounded && !_ctx.IsSliding && !gunBusy)
            {
                DoGroundCombo(1);
                return true;
            }

            return false;
        }
        /// <summary>蹲下攻击 Movable 之后按下攻击键 → 重新播放同一个 Crouch_Attack
        /// （非连招，只有一个动画；每次都是全新的单次攻击）。</summary>
        private bool TickCrouchAttackRestart(PlayerIntent intent, bool gunBusy)
        {
            if (_phase == PlayerMeleePhase.MovableSheath && intent.SlashPressed
                && _ctx.IsGrounded && !_ctx.IsSliding && !gunBusy && _ctx.IsCrouchBusy)
            {
                DoCrouchAttack();
                return true;
            }
            return false;
        }

        /// <summary>Cancelable / Movable 动作 + 移动打断。攻击结束时返回 true。</summary>
        private bool TickCancelableInterrupts(PlayerIntent intent, bool gunBusy)
        {
            if (_phase != PlayerMeleePhase.ActionCancelable
                && _phase != PlayerMeleePhase.MovableSheath)
            {
                return false;
            }

            bool actionInterrupt = gunBusy || intent.WantsAds || intent.ReloadPressed;

            // Cancelable 后背后 A/D → BackStep（Controller）；不要先软结束。
            bool behind = PlayerJump.IsMoveBehind(intent.Move, _motor.Facing);

            // 蹲下由 Movable 门控（PlayerArbiter.CanCrouch）— 与跑步解锁同一点 —
            // 故仅在 MovableSheath 时为蹲下结束攻击，而非从 Cancelable。
            // 滑铲本身经 actionInterrupt 强制结束（gunBusy = !CanMelee，现亦对
            // !CrouchIsSliding 门控）— 此处覆盖普通蹲下/起身情况。
            bool crouchInterrupt = _phase == PlayerMeleePhase.MovableSheath
               && !_isCrouchAttack
               && (intent.CrouchPressed || _ctx.IsCrouchBusy);

            // 跳跃由 Cancelable 门控（PlayerArbiter.CanJump 使用 MeleeLocksActions），故跳跃
            // 按下从 ActionCancelable 起即可结束地面攻击 — 不只在 Movable 之后。
            // 仅地面：跳跃攻击（已在空中）经 TickJumpAttackChain 用 Slash 键衔接，
            // 而非 Jump 键，故此处不得触碰它们。
            bool jumpInterrupt = _ctx.IsGrounded && !behind && (intent.JumpPressed || intent.Jump);

            // 跑步 / 一般移动仍等待 Movable。
            bool moveInterrupt = _phase == PlayerMeleePhase.MovableSheath
                && !behind
                && (Mathf.Abs(intent.Move) > 0.1f || _ctx.OnAir);

            if (actionInterrupt || crouchInterrupt || jumpInterrupt || moveInterrupt)
            {
                EndAttack();
                return true;
            }

            if (!string.IsNullOrEmpty(_activeState) && _anim.IsPlaying(_activeState))
            {
                _anim.SyncCurrent(_activeState);
            }

            return false;
        }

        private void TickSheathEnd()
        {
            bool animDone = !string.IsNullOrEmpty(_activeState)
                && _anim.IsPlaying(_activeState)
                && _anim.BaseFinished;

            if (_elapsed >= _sheathEndAt || animDone)
            {
                // 收刀音效（SE_Noutou / SE_Noutou2 / SE_NoutouFast）来自片段的
                // 烘焙动画事件 → PlayerAudio.SendEvent；此处无定时驱动音效。
                EndAttack();
            }
        }

        private void DoGroundCombo(int index)
        {
            index = Mathf.Clamp(index, 1, _settings.maxMeleeCombo);
            string state;
            switch (index)
            {
                case 2:
                    state = PlayerAnimDriver.States.Attack2;
                    break;
                case 3:
                    state = PlayerAnimDriver.States.Attack3;
                    break;
                default:
                    state = PlayerAnimDriver.States.Attack1;
                    break;
            }

            AttackTiming timing = GroundTimings[index];
            BeginAttack(state, timing.Attackable, timing.Cancelable, timing.Movable,
                timing.SheathEnd, index);
            // Attack3 / Attack4 音效与 E_Katana4 特效一起播放（PlayMelee4），而非起手。
            // combo1 → E_Katana1（Attack），combo2 → E_Katana2（Attack2）。
            if (index != 3)
            {
                _audio?.PlayKatana(index);
            }

            SpawnComboSlash(index);
        }

        private void DoCrouchAttack()
        {
            _isCrouchAttack = true;
            _hasSpawnedCrouchSlash = false;
            _activeState = "Crouch_Attack";
            _combo = 0;
            _elapsed = 0;
            _attackableAt = 0.4f;
            _cancelableAt = 0.3167f;
            _movableAt = 0.6667f;
            _sheathEndAt = 2.5f;
            _anim.ForcePlay("Crouch_Attack");
            _overrideVx = 0;
            _hasOverrideVx = true;
            _phase = PlayerMeleePhase.WindupLocked;
        }
        private void BeginAttack(string state, float attackableAt, float cancelableAt,
            float movableAt, float sheathEndAt, int combo)
        {
            _phase = PlayerMeleePhase.WindupLocked;
            _elapsed = 0f;
            _attackableAt = attackableAt;
            _cancelableAt = Mathf.Max(cancelableAt, 0.05f);
            _movableAt = Mathf.Max(movableAt, _cancelableAt);
            _sheathEndAt = Mathf.Max(sheathEndAt, _movableAt);
            _combo = combo;
            _activeState = state;
            _sheathEndAt = Mathf.Max(sheathEndAt, _movableAt);
            _anim.ForcePlay(state);
            // 清掉任何到来的跑步/滑铲动量，以免打击带着横向惯性。
            _overrideVx = 0f;
            _hasOverrideVx = true;
            _motor.SetImmediateVelocityX(0f);
        }

        private void EndAttack()
        {
            if (_isDashing)
            {
                _dash.End();
                _isDashing = false;
            }

            if (_isCrouchAttack && _ctx != null && _ctx.IsCrouchBusy)
            {
                _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
            }

            _phase = PlayerMeleePhase.Idle;      
            _activeState = null;
            _combo = 0;
            _hasOverrideVx = false;
            _pendingAttack2Katana3 = false;
            _pendingAttack3Katana4 = false;
            _isCrouchAttack = false;
            _dashInputBuffered = false;
        }
        private void BeginDash(int nextCombo)
        {
            _isDashing = true;
            _pendingComboAfterDash = Mathf.Clamp(nextCombo, 1, _settings.maxMeleeCombo);
            _elapsed = 0f;
            _activeState = PlayerAnimDriver.States.StepForward2;
            _phase = PlayerMeleePhase.WindupLocked;
            _dashInputBuffered = false;
            _dash.Begin();
        }

        private void TickDash(PlayerIntent intent, float dt)
        {
            bool dashFinished = _dash.Tick(dt);
            _elapsed = _dash.Elapsed;

            bool chainable = _elapsed >= _dash.SteppedForwardAt;
            if (chainable && intent.SlashPressed && _ctx.IsGrounded && !_ctx.IsSliding)
            {
                int next = _pendingComboAfterDash;
                EndDash();
                DoGroundCombo(next);
                return;
            }
            bool actionInterrupt = intent.WantsAds || intent.ReloadPressed || intent.JumpPressed;
            if (actionInterrupt)
            {
                EndDash();
                EndAttack();
                return;
            }
            if (dashFinished)
            {
                EndDash();                   
                EndAttack();                  
                _anim.ForcePlay(PlayerAnimDriver.States.RunToIdle);
            }
        }
        private float DashWindowOpenAt()
        {
            switch (_combo)
            {
                case 1: return PlayerAnimTimings.Attack1.E_Katana1;  // 0.0667s
                case 2: return PlayerAnimTimings.Attack2.E_Katana2;  // 0.0333s
                default: return 6f / 60f;                            // 0.1s
            }
        }
        private void TickDashBuffer(PlayerIntent intent)
        {
            // 已缓冲则跳过
            if (_dashInputBuffered)
            {
                return;
            }

            // 超过 Movable 则不再接受输入
            if (_elapsed >= _movableAt)
            {
                return;
            }

            // 窗口：斩击特效时刻 → Movable 之前
            if (_elapsed >= DashWindowOpenAt() && intent.ForwardPressed(_motor.Facing))
            {
                _dashInputBuffered = true;
            }
        }
        private void EndDash()
        {
            _dash.End();
            _isDashing = false;
        }

        public void Cancel()
        {
            EndAttack();
        }
    }
}
