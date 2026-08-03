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
    /// Ground locomotion: Idle / Run / soft A_to_B exits
    /// (<c>Run_to_Idle</c>, <c>Crouch_To_Idle</c>, stand-up <c>Slide_To_Idle</c>,
    /// soft <c>Landing_to_Run*</c>).
    /// Facing reverse flips scale and stays on Run (no Run_Turning).
    /// </summary>
    public class PlayerLocomotion
    {
        private bool _stopping;
        private bool _stopClipSeen;
        private float _stopLock;
        private PlayerLocoState _state = PlayerLocoState.Idle;

        public PlayerLocoState State => _state;
        /// <summary>Unused — Run_Turning removed; kept so Arbiter snapshot stays stable.</summary>
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

            // Soft A_to_B stand/exit clips — hold until done; move cuts to Run.
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

                // Soft Landing_to_Run: visual only — yield to Run when done / already moving.
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
                    // ForcePlay may not show until the next animator update — wait, do not restart.
                    anim.SyncCurrent(PlayerAnimDriver.States.RunToIdle);
                }
                else
                {
                    // Clip was seen then Mecanim exitTime'd out, or safety timeout.
                    FinishStopToIdle(anim);
                }

                return;
            }

            // Only actual Run ownership — do not re-enter stop from leftover smoothed vx
            // after Idle (high-speed release used to loop Run_to_Idle ↔ Idle).
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
        /// Stand / exit transitions owned by loco as soft holds.
        /// (Slide_To_Idle while SlideToCrouch is blocked via CrouchIsBusy → BlockLocomotionAnim.)
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
