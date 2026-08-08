using UnityEngine;

namespace Player
{
    /// <summary>
    /// 经典 UnityEngine.Input 读取器（键盘 + 鼠标），产出 <see cref="PlayerIntent"/>。
    /// 保留原键位布局（A/D、W、S、Space、R、LMB/RMB 等），但不依赖
    /// InControl / InControlManager，因此可在裸场景中使用。
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
        [SerializeField] private KeyCode jump = KeyCode.W;
        [SerializeField] private KeyCode evade = KeyCode.Space;
        [SerializeField] private KeyCode reload = KeyCode.R;
        [SerializeField] private KeyCode skill = KeyCode.LeftShift;
        [SerializeField] private KeyCode berserk = KeyCode.Q;
        [SerializeField] private KeyCode items = KeyCode.E;
        [SerializeField] private KeyCode menu = KeyCode.Escape;
        [SerializeField] private KeyCode keyFire = KeyCode.N;
        [SerializeField] private KeyCode keyAds = KeyCode.M;
        [SerializeField] private KeyCode slashKey = KeyCode.J;

        [Header("选项")]
        [Tooltip("同时用方向键读取水平移动。")]
        [SerializeField] private bool useArrowKeys = true;

        private PlayerIntent _intent;

        public PlayerIntent Intent => _intent;

        private void Update()
        {
            var prev = _intent;
            var i = default(PlayerIntent);

            float move = 0f;
            if (Held(left) || (useArrowKeys && Input.GetKey(KeyCode.LeftArrow))) move -= 1f;
            if (Held(right) || (useArrowKeys && Input.GetKey(KeyCode.RightArrow))) move += 1f;
            i.Move = move;
            i.MoveLeftPressed = Down(left) || (useArrowKeys && Input.GetKeyDown(KeyCode.LeftArrow));
            i.MoveRightPressed = Down(right) || (useArrowKeys && Input.GetKeyDown(KeyCode.RightArrow));


            float vertical = 0f;
            if (Held(up)) vertical += 1f;
            if (Held(down)) vertical -= 1f;
            i.Vertical = vertical;

            i.StickX = 0f;
            i.StickY = 0f;

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 screen = Input.mousePosition;
                // 正交 2D：ScreenToWorldPoint 保留来自相机平面的 z。
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

            bool fire = Input.GetMouseButton(0) || Held(keyFire);
            bool firePressed = Input.GetMouseButtonDown(0) || Down(keyFire);
            bool ads = Input.GetMouseButton(1) || Held(keyAds);

            i.Jump = Held(jump);
            i.JumpPressed = Down(jump);
            i.JumpReleased = Up(jump);

            i.Crouch = Held(down);
            i.CrouchPressed = Down(down) || (!prev.Crouch && i.Crouch);

            i.Fire = fire;
            i.FirePressed = firePressed;

            i.Ads = ads;
            i.StickAds = false;

            // 近战由专用斩击键触发，或在未瞄准时由 Fire 触发
            //（镜像原 FIRE -> GAME_SLASH2 路由）。
            bool slashHeld = Held(slashKey) || (fire && !ads);
            bool slashDown = Down(slashKey) || (firePressed && !ads);
            i.Slash = slashHeld;
            i.SlashPressed = slashDown;

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
