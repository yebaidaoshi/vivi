using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player
{
    public enum PlayerMagicPhase
    {
        Idle,
        ChannelIntro,
        ChannelHold,
        Cast,
        Cancel
    }

    /// <summary>
    /// floor Magic FSM（LeftShift / GAME_SKILL + LMB / GAME_FIRE）：
    /// 按住 Shift → 引导（ManaFlow + Magic_Channel*）；
    /// 按住 Shift 时 LMB → Magic_FrontCast + WindMagic；
    /// 单独松开 Shift → Magic_Channel_Cancel。
    /// 空中引导使用 OnAir 动画；仍在蓄力时落地切换到地面引导动画。
    /// </summary>
    public class PlayerMagic : MonoBehaviour
    {
        [Header("预制体（Monkey / floor 魔法工具集）")]
        [SerializeField] private GameObject manaFlowPrefab;
        [SerializeField] private GameObject windMagicPrefab;
        [SerializeField] private float manaFlowScale = 3f;
        [SerializeField] private Vector2 windMagicOffset = Vector2.zero;
        [SerializeField] private float windMagicLifetime = 2f;

        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerAudio _audio;
        private PlayerMotorSettings _settings;

        private PlayerMagicPhase _phase = PlayerMagicPhase.Idle;
        private float _phaseTimer;
        private bool _wasSkill;
        /// <summary>在引导开始时冻结，以免地面/空中动画在蓄力中途翻转。</summary>
        private bool _airChannel;
        private GameObject _manaFlow;

        public PlayerMagicPhase Phase => _phase;
        public bool IsBusy => _phase != PlayerMagicPhase.Idle;
        public bool LocksMovement => _phase == PlayerMagicPhase.ChannelIntro
            || _phase == PlayerMagicPhase.ChannelHold
            || _phase == PlayerMagicPhase.Cast
            || _phase == PlayerMagicPhase.Cancel;
        public bool LocksActions => LocksMovement;

        /// <summary>魔法占用移动时：钉住速度（空中 = 悬停 / 无重力）。</summary>
        public bool HasVelocityOverride => IsBusy;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            ResolvePrefabs();
        }

        private void OnDisable()
        {
            StopManaFlow();
            _phase = PlayerMagicPhase.Idle;
            _airChannel = false;
        }

        private void ResolvePrefabs()
        {
#if UNITY_EDITOR
			if (manaFlowPrefab == null)
			{
				manaFlowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
					"Assets/GameObject/ManaFlow.prefab");
			}

			if (windMagicPrefab == null)
			{
				windMagicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
					"Assets/GameObject/WindMagic.prefab");
			}
#endif
        }

        public void ApplyFixedVelocity()
        {
            if (_motor == null || !HasVelocityOverride)
            {
                return;
            }

            // Chronos 在自己的时间线上积分重力 — 每个 FixedUpdate 清零速度，
            // 使空中引导悬停、地面引导定住。
            _motor.SetVelocity(Vector2.zero);
        }

        public void Cancel()
        {
            if (_phase == PlayerMagicPhase.Idle)
            {
                return;
            }

            if (_phase == PlayerMagicPhase.Cast)
            {
                EndMagic();
                return;
            }

            BeginCancel();
        }

        public void Tick(PlayerIntent intent, bool canMagic)
        {
            float dt = _motor != null ? _motor.DeltaTime : Time.deltaTime;
            bool skill = intent.Skill;
            bool skillReleased = _wasSkill && !skill;
            _wasSkill = skill;

            switch (_phase)
            {
                case PlayerMagicPhase.Idle:
                    if (canMagic && skill)
                    {
                        BeginChannel();
                    }
                    break;

                case PlayerMagicPhase.ChannelIntro:
                    _phaseTimer -= dt;
                    EnsureManaFlow();
                    TryLandChannelToGround();

                    if (skillReleased)
                    {
                        BeginCancel();
                        break;
                    }

                    if (skill && intent.FirePressed)
                    {
                        BeginCast();
                        break;
                    }

                    // 动画器可能已经 exitTime 进入 Channeling — 跟随一次即可。
                    if (IsPlayingHold() || _phaseTimer <= 0f || ChannelIntroFinished())
                    {
                        BeginChannelHold();
                    }
                    else
                    {
                        _anim.SyncCurrent(IntroState());
                    }
                    break;

                case PlayerMagicPhase.ChannelHold:
                    EnsureManaFlow();
                    TryLandChannelToGround();
                    _anim.SyncCurrent(HoldState());

                    if (skillReleased)
                    {
                        BeginCancel();
                        break;
                    }

                    if (skill && intent.FirePressed)
                    {
                        BeginCast();
                    }
                    break;

                case PlayerMagicPhase.Cast:
                    _phaseTimer -= dt;
                    _anim.SyncCurrent(PlayerAnimDriver.States.MagicFrontCast);
                    if (_phaseTimer <= 0f || _anim.BaseFinished
                        || !_anim.IsPlaying(PlayerAnimDriver.States.MagicFrontCast))
                    {
                        EndMagic();
                    }
                    break;

                case PlayerMagicPhase.Cancel:
                    _phaseTimer -= dt;
                    _anim.SyncCurrent(CancelState());
                    if (_phaseTimer <= 0f || _anim.BaseFinished
                        || (!_anim.IsPlaying(PlayerAnimDriver.States.MagicChannelCancel)
                            && !_anim.IsPlaying(PlayerAnimDriver.States.MagicChannelCancelOnAir)))
                    {
                        EndMagic();
                    }
                    break;
            }
        }

        private void BeginChannel()
        {
            _airChannel = _motor != null && !_motor.IsGrounded;
            _phase = PlayerMagicPhase.ChannelIntro;
            _phaseTimer = _airChannel
                ? PlayerAnimTimings.MagicChannelOnAir.ClipLength
                : PlayerAnimTimings.MagicChannel.ClipLength;

            // 播放一次 intro（Magic_Channel / Magic_Channel_OnAir → 片段 Magic_Magic_Channel*）。
            // ChannelIntro 之后只 SyncCurrent，以免与 Mecanim exitTime → Channeling* 打架。
            _anim.ForcePlay(IntroState());
            if (_airChannel && _motor != null)
            {
                _motor.SetVelocity(Vector2.zero);
            }

            EnsureManaFlow();
        }

        private void BeginChannelHold()
        {
            _phase = PlayerMagicPhase.ChannelHold;
            _phaseTimer = 0f;
            if (!_anim.IsPlaying(HoldState()))
            {
                _anim.ForcePlay(HoldState());
            }
            else
            {
                _anim.SyncCurrent(HoldState());
            }

            EnsureManaFlow();
        }

        /// <summary>
        /// 下落中空中蓄力：落地后继续蓄力，切换到地面引导动画。
        /// 重力 / 速度覆盖保持原样（目前未完全取消）。
        /// </summary>
        private void TryLandChannelToGround()
        {
            if (_motor == null || !_motor.IsGrounded)
            {
                return;
            }

            bool playingAirHold = _anim != null
                && (_anim.IsPlaying(PlayerAnimDriver.States.MagicChannelingOnAir)
                    || _anim.IsPlaying(PlayerAnimDriver.States.MagicChannelOnAir));
            if (!_airChannel && !playingAirHold)
            {
                return;
            }

            _airChannel = false;

            if (_phase == PlayerMagicPhase.ChannelHold)
            {
                _phaseTimer = 0f;
                _anim.ForcePlay(PlayerAnimDriver.States.MagicChanneling);
            }
            else
            {
                // 仍在空中 intro — 继续用地面 intro。
                _phase = PlayerMagicPhase.ChannelIntro;
                _phaseTimer = PlayerAnimTimings.MagicChannel.ClipLength;
                _anim.ForcePlay(PlayerAnimDriver.States.MagicChannel);
            }
        }

        private void BeginCast()
        {
            StopManaFlow();
            _phase = PlayerMagicPhase.Cast;
            _phaseTimer = PlayerAnimTimings.MagicFrontCast.ClipLength + 0.05f;
            _anim.ForcePlay(PlayerAnimDriver.States.MagicFrontCast);
            _audio?.PlayWindMagic();

            int facing = _motor != null ? _motor.Facing : 1;
            PlayerVfx.SpawnOneShot(windMagicPrefab, transform, windMagicOffset, facing,
                mirrorByFacing: true, lifetime: windMagicLifetime);
        }

        private void BeginCancel()
        {
            StopManaFlow();
            _phase = PlayerMagicPhase.Cancel;
            _phaseTimer = _airChannel
                ? PlayerAnimTimings.MagicChannelCancelOnAir.ClipLength + 0.05f
                : PlayerAnimTimings.MagicChannelCancel.ClipLength + 0.05f;
            _anim.ForcePlay(CancelState());
        }

        private void EndMagic()
        {
            StopManaFlow();
            _phase = PlayerMagicPhase.Idle;
            _phaseTimer = 0f;
            _airChannel = false;
        }

        private string IntroState() => _airChannel
            ? PlayerAnimDriver.States.MagicChannelOnAir
            : PlayerAnimDriver.States.MagicChannel;

        private string HoldState() => _airChannel
            ? PlayerAnimDriver.States.MagicChannelingOnAir
            : PlayerAnimDriver.States.MagicChanneling;

        private string CancelState() => _airChannel
            ? PlayerAnimDriver.States.MagicChannelCancelOnAir
            : PlayerAnimDriver.States.MagicChannelCancel;

        private bool ChannelIntroFinished()
        {
            return _anim.IsPlaying(IntroState()) && _anim.BaseFinished;
        }

        private bool IsPlayingHold()
        {
            return _anim.IsPlaying(HoldState());
        }

        private void EnsureManaFlow()
        {
            if (_manaFlow != null || manaFlowPrefab == null)
            {
                return;
            }

            _manaFlow = Instantiate(manaFlowPrefab);
            _manaFlow.name = "ManaFlow_Charge";
            _manaFlow.transform.SetParent(transform, false);
            _manaFlow.transform.localPosition = Vector3.zero;
            _manaFlow.transform.localRotation = Quaternion.identity;
            _manaFlow.transform.localScale = Vector3.one * manaFlowScale;

            var systems = _manaFlow.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.loop = true;
                if (!systems[i].isPlaying)
                {
                    systems[i].Play(true);
                }
            }
        }

        private void StopManaFlow()
        {
            if (_manaFlow == null)
            {
                return;
            }

            PlayerVfx.StopAndDestroy(_manaFlow, 0.75f);
            _manaFlow = null;
        }
    }
}
