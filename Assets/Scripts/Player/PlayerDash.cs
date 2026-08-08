using UnityEngine;

namespace Player
{
    /// <summary>
    /// 连招冲刺步 Step_Forward2：Attack → Dash → Attack2 → Dash → Attack3 连段中，
    /// 在 Attackable 窗口内朝面向方向按 A/D（边沿触发）触发的短距离冲刺。
    /// 由 <see cref="PlayerMelee"/> 驱动时机（Attackable / SteppedForward 窗口判定，
    /// SlashPressed 边沿触发衔接下一段）——本类只负责位移冲量 + 动画 + 音效/VFX 挂点，
    /// 不做任何窗口 / 连招判定，也不接入 PlayerArbiter（近战 <c>_phase</c> 全程保持
    /// WindupLocked，Dash 期间与普通挥砍锁定语义一致：ADS / 后撤 / 跳跃仍被阻挡）。
    /// 22 帧 @60fps（0.3667s），Stepped_Forward 事件 @ 第8帧（0.1333s）。
    /// </summary>
    public class PlayerDash : MonoBehaviour
    {
        [Header("冲刺烟雾 VFX（可选，仿 BackStep Step_Smoke）")]
        [SerializeField] private GameObject stepSmokePrefab;
        [Tooltip("相对 _Heroine 根节点的偏移；x 随朝向镜像。")]
        [SerializeField] private Vector2 stepSmokeOffset = Vector2.zero;
        [SerializeField] private float stepSmokeLifetime = 1f;

        [Header("冲刺参数")]
        [Tooltip("冲刺冲量大小（沿面向方向，非叠加移动输入）。")]
        [SerializeField] private float dashImpulse = 35f;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private bool _active;
        private float _elapsed;
        private float _coastVx;
        private bool _hasSteppedSe;

        /// <summary>冲刺进行中（全程为硬位移——片段很短，不设软恢复阶段）。</summary>
        public bool IsActive => _active;
        public float Elapsed => _elapsed;
        /// <summary>Stepped_Forward 事件时刻 —— 冲刺→下一击的最早可衔接点。</summary>
        public float SteppedForwardAt => PlayerAnimTimings.StepForward2.SteppedForward;
        /// <summary>Step_Forward2 片段总长 —— 冲刺→下一击窗口的上限；片段播完即结束。</summary>
        public float ClipLength => PlayerAnimTimings.StepForward2.ClipLength;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolveVfx();
        }

        private void ResolveVfx()
        {
#if UNITY_EDITOR
            // 模块在运行时组合且无序列化引用；按路径拉取预制体。
            if (stepSmokePrefab == null)
            {
                stepSmokePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameObject/Step_Smoke.prefab");
            }
#endif
        }

        /// <summary>
        /// 由 PlayerMelee 在连招 Attackable 窗口内、朝面向方向按下 A/D 的那一帧调用。
        /// 不做任何门控检查 —— 调用方已经验证过窗口 / 地面 / 连招上限。
        /// </summary>
        public void Begin()
        {
            _active = true;
            _elapsed = 0f;
            _hasSteppedSe = false;

            int facing = _motor.Facing;
            _coastVx = facing * (dashImpulse > 0f ? dashImpulse : 35f);
            _motor.SetImmediateVelocityX(_coastVx);

            _anim.ForcePlay(PlayerAnimDriver.States.StepForward2);
            PlayerVfx.SpawnOneShot(stepSmokePrefab, transform, stepSmokeOffset, facing,
                true, stepSmokeLifetime);
        }

        /// <summary>推进冲刺计时。返回 true 表示片段已播完（调用方应结束 Dash，
        /// 不论是否已经衔接了下一击 —— 衔接发生时调用方会先行调用 <see cref="End"/>，
        /// 使本方法返回值在那种情况下不再被读取）。</summary>
        public bool Tick(float dt)
        {
            if (!_active)
            {
                return true;
            }

            _elapsed += dt;

            if (!_hasSteppedSe && _elapsed >= SteppedForwardAt)
            {
                _hasSteppedSe = true;
                _audio?.SendEvent("Stepped_Forward");
            }

            return _elapsed >= ClipLength;
        }

        public void ApplyFixedVelocity()
        {
            if (!_active)
            {
                return;
            }

            _motor.SetImmediateVelocityX(_coastVx);
            _motor.ClampFallSpeed();
        }

        public void End()
        {
            _active = false;
            _elapsed = 0f;
            _coastVx = 0f;
            _hasSteppedSe = false;
        }
    }
}