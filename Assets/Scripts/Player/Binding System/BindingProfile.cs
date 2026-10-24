using UnityEngine;

namespace Player
{
    [System.Flags]
    public enum StruggleKeys
    {
        None  = 0,
        Left  = 1 << 0,   // A
        Right = 1 << 1,   // D
        Up    = 1 << 2,   // W
        Down  = 1 << 3,   // S
        LMB   = 1 << 4,   // 左键
        RMB   = 1 << 5,   // 右键
        All   = Left | Right | Up | Down | LMB | RMB
    }

    [CreateAssetMenu(fileName = "BindingProfile", menuName = "Player/Binding Profile")]
    public class BindingProfile : ScriptableObject
    {
        [Header("动画名（Spine/Animator 状态名）")]
        public string pinnedAnim = "Mounted";
        public string loopAnim   = "Mounted_HyenaLoop";
        public string escapeAnim = "Mounted_Escape";
        public string deadAnim   = "Mounted_Hyena_Dead";

        [Header("时长（秒）")]
        public float pinnedDuration = 1.1f;
        public float escapeDuration = 1.42f;
        public float deadDuration   = 2.4f;

        [Header("挣脱")]
        [Tooltip("允许用于挣扎的按键。")]
        public StruggleKeys allowedKeys = StruggleKeys.All;
        [Tooltip("每次有效按键增加的进度（0~1）。")]
        [Range(0.01f, 1f)] public float strugglePerPress = 0.1f;
        [Tooltip("两次按键之间的最短间隔，低于此值不计（防连发）。")]
        public float minPressInterval = 0.05f;
        [Tooltip("不按键时每秒衰减量（0 = 不衰减）。")]
        public float struggleDecayPerSec = 0f;

        [Header("音效")]
        public AudioClip struggleSe;
        public AudioClip escapeSe;
        public AudioClip deathSe;
    }
}