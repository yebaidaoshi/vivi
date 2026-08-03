using System;

namespace Player
{
    /// <summary>
    /// Shared per-frame context: services + layer snapshot + capabilities.
    /// Modules read this instead of holding sibling references.
    /// </summary>
    public class PlayerContext
    {
        public PlayerIntent Intent;
        public PlayerMotor Motor;
        public PlayerAnimDriver Anim;
        public PlayerAudio Audio;
        public PlayerMotorSettings Settings;
        public PlayerCapabilities Caps;
        public PlayerLayerSnapshot Layers;

        /// <summary>Wired by controller to <see cref="PlayerJump.NotifyJumpAttack"/>.</summary>
        public Action NotifyJumpAttack;

        public float DeltaTime => Motor != null ? Motor.DeltaTime : 0f;
        public bool IsGrounded => Layers.Grounded;
        public bool OnAir => Layers.JumpOnAir;
        public bool IsSliding => Layers.CrouchIsSliding;
        public bool IsCrouchBusy => Layers.CrouchIsBusy;

        public void Bind(
            out PlayerMotor motor,
            out PlayerAnimDriver anim,
            out PlayerAudio audio,
            out PlayerMotorSettings settings)
        {
            motor = Motor;
            anim = Anim;
            audio = Audio;
            settings = Settings;
        }
    }







}