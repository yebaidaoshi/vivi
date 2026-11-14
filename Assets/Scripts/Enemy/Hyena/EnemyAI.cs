using System.Collections.Generic;
using Enemy.Fsm;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 鬣狗 AI —— 按原版 PlayMaker FSM（_Hyena/AI，111 状态）驱动。
    ///
    /// 数据来源：Assets/Resources/EnemyFsm/_HyenaParent.json（由解包 prefab 自动导出）。
    /// 状态与跳转完全来自原文件；动作按语义映射到 EnemyMotor / EnemyAnimDriver / EnemyCombat。
    ///
    /// 事件从哪里来：
    ///   - 感知：本脚本 Tick 里按距离/视野产生 Spot / CLAW / WALK（对应原版 Sight / *_Range FSM）
    ///   - 动画：EnemyController.SendEvent → OnAnimEvent → SendEvent
    ///   - 受击：EnemyHealth → NotifyHit → SendEvent("FLINCH")
    /// </summary>
    public class EnemyAI : EnemyBrainBase
    {
        [Header("FSM 数据")]
        [SerializeField] private string fsmHost = "_Hyena";
        [SerializeField] private string fsmName = "AI";
        [SerializeField] private bool logStateChanges = true;
        [SerializeField] private bool logHeartbeat = false;

        [Header("移动")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float runThreshold = 9f;

        [Header("距离阈值")]
        [SerializeField] private float attackRange = 2.5f;
        [SerializeField] private float viewDistance = 60f;

        [Header("攻击冷却")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("感知（原版 Sight / BackSight）")]
        [Tooltip("正面视野距离（原版 Sight 盒 42 宽）")]
        [SerializeField] private float sightForward = 42f;
        [Tooltip("背后感知距离（原版 BackSight 盒 7 宽）")]
        [SerializeField] private float sightBack = 7f;

        [Header("跳攻（原版 JumpAttack_Range）")]
        [Tooltip("跳攻命中伤害")]
        [SerializeField] private int jumpAttackDamage = 20;
        [Tooltip("跳攻判定盒中心（相对自身，x 按朝向翻转）")]
        [SerializeField] private Vector2 jumpHitBoxOffset = new Vector2(2.5f, 2f);
        [SerializeField] private Vector2 jumpHitBoxSize = new Vector2(4f, 4f);
        [Tooltip("跳攻触发距离（原版范围盒约 15.6）")]
        [SerializeField] private float jumpAttackRange = 15.6f;
        [SerializeField] private float jumpAttackCooldown = 4f;

        [Header("仇恨记忆")]
        [Tooltip("被发现后维持追击的时间，绕背后也不会立刻丢失目标")]
        [SerializeField] private float aggroMemory = 6f;
        [Tooltip("超过这个距离才彻底脱战")]
        [SerializeField] private float loseRange = 60f;

        [Header("巡逻（原版 Roam）")]
        [SerializeField] private float roamInterval = 4f;
        [SerializeField] private float roamStepTime = 1.2f;

        [Header("束缚/擒抱攻击")]
        [SerializeField] private float mountRange = 3f;
        [SerializeField] private float mountCooldown = 8f;

        [Header("动画状态名")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string walkAnim = "Movement";
        [SerializeField] private string spotAnim = "Idle_Caution";
        [SerializeField] private string surprisedAnim = "Surprised";
        [SerializeField] private string clawAnim = "Claw";
        [SerializeField] private string jumpAttackAnim = "Jump_Attack0";
        [SerializeField] private string hitAnim = "Hit_Up1";
        [SerializeField] private string breakAnim = "Break";
        [SerializeField] private string deathAnim = "Hit_Blow1";

        [Header("状态时长（秒，按原版动作推算）")]
        [SerializeField] private float initDelay = 0.05f;
        [SerializeField] private float difficultyDelay = 0.1f;
        [SerializeField] private float surprisedDelay = 0.45f;
        [SerializeField] private float walkStepDelay = 0.6f;
        [SerializeField] private float clawWindup = 0.3f;
        [SerializeField] private float clawRecover = 0.6f;
        [SerializeField] private float jumpAttackDelay = 0.9f;
        [SerializeField] private float hitDelay = 0.35f;
        [SerializeField] private float breakDelay = 0.5f;

        private EnemyCombat _combat;
        private EnemyHealth _health;

        private FsmDefinition _fsm;
        private readonly Dictionary<string, string> _globals = new Dictionary<string, string>();

        private string _state = "";
        private float _stateTime;
        private float _attackFiredAt = -1f;
        private bool _deathPlayed;
        private bool _playerDetected;
        private float _hbTimer;
        private float _attackCooldownTimer;
        private float _jumpCooldownTimer;
        private bool _alerted;
        private float _alertGrace;
        private float _aggroTimer;
        private float _roamTimer;
        private float _mountCooldownTimer;
        private int _roamDir;
        private bool _warnedNoController;
        private readonly Dictionary<string, bool> _loggedAnim = new Dictionary<string, bool>();

        public string CurrentState => _state;
        public FsmDefinition Fsm => _fsm;

        // =====================================================================
        // 初始化
        // =====================================================================

        public override void Init(EnemyMotor motor, EnemyAnimDriver anim, EnemyAttackModule attack, EnemyHealth health)
        {
            base.Init(motor, anim, attack, health);

            _combat = attack as EnemyCombat;
            _health = health;

            LoadFsm();
        }

        private void LoadFsm()
        {
            var db = EnemyFsmData.Hyena;
            if (db == null) return;

            _fsm = db.Find(fsmHost, fsmName);
            if (_fsm == null)
            {
                Debug.LogError("[EnemyAI] 找不到 FSM 数据：" + fsmHost + "/" + fsmName);
                return;
            }

            _globals.Clear();
            if (_fsm.globalTransitions != null)
            {
                for (int i = 0; i < _fsm.globalTransitions.Length; i++)
                {
                    var g = _fsm.globalTransitions[i];
                    if (!string.IsNullOrEmpty(g.evt)) _globals[g.evt] = g.to;
                }
            }

            EnterState(_fsm.startState);

            Debug.Log("[HyenaAI] FSM loaded: " + _fsm.name + " states=" + _fsm.states.Length
                + " globals=" + _globals.Count + " start=" + _fsm.startState
                + " target=" + (target != null ? target.name : "null"), this);
        }

        private void Start()
        {
            if (_fsm == null) LoadFsm();
        }

        // =====================================================================
        // 主循环
        // =====================================================================

        public override void Tick()
        {
            if (_fsm == null || _health == null || _health.IsDead) return;

            // 束缚期间交给 EnemyCombat
            if (_combat != null && (_combat.IsMountAttacking || _combat.IsPostKillFrozen)) return;

            float dt = Time.deltaTime;
            _stateTime += dt;
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
            if (_jumpCooldownTimer > 0f) _jumpCooldownTimer -= dt;
            if (_alertGrace > 0f) _alertGrace -= dt;
            if (_aggroTimer > 0f) _aggroTimer -= dt;
            if (_roamTimer > 0f) _roamTimer -= dt;
            if (_mountCooldownTimer > 0f) _mountCooldownTimer -= dt;

            // 原版每帧同步 Speed 参数（Movement 是 BlendTree，靠它决定迈步速度）
            Anim.SetFloat("Speed", Motor.Speed);
            _playerDetected = target != null && HorizontalDistanceTo(target) <= viewDistance;

            TickState(dt);
            TickDrivers();

            if (logStateChanges && logHeartbeat)
            {
                _hbTimer += dt;
                if (_hbTimer >= 2f)
                {
                    _hbTimer = 0f;
                    Debug.Log("[HyenaAI] state=" + _state
                        + " target=" + (target != null ? target.name : "null")
                        + " dist=" + (target != null ? HorizontalDistanceTo(target).ToString("F1") : "-")
                        + " detected=" + _playerDetected
                        + " combat=" + (_combat != null ? (_combat.IsAttacking ? "attack" : "idle") : "none"), this);
                }
            }
        }

        /// <summary>根据距离/感知产生原版事件（替代 Sight / ClawAttack_Range / JumpAttack_Range FSM）。</summary>
        private void TickDrivers()
        {
            if (target == null) return;

            float dist = HorizontalDistanceTo(target);
            bool inFront = IsFacingTarget();
            bool rawSight = (inFront && dist <= sightForward) || (!inFront && dist <= sightBack);

            // ---- 感知 + 仇恨记忆：一旦发现，绕背后也不会立刻丢 ----
            if (rawSight)
            {
                _aggroTimer = aggroMemory;
                if (!_alerted)
                {
                    _alerted = true;
                    _alertGrace = 0.4f;   // 让 Spot 动画播一下
                    SendEvent("Spot");
                }
            }

            bool sighted = rawSight || _aggroTimer > 0f;

            if (!sighted)
            {
                if (dist > loseRange) _alerted = false;

                // ---- 原版 Roam：没发现玩家时来回巡逻 ----
                if (_state == "Idle" && _roamTimer <= 0f)
                {
                    _roamTimer = roamInterval;
                    SendEvent("Roam");
                }
                return;
            }

            if (_alertGrace > 0f) return;

            bool combatBusy = _combat != null && _combat.IsAttacking;
            if (combatBusy) return;

            // ---- 束缚/擒抱：贴身时优先 ----
            if (dist <= mountRange && _mountCooldownTimer <= 0f
                && _combat != null && _combat.CanMountAttack)
            {
                _mountCooldownTimer = mountCooldown;
                FaceTarget();
                _combat.PerformMountAttack();
                return;
            }

            // ---- 原版 ClawAttack_Range ----
            if (dist <= attackRange && _attackCooldownTimer <= 0f)
            {
                _attackCooldownTimer = attackCooldown;
                SendEvent("CLAW");
                return;
            }

            // ---- 原版 JumpAttack_Range ----
            if (dist > attackRange && dist <= jumpAttackRange
                && _jumpCooldownTimer <= 0f && !IsAttackState())
            {
                _jumpCooldownTimer = jumpAttackCooldown;
                SendEvent("Jump_Attack");
                return;
            }

            if (IsMoveState(_state)) return;
            if (IsAttackState()) return;

            SendEvent(dist >= runThreshold ? "RUN" : "WALK");
        }

        /// <summary>原版 JumpAttack_HitBox 的等效判定：在身前一个盒范围内结算伤害。</summary>
        private void DealJumpAttackDamage()
        {
            Vector2 center = (Vector2)transform.position
                + new Vector2(jumpHitBoxOffset.x * Motor.Facing, jumpHitBoxOffset.y);

            var hits = Physics2D.OverlapBoxAll(center, jumpHitBoxSize, 0f);
            if (hits == null) return;

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                var dmg = col.GetComponentInParent<Player.IDamageable>();
                if (dmg == null || dmg.IsDead) continue;

                Vector2 kb = new Vector2(Motor.Facing * 10f, 6f);
                dmg.TakeDamage(jumpAttackDamage, kb, gameObject);
                break;
            }
        }

        private bool IsFacingTarget()
        {
            if (target == null) return true;
            float dx = target.position.x - transform.position.x;
            return (dx >= 0f) == (Motor.Facing > 0);
        }

        private bool HasTransition(string evt)
        {
            if (_fsm == null) return false;
            var st = _fsm.FindState(_state);
            return st != null && !string.IsNullOrEmpty(st.TargetStateFor(evt));
        }

        public string DebugAnimInfo => Anim != null ? Anim.CurrentClipName + " t=" + Anim.CurrentNormalizedTime.ToString("F2") : "-";

        private bool IsAttackState()
        {
            switch (_state)
            {
                case "ClawAttack":
                case "State 15":
                case "State 24":
                case "JumpAttack":
                case "State 21":
                case "State 23":
                case "HitEvent":
                case "State 76":
                case "State 41":
                case "State 25":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMoveState(string s)
        {
            switch (s)
            {
                case "Walk":
                case "Walking":
                case "State 61":
                case "Walk 2":
                case "State 68":
                case "RunUp":
                case "Walking 2":
                case "MovementLoop":
                    return true;
                default:
                    return false;
            }
        }


        private void TickState(float dt)
        {
            switch (_state)
            {
                case "INIT":
                    if (_stateTime >= initDelay) SendEvent("FINISHED");
                    break;

                case "State 9":
                    if (_stateTime >= difficultyDelay) SendEvent("Normal");
                    break;

                case "Easy":
                case "Normal":
                case "Hard":
                    if (_stateTime >= difficultyDelay) SendEvent("FINISHED");
                    break;

                case "Spot":
                case "State 2":
                    // 原地警戒，等待 WALK / CLAW / RUN
                    Motor.Stop();
                    FaceTarget();
                    break;

                case "Surprised":
                    Motor.Stop();
                    FaceTarget();
                    if (_stateTime >= surprisedDelay) SendEvent("Next");
                    break;

                case "Walk":
                case "State 61":
                case "Walk 2":
                case "State 68":
                    if (_stateTime >= 0.15f) SendEvent("FINISHED");
                    break;

                case "Walking":
                case "Walking 2":
                case "RunUp":
                case "Walking 3":
                    Approach(_state.StartsWith("Walking 2") || _state == "RunUp" ? runSpeed : walkSpeed, dt);
                    if (_stateTime >= walkStepDelay) SendEvent("FINISHED");
                    break;

                case "MovementLoop":
                    Approach(target != null && HorizontalDistanceTo(target) > attackRange ? runSpeed : 0f, dt);
                    break;

                case "ClawAttack":
                    Motor.Stop();
                    if (_attackFiredAt < 0f && _stateTime >= clawWindup)
                    {
                        _attackFiredAt = _stateTime;
                        if (_combat != null) _combat.PerformAttack();
                    }
                    if (_stateTime >= clawWindup + 0.4f) SendEvent("Claw");
                    break;

                case "State 15":
                    if (_stateTime >= clawRecover * 0.5f) SendEvent("Claw_End");
                    break;

                case "State 24":
                    if (_stateTime >= clawRecover) SendEvent("FINISHED");
                    break;

                case "JumpAttack":
                    Motor.Stop();
                    FaceTarget();
                    if (_stateTime >= 0.25f) SendEvent("JAttack");
                    break;

                case "State 21":
                    if (_attackFiredAt < 0f && _stateTime >= 0.08f)
                    {
                        _attackFiredAt = _stateTime;
                        DealJumpAttackDamage();
                    }
                    if (_stateTime >= jumpAttackDelay * 0.4f) SendEvent("JAttack_End");
                    break;

                case "State 23":
                    if (_stateTime >= jumpAttackDelay) SendEvent("Next");
                    break;

                case "HitEvent":
                    Motor.Stop();
                    if (_stateTime >= hitDelay) SendEvent("FINISHED");
                    break;

                case "State 65":
                    if (_stateTime >= 0.1f) SendEvent("FINISHED");
                    break;

                case "State 13":
                    // 原版巡逻入口：随机选左右
                    if (_stateTime >= 0.1f) SendEvent(Random.value < 0.5f ? "RoamLeft" : "RoamRight");
                    break;

                case "State 14":
                case "State 17":
                    Motor.MoveTo(new Vector2(_roamDir, 0f), walkSpeed);
                    if (_stateTime >= roamStepTime) SendEvent("FINISHED");
                    break;

                case "State 16":
                case "State 18":
                    Motor.Stop();
                    if (_stateTime >= roamStepTime * 0.6f) SendEvent("FINISHED");
                    break;

                case "State 35":
                    Motor.Stop();
                    if (_stateTime >= hitDelay) SendEvent("Next");
                    break;

                case "State 76":
                case "State 41":
                    SendEvent("do");
                    break;

                case "Break":
                    Motor.Stop();
                    if (_stateTime >= breakDelay) SendEvent("Next");
                    break;

                case "Step_Up":
                case "Climb_Up":
                case "Step_Down":
                case "Climb_Down":
                    Motor.Stop();
                    if (_stateTime >= 0.3f) SendEvent(_state == "Climb_Up" ? "Jump_High" : "Jump");
                    break;

                case "Climbed":
                case "State 5":
                case "State 72":
                case "State 69":
                    Motor.Stop();
                    break;

                default:
                    Motor.Stop();
                    // 没有出口的终点状态：给个保底，防止永久卡死
                    if (_stateTime > 0.8f && HasTransition("Next")) SendEvent("Next");
                    break;
            }
        }

        /// <summary>只有当前没在播这个状态时才 Play，避免每帧/每次切状态把动画重置到第 0 帧。</summary>
        private void EnsureAnim(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;
            if (!Anim.HasController)
            {
                if (!_warnedNoController)
                {
                    _warnedNoController = true;
                    Debug.LogWarning("[HyenaAI] Animator 没有 Controller，动画不会播放。", this);
                }
                return;
            }

            if (Anim.IsPlaying(stateName)) return;

            Anim.PlayState(stateName);

            if (logStateChanges && !_loggedAnim.TryGetValue(stateName, out _))
            {
                _loggedAnim[stateName] = true;
                Debug.Log("[HyenaAI] play anim '" + stateName + "' (animator now: " + Anim.CurrentClipName + ")", this);
            }
        }

        private void Approach(float speed, float dt)

        {
            if (target == null) { Motor.Stop(); return; }

            float dir = Mathf.Sign(target.position.x - transform.position.x);
            Motor.FaceDirection(new Vector2(dir, 0f));
            Motor.MoveTo(new Vector2(dir, 0f), speed);

            if (!Anim.IsPlaying(walkAnim)) EnsureAnim(walkAnim);
        }

        private void FaceTarget()
        {
            if (target == null) return;
            Motor.FaceDirection(new Vector2(Mathf.Sign(target.position.x - transform.position.x), 0f));
        }

        // =====================================================================
        // 状态机
        // =====================================================================

        /// <summary>发送事件：先查全局跳转，再查当前状态的跳转。</summary>
        public void SendEvent(string evt)
        {
            if (_fsm == null || string.IsNullOrEmpty(evt)) return;

            string next;
            if (_globals.TryGetValue(evt, out next))
            {
                if (next == _state) return;   // 已在目标状态，避免每帧重入
                EnterState(next);
                return;
            }

            var st = _fsm.FindState(_state);
            if (st != null)
            {
                next = st.TargetStateFor(evt);
                if (!string.IsNullOrEmpty(next) && next != _state)
                {
                    EnterState(next);
                }
            }
        }

        private void EnterState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;

            if (logStateChanges)
                Debug.Log("[HyenaAI] " + _state + " -> " + stateName, this);

            _state = stateName;
            _stateTime = 0f;
            _attackFiredAt = -1f;
            OnEnterState(stateName);
        }

        private void OnEnterState(string stateName)
        {
            switch (stateName)
            {
                case "INIT":
                    Motor.Stop();
                    if (Motor.IsFrozen) Motor.UnfreezePhysics();
                    Anim.ResumeAnimator();
                    break;

                case "State 9":
                    Motor.Stop();
                    break;

                case "Easy":
                case "Normal":
                case "Hard":
                    Motor.Stop();
                    break;

                case "Idle":
                    Motor.Stop();
                    EnsureAnim(idleAnim);
                    break;

                case "State 14":
                    _roamDir = -1;
                    Motor.FaceDirection(new Vector2(_roamDir, 0f));
                    EnsureAnim(walkAnim);
                    break;

                case "State 17":
                    _roamDir = 1;
                    Motor.FaceDirection(new Vector2(_roamDir, 0f));
                    EnsureAnim(walkAnim);
                    break;

                case "Spot":
                    Motor.Stop();
                    EnsureAnim(spotAnim);
                    FaceTarget();
                    break;

                case "Surprised":
                    Motor.Stop();
                    EnsureAnim(surprisedAnim);
                    FaceTarget();
                    break;

                case "Walk":
                case "Walking":
                case "State 61":
                case "Walk 2":
                case "Walking 2":
                case "RunUp":
                case "MovementLoop":
                    EnsureAnim(walkAnim);
                    break;

                case "ClawAttack":
                    Motor.Stop();
                    FaceTarget();
                    EnsureAnim(clawAnim);
                    break;

                case "JumpAttack":
                    Motor.Stop();
                    FaceTarget();
                    EnsureAnim(jumpAttackAnim);
                    break;

                case "HitEvent":
                    Motor.Stop();
                    EnsureAnim(hitAnim);
                    break;

                case "Break":
                    Motor.Stop();
                    EnsureAnim(breakAnim);
                    break;

                case "State 62":
                    Motor.Stop();
                    if (!_deathPlayed)
                    {
                        _deathPlayed = true;
                        EnsureAnim(deathAnim);
                    }
                    break;

                default:
                    break;
            }
        }

        // =====================================================================
        // 外部事件
        // =====================================================================

        public override void OnAnimEvent(string eventName)
        {
            SendEvent(eventName);
        }

        /// <summary>受击（EnemyHealth 调用）：原版走 FLINCH → State 35。</summary>
        public override void NotifyHit()
        {
            if (_health != null && _health.IsDead) return;
            SendEvent("FLINCH");
        }

        public override void OnDeath()
        {
            SendEvent("DO SCENE");
        }

        public void OnJumpHighEvent() { SendEvent("Jump_High"); }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, viewDistance);
        }
#endif
    }
}
