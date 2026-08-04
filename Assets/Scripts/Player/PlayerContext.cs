using System;

namespace Player
{
    /// <summary>
    /// 每帧共享上下文：服务引用 + 层级快照 + 能力集。
    /// 各模块读取此对象，而不再持有兄弟模块引用。
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

        /// <summary>由控制器接线到 <see cref="PlayerJump.NotifyJumpAttack"/>。</summary>
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
