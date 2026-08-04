using UnityEngine;

namespace Player
{
    public enum PlayerLocoState
    {
        Idle,
        Run,
        Stop
    }

    /// <summary>
    /// 地面移动：Idle / Run / 软 A_to_B 退出
    ///（<c>Run_to_Idle</c>、<c>Crouch_To_Idle</c>、起身 <c>Slide_To_Idle</c>、
    /// 软 <c>Landing_to_Run*</c>）。
    /// 朝向反转会翻转 scale 并留在 Run（不走 Run_Turning）。
    /// </summary>
    public class PlayerLocomotion
    {
        private bool _stopping;
        private bool _stopClipSeen;
        private float _stopLock;
        private PlayerLocoState _state = PlayerLocoState.Idle;

        public PlayerLocoState State => _state;
        /// <summary>未使用 — Run_Turning 已移除；保留以使 Arbiter 快照保持稳定。</summary>
        public bool TurnLockActive => false;
        public bool IsStopping => _stopping;

        public void TickTimers(float dt)
        {
            if (_stopLock > 0f)
            {
                _stopLock -= dt;
            }
        }

        public void Tick(PlayerIntent intent, PlayerContext ctx, in PlayerCapabilities caps)
        {
            var motor = ctx.Motor;
            var anim = ctx.Anim;
            var audio = ctx.Audio;

            if (caps.BlockLocomotionAnim || caps.AnimOwner != PlayerAnimOwner.Locomotion)
            {
                ClearStop();
                _state = PlayerLocoState.Idle;
                return;
            }

            if (!motor.IsGrounded)
            {
                ClearStop();
                _state = PlayerLocoState.Idle;
                return;
            }

            float move = intent.Move;
            bool hasMove = Mathf.Abs(move) > 0.1f;

            // 软 A_to_B 站立/退出片段 — 保持到播完；有移动则切到 Run。
            string softExit = SoftExitTransition(anim);
            if (softExit != null)
            {
                ClearStop();
                if (hasMove)
                {
                    ApplyFacing(intent, motor, move);
                    anim.ForcePlay(PlayerAnimDriver.States.Run);
                    audio?.TryPlayFootstep();
                    _state = PlayerLocoState.Run;
                    return;
                }

                anim.SyncCurrent(softExit);
                if (anim.BaseFinished)
                {
                    anim.PlayBase(PlayerAnimDriver.States.Idle);
                    _state = PlayerLocoState.Idle;
                }
                else
                {
                    _state = PlayerLocoState.Idle;
                }

                return;
            }

            if (hasMove)
            {
                bool wasStopping = _stopping;
                ClearStop();
                ApplyFacing(intent, motor, move);

                // 软 Landing_to_Run：仅视觉 — 播完 / 已在移动时让给 Run。
                string landRun = LandToRunState(anim);
                if (landRun != null && !anim.BaseFinished)
                {
                    anim.SyncCurrent(landRun);
                    audio?.TryPlayFootstep();
                    _state = PlayerLocoState.Run;
                    return;
                }

                if (wasStopping || !anim.IsPlaying(PlayerAnimDriver.States.Run))
                {
                    anim.ForcePlay(PlayerAnimDriver.States.Run);
                }
                else
                {
                    anim.PlayBase(PlayerAnimDriver.States.Run);
                }

                audio?.TryPlayFootstep();
                _state = PlayerLocoState.Run;
                return;
            }

            if (_stopping)
            {
                _state = PlayerLocoState.Stop;
                if (anim.IsPlaying(PlayerAnimDriver.States.RunToIdle))
                {
                    _stopClipSeen = true;
                    anim.SyncCurrent(PlayerAnimDriver.States.RunToIdle);
                    if (anim.BaseFinished || _stopLock <= 0f)
                    {
                        FinishStopToIdle(anim);
                    }
                }
                else if (!_stopClipSeen && _stopLock > 0f)
                {
                    // ForcePlay 可能要到下一帧 Animator 更新才生效 — 等待，不要重开。
                    anim.SyncCurrent(PlayerAnimDriver.States.RunToIdle);
                }
                else
                {
                    // 片段已出现后被 Mecanim exitTime 切走，或安全超时。
                    FinishStopToIdle(anim);
                }

                return;
            }

            // 仅在确实由 Run 持有时进入停止 — 不要因 Idle 后残留的平滑 vx
            // 再次进入停止（高速松键曾导致 Run_to_Idle ↔ Idle 循环）。
            bool wasRunning = anim.IsPlaying(PlayerAnimDriver.States.Run)
                || _state == PlayerLocoState.Run;

            if (wasRunning)
            {
                _stopping = true;
                _stopClipSeen = false;
                _stopLock = PlayerAnimTimings.RunToIdle.ClipLength + 0.05f;
                anim.ForcePlay(PlayerAnimDriver.States.RunToIdle);
                _state = PlayerLocoState.Stop;
                return;
            }

            anim.PlayBase(PlayerAnimDriver.States.Idle);
            _state = PlayerLocoState.Idle;
        }

        private void FinishStopToIdle(PlayerAnimDriver anim)
        {
            ClearStop();
            if (!anim.IsPlaying(PlayerAnimDriver.States.Idle))
            {
                anim.PlayBase(PlayerAnimDriver.States.Idle);
            }
            else
            {
                anim.SyncCurrent(PlayerAnimDriver.States.Idle);
            }

            _state = PlayerLocoState.Idle;
        }

        private static void ApplyFacing(PlayerIntent intent, PlayerMotor motor, float move)
        {
            int desired = move > 0f ? 1 : -1;
            if (desired != motor.Facing
                && !PlayerJump.IsHoldingBackForFlip(move, motor.Facing, intent.Jump))
            {
                motor.ForceFacing(desired);
            }
        }

        /// <summary>
        /// 由 loco 以软保持方式持有的站立/退出过渡。
        ///（SlideToCrouch 被阻塞时的 Slide_To_Idle 经 CrouchIsBusy → BlockLocomotionAnim。）
        /// </summary>
        private static string SoftExitTransition(PlayerAnimDriver anim)
        {
            if (anim.IsPlaying(PlayerAnimDriver.States.CrouchToIdle))
            {
                return PlayerAnimDriver.States.CrouchToIdle;
            }

            if (anim.IsPlaying(PlayerAnimDriver.States.SlideToIdle))
            {
                return PlayerAnimDriver.States.SlideToIdle;
            }

            return null;
        }

        private static string LandToRunState(PlayerAnimDriver anim)
        {
            if (anim.IsPlaying(PlayerAnimDriver.States.LandingToRun))
            {
                return PlayerAnimDriver.States.LandingToRun;
            }

            if (anim.IsPlaying(PlayerAnimDriver.States.LandingToRunForward))
            {
                return PlayerAnimDriver.States.LandingToRunForward;
            }

            return null;
        }

        private void ClearStop()
        {
            _stopping = false;
            _stopClipSeen = false;
            _stopLock = 0f;
        }
    }
}
