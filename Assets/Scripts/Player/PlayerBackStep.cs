using UnityEngine;

namespace Player
{
    /// <summary>
    /// floor Movement BackStep:
    /// Impulse ±50, coast until anim Movable @ 0.3s (then velocity → 0).
    /// After Movable the remaining clip is soft — move / jump / attack may interrupt.
    /// </summary>
    public class PlayerBackStep : MonoBehaviour
    {
        [Header("Step smoke VFX (floor Movement BackStep CreateObject → Step_Smoke)")]
        [SerializeField] private GameObject stepSmokePrefab;
        [Tooltip("Offset from the _Heroine root; x mirrored by facing (floor GetScale → SetScale).")]
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

        /// <summary>Until Movable (0.3s) — hard velocity / move / jump lock.</summary>
        public bool IsActive => _moveLocked;
        /// <summary>True while backstep is active (hard coast or soft recovery anim).</summary>
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
			// Modules are composed at runtime with no serialized refs; pull the prefab by path.
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
            // floor BackStep CreateObject: one-shot Step_Smoke at the _Heroine root, facing-mirrored.
            PlayerVfx.SpawnOneShot(stepSmokePrefab, transform, stepSmokeOffset, _motor.Facing,
                true, stepSmokeLifetime);
            return true;
        }

        /// <summary>After Movable — cut soft recovery when another action takes over.</summary>
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
                // Soft recovery: do not force-own anim — loco / melee may interrupt.
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
