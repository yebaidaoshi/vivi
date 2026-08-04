using UnityEngine;

namespace Player
{
    /// <summary>
    /// floor Movement BackStep：
    /// 冲量 ±50，滑行直到动画 Movable @ 0.3s（然后速度 → 0）。
    /// Movable 之后剩余片段为软保持 — 移动 / 跳跃 / 攻击可打断。
    /// </summary>
    public class PlayerBackStep : MonoBehaviour
    {
        [Header("后撤烟雾 VFX（floor Movement BackStep CreateObject → Step_Smoke）")]
        [SerializeField] private GameObject stepSmokePrefab;
        [Tooltip("相对 _Heroine 根节点的偏移；x 随朝向镜像（floor GetScale → SetScale）。")]
        [SerializeField] private Vector2 stepSmokeOffset = Vector2.zero;
        [SerializeField] private float stepSmokeLifetime = 1.2f;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private bool _moveLocked;
        private bool _animHold;
        private float _coastTimer;
        private float _animTimer;
        private float _coastVx;

        /// <summary>直到 Movable（0.3s）— 硬锁定速度 / 移动 / 跳跃。</summary>
        public bool IsActive => _moveLocked;
        /// <summary>后撤进行中为真（硬滑行或软恢复动画）。</summary>
        public bool IsBusy => _moveLocked || _animHold;
        public bool HasVelocityOverride => _moveLocked;

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

        public bool TryStart()
        {
            if (_moveLocked || _animHold || !_motor.IsGrounded)
            {
                return false;
            }

            _moveLocked = true;
            _animHold = true;
            _coastTimer = PlayerAnimTimings.BackStep.Movable;
            _animTimer = PlayerAnimTimings.BackStep.ClipLength + 0.05f;

            int dir = -_motor.Facing;
            float impulse = _settings.backStepImpulse > 0f ? _settings.backStepImpulse : 50f;
            _coastVx = dir * impulse;

            _motor.SetImmediateVelocityX(0f);
            _motor.SetVelocityY(0f);
            _motor.AddForce(new Vector2(_coastVx, 0f), ForceMode2D.Impulse);
            _motor.SetImmediateVelocityX(_coastVx);

            _anim.ForcePlay(PlayerAnimDriver.States.BackStep);
            _audio?.PlayBackStep();
            // floor BackStep CreateObject：在 _Heroine 根节点生成一次性 Step_Smoke，随朝向镜像。
            PlayerVfx.SpawnOneShot(stepSmokePrefab, transform, stepSmokeOffset, _motor.Facing,
                true, stepSmokeLifetime);
            return true;
        }

        /// <summary>Movable 之后 — 当其他动作接管时切断软恢复。</summary>
        public void Interrupt()
        {
            if (_moveLocked)
            {
                return;
            }

            _animHold = false;
            _animTimer = 0f;
        }

        public void Tick()
        {
            if (!_moveLocked && !_animHold)
            {
                return;
            }

            float dt = _motor.DeltaTime;

            if (_moveLocked)
            {
                _coastTimer -= dt;
                _anim.SyncCurrent(PlayerAnimDriver.States.BackStep);
                if (_coastTimer <= 0f)
                {
                    _moveLocked = false;
                    _coastVx = 0f;
                    _motor.SetImmediateVelocityX(0f);
                }
            }
            else if (_animHold)
            {
                // 软恢复：不强行独占动画 — loco / 近战可打断。
                _animTimer -= dt;
                if (!_anim.IsPlaying(PlayerAnimDriver.States.BackStep)
                    || _anim.BaseFinished
                    || _animTimer <= 0f)
                {
                    _animHold = false;
                }
            }
        }

        public void ApplyFixedVelocity()
        {
            if (!_moveLocked)
            {
                return;
            }

            _motor.SetImmediateVelocityX(_coastVx);
            _motor.ClampFallSpeed();
        }
    }
}
