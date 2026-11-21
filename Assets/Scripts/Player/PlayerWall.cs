using UnityEngine;

namespace Player
{
    /// <summary>
    /// 墙面系统（对应原版 Movement 的 <c>WallCheck2</c> / <c>WallStop</c>）：
    /// 空中贴墙 + 朝墙输入 → 限制下落速度（滑墙）。
    /// 原版的 <c>Climb</c> / <c>LedgeClimb</c> 需要专门的攀爬动画，先留接口不实现。
    /// </summary>
    public class PlayerWall
    {
        /// <summary>正在滑墙。</summary>
        public bool IsSliding { get; private set; }
        /// <summary>面朝方向有墙。</summary>
        public bool TouchingWall { get; private set; }
        /// <summary>墙在朝哪边（1 = 右侧，-1 = 左侧）。</summary>
        public int WallSide { get; private set; }

        public float slideSpeed = 3.2f;

        private PlayerMotor _motor;

        public void Init(PlayerContext ctx)
        {
            ctx.Bind(out _motor, out _, out _, out _);
        }

        public void Tick(PlayerIntent intent, bool onAir, bool allowWall)
        {
            if (_motor == null)
            {
                IsSliding = false;
                TouchingWall = false;
                return;
            }

            int side = _motor.Facing;
            WallSide = side;
            TouchingWall = side > 0 ? _motor.WallRight : _motor.WallLeft;

            if (!onAir || !allowWall || !TouchingWall)
            {
                IsSliding = false;
                return;
            }

            // 只有"朝墙压方向"才算滑墙（原版 WallStop 的触发条件）
            bool pressingIntoWall = Mathf.Abs(intent.Move) > 0.1f && Mathf.Sign(intent.Move) == side;
            IsSliding = pressingIntoWall;

            if (!IsSliding) return;

            var v = _motor.GetVelocity();

            if (v.y < -slideSpeed)
            {
                _motor.SetVelocityY(-slideSpeed);
            }
        }
    }
}
