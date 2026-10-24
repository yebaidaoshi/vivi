using UnityEngine;

namespace Player
{
    /// <summary>
    /// 经典 UnityEngine.Input 读取器（键盘 + 鼠标），产出 <see cref="PlayerIntent"/>。
    /// 所有键位（含鼠标）均可在 Inspector 中配置。
    /// 执行顺序早于 PlayerController，确保每帧意图为最新。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("移动键")]
        [SerializeField] private KeyCode left = KeyCode.A;
        [SerializeField] private KeyCode right = KeyCode.D;
        [SerializeField] private KeyCode up = KeyCode.W;
        [SerializeField] private KeyCode down = KeyCode.S;

        [Header("动作键")]
        [SerializeField] private KeyCode jump = KeyCode.Space;
        [SerializeField] private KeyCode evade = KeyCode.Space;
        [SerializeField] private KeyCode reload = KeyCode.R;
        [SerializeField] private KeyCode skill = KeyCode.LeftShift;
        [SerializeField] private KeyCode berserk = KeyCode.Q;
        [SerializeField] private KeyCode items = KeyCode.E;
        [SerializeField] private KeyCode menu = KeyCode.Escape;
        [SerializeField] private KeyCode keyFire = KeyCode.N;
        [SerializeField] private KeyCode keyAds = KeyCode.M;
        [SerializeField] private KeyCode slashKey = KeyCode.J;

        [Header("鼠标输入开关")]
        [Tooltip("允许鼠标左键触发开火。")]
        [SerializeField] private bool useMouseFire = true;
        [Tooltip("允许鼠标右键触发 ADS。")]
        [SerializeField] private bool useMouseAds = true;
        [Tooltip("允许鼠标左键在非 ADS 时触发斩击。")]
        [SerializeField] private bool useMouseSlash = true;

        [Header("选项")]
        [Tooltip("同时用方向键读取水平移动。")]
        [SerializeField] private bool useArrowKeys = true;
        [Tooltip("键盘左右方向键。")]
        [SerializeField] private KeyCode arrowLeft = KeyCode.LeftArrow;
        [Tooltip("键盘右方向键。")]
        [SerializeField] private KeyCode arrowRight = KeyCode.RightArrow;

        private PlayerIntent _intent;

        public PlayerIntent Intent => _intent;

        private void Update()
        {
            var prev = _intent;
            var i = default(PlayerIntent);

            // ---- 水平移动 ----
            float move = 0f;
            if (Held(left) || (useArrowKeys && Held(arrowLeft))) move -= 1f;
            if (Held(right) || (useArrowKeys && Held(arrowRight))) move += 1f;
            i.Move = move;
            i.MoveLeftPressed = Down(left) || (useArrowKeys && Down(arrowLeft));
            i.MoveRightPressed = Down(right) || (useArrowKeys && Down(arrowRight));

            // ---- 垂直（蹲下等） ----
            float vertical = 0f;
            if (Held(up)) vertical += 1f;
            if (Held(down)) vertical -= 1f;
            i.Vertical = vertical;

            i.StickX = 0f;
            i.StickY = 0f;

            // ---- 鼠标世界坐标 ----
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 screen = Input.mousePosition;
                screen.z = Mathf.Abs(cam.transform.position.z);
                Vector3 world = cam.ScreenToWorldPoint(screen);
                i.AimPoint = new Vector2(world.x, world.y);
                i.HasAimPoint = true;
            }
            else
            {
                i.AimPoint = Vector2.zero;
                i.HasAimPoint = false;
            }

            // ---- 开火 / ADS（键鼠组合） ----
            bool mouseFireHeld = useMouseFire && Input.GetMouseButton(0);
            bool mouseFireDown = useMouseFire && Input.GetMouseButtonDown(0);
            bool mouseAdsHeld = useMouseAds && Input.GetMouseButton(1);

            bool fire = mouseFireHeld || Held(keyFire);
            bool firePressed = mouseFireDown || Down(keyFire);
            bool ads = mouseAdsHeld || Held(keyAds);

            // ---- 跳跃 ----
            i.Jump = Held(jump);
            i.JumpPressed = Down(jump);
            i.JumpReleased = Up(jump);

            // ---- 蹲下 ----
            i.Crouch = Held(down);
            i.CrouchPressed = Down(down) || (!prev.Crouch && i.Crouch);

            // ---- 开火 ----
            i.Fire = fire;
            i.FirePressed = firePressed;

            // ---- ADS ----
            i.Ads = ads;
            i.StickAds = false;

            // ---- 近战斩击：专用键 + 可选鼠标左键（非 ADS 时） ----
            bool mouseSlashHeld = useMouseSlash && mouseFireHeld && !ads;
            bool mouseSlashDown = useMouseSlash && mouseFireDown && !ads;

            bool slashHeld = Held(slashKey) || mouseSlashHeld;
            bool slashDown = Down(slashKey) || mouseSlashDown;
            i.Slash = slashHeld;
            i.SlashPressed = slashDown;

            // ---- 闪避 / 换弹 / 技能 ----
            i.Evade = Held(evade);
            i.EvadePressed = Down(evade);

            i.Reload = Held(reload);
            i.ReloadPressed = Down(reload);

            i.Skill = Held(skill);
            i.Berserk = Held(berserk);
            i.Items = Held(items);
            i.Menu = Held(menu);

            _intent = i;
        }

        private static bool Held(KeyCode k) => k != KeyCode.None && Input.GetKey(k);
        private static bool Down(KeyCode k) => k != KeyCode.None && Input.GetKeyDown(k);
        private static bool Up(KeyCode k) => k != KeyCode.None && Input.GetKeyUp(k);
    }
}