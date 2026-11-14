using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 眩晕槽（原版 StunGauge FSM 的等效实现）。
    /// 受击累积 → 满槽进入眩晕（播眩晕动画 + 通知 Brain）→ 眩晕结束后 RESET 回 INIT。
    /// </summary>
    public class EnemyStunGauge : MonoBehaviour
    {
        [Header("槽")]
        [SerializeField] private float maxStun = 75f;   // 子弹 5 伤害 × 15 发 ≈ 眩晕（原作 20 发击杀）
        [Tooltip("每秒自然衰减")]
        [SerializeField] private float decayPerSecond = 4f;
        [Tooltip("眩晕值按伤害比例累积（1 = 伤害 1 点加 1 点槽）")]
        [SerializeField] private float stunPerDamage = 1f;
        [Tooltip("每次受击至少累积的眩晕值")]
        [SerializeField] private float stunPerHitMin = 3f;

        [Header("眩晕")]
        [SerializeField] private float stunDuration = 3f;
        [SerializeField] private string stunAnim = "Break";
        [Tooltip("找不到 stunAnim 时依次尝试的备用动画")]
        [SerializeField] private string[] stunAnimFallbacks = { "Shake", "Surprised", "Idle_Caution" };
        [Tooltip("眩晕结束时发给 AI 的事件（原版 RESET）")]
        [SerializeField] private string recoverEvent = "RESET";

        private EnemyBrainBase _brain;
        private EnemyMotor _motor;
        private EnemyAnimDriver _anim;

        private string _resolvedStunAnim;
        private float _value;
        private float _stunTimer;

        public float Value => _value;
        public float MaxValue => maxStun;
        public bool IsStunned => _stunTimer > 0f;

        public void Init(EnemyBrainBase brain, EnemyMotor motor, EnemyAnimDriver anim)
        {
            _brain = brain;
            _motor = motor;
            _anim = anim;
            _value = 0f;
            _stunTimer = 0f;
        }

        public void AddStun(float amount)
        {
            if (IsStunned) return;
            if (GetComponent<EnemyHealth>() != null && GetComponent<EnemyHealth>().IsDead) return;
            _value += amount;
            if (_value >= maxStun) Break();
        }

        /// <summary>死亡/脱离时强制取消眩晕状态。</summary>
        public void Cancel()
        {
            _stunTimer = 0f;
            _value = 0f;
            var flasher = GetComponent<EnemyColorFlasher>();
            if (flasher != null) flasher.SetStunPulse(false);
        }

        public void AddHitStun(int damage)
        {
            AddStun(Mathf.Max(stunPerHitMin, damage * stunPerDamage));
        }

        private string ResolveStunAnim()
        {
            if (_anim == null) return null;

            // 原作眩晕动画就是 Break，优先用它
            if (_anim.FindStateLayer("Break") >= 0) return "Break";
            if (!string.IsNullOrEmpty(stunAnim) && _anim.FindStateLayer(stunAnim) >= 0) return stunAnim;

            if (stunAnimFallbacks != null)
            {
                for (int i = 0; i < stunAnimFallbacks.Length; i++)
                {
                    var name = stunAnimFallbacks[i];
                    if (!string.IsNullOrEmpty(name) && _anim.FindStateLayer(name) >= 0)
                    {                        return name;
                    }
                }
            }

            Debug.LogError("[EnemyStunGauge] 找不到任何可用的眩晕动画（" + stunAnim + "）", this);
            return null;
        }

        private void Break()
        {
            _value = 0f;
            _stunTimer = stunDuration;

            if (_motor != null) _motor.Stop();

            if (_anim == null) _anim = GetComponent<EnemyAnimDriver>();

            _resolvedStunAnim = ResolveStunAnim();            if (_anim != null && _resolvedStunAnim != null) _anim.PlayState(_resolvedStunAnim, 0f);

            var flasher = GetComponent<EnemyColorFlasher>();
            if (flasher != null) flasher.SetStunPulse(true);

            if (_brain != null) _brain.OnAnimEvent("Stun");
            var cam = SceneCamera.CameraDirector.Instance;
            if (cam != null) cam.Shake(0.2f, 0.4f);

            var sfx = GetComponent<EnemyAudio>();
            if (sfx != null) sfx.PlayStun();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_stunTimer > 0f)
            {
                // 眩晕动画循环播放，避免播完一遍后看起来"完全卡死"
                if (_anim != null && _resolvedStunAnim != null && !_anim.IsPlaying(_resolvedStunAnim))
                    _anim.PlayState(_resolvedStunAnim, 0f);

                _stunTimer -= dt;
                if (_stunTimer <= 0f)
                {
                    _stunTimer = 0f;
                    var flasher = GetComponent<EnemyColorFlasher>();
                    if (flasher != null) flasher.SetStunPulse(false);                    if (_brain != null && !string.IsNullOrEmpty(recoverEvent)) _brain.OnAnimEvent(recoverEvent);
                }
                return;
            }

            if (_value > 0f) _value = Mathf.Max(0f, _value - decayPerSecond * dt);
        }
    }
}
