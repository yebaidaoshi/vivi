using UnityEngine;

namespace Player
{ 
    public struct PlayerIntent 
    {
    public float Move;
    public float Vertical;
    public float StickX;//用来存储摇杆在水平方向上的偏移量或输入值
    public float StickY;//用来存储摇杆在垂直方向上的偏移量或输入值
    //鼠标在 2D 游戏世界里的坐标只有 X 和 Y，Z 轴没有用
    //注意：只有当 HasAimPoint 为 true 时（比如鼠标没移出屏幕且存在摄像机）AimPoint 才有效
        public Vector2 AimPoint;
        public bool HasAimPoint;

        public bool Jump;
        public bool JumpPressed;//仅在按键被按下的第一帧返回
        public bool JumpReleased;//表示跳跃按键是否被释放

        public bool Crouch;
        public bool CrouchPressed;//仅在按键被按下的第一帧返回

        public bool Fire;
        public bool FirePressed;//仅在按键被按下的第一帧返回

        public bool Ads;//瞄准状态
        public bool StickAds;//摇杆瞄准状态

        public bool Slash;//近战攻击状态
        public bool SlashPressed;//仅在按键被按下的第一帧返回

        public bool Evade;//闪避状态
        public bool EvadePressed;//仅在按键被按下的第一帧返回
        
        public bool Reload;//换弹状态
        public bool ReloadPressed;//仅在按键被按下的第一帧返回

        public bool Skill;//技能状态
        public bool Skill2;
        public bool Skill3;
        public bool Berserk;//狂暴状态？！
        public bool Items;//物品栏状态？！
        public bool Menu;//菜单状态？！

        public bool MoveLeftPressed;   // A / 左方向键 本帧刚按下
        public bool MoveRightPressed;  // D / 右方向键 本帧刚按下
        /// <summary>来自 RMB/按键或摇杆的 ADS；派生属性，使枪/蹲/近战始终看到实时瞄准意图。</summary>
        public bool WantsAds => Ads || StickAds;
        //玩家是否想要中断软动作（Soft Action），例如在攻击或技能动画播放时，玩家可能希望通过某些输入来中断当前动作。
        public bool WantsSoftActionInterrupt =>
            Mathf.Abs(Move) > 0.1f //玩家移动
            || JumpPressed //玩家按了跳跃
            || Jump//玩家持续按住跳跃
            || SlashPressed//玩家按了攻击
            || WantsAds//玩家想要进入瞄准状态
            || EvadePressed//玩家按了闪避
            || Crouch//玩家按了下蹲 或 持续按下
            || ReloadPressed//玩家按了换弹
            || Skill;//玩家按了技能
        public bool ForwardPressed(int facing) => facing >= 0 ? MoveRightPressed : MoveLeftPressed;
    }


}
