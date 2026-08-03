using UnityEngine;

namespace Player
{
    /// <summary>
    /// Classic UnityEngine.Input reader (keyboard + mouse), producing a <see cref="PlayerIntent"/>.
    /// Keeps the original keyboard layout (A/D, W, S, Space, R, LMB/RMB, ...) but has no
    /// InControl / InControlManager dependency, so it works in a bare scene.
    /// Runs before PlayerController (execution order) so intent is fresh each frame.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Move keys")]
        [SerializeField] private KeyCode left = KeyCode.A;
        [SerializeField] private KeyCode right = KeyCode.D;
        [SerializeField] private KeyCode up = KeyCode.W;
        [SerializeField] private KeyCode down = KeyCode.S;

        [Header("Action keys")]
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

        [Header("Options")]
        [Tooltip("Also read arrow keys for horizontal move.")]
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
                // Orthographic 2D: ScreenToWorldPoint keeps z from the camera plane.
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

            // Melee triggers off the dedicated slash key, or off Fire when not aiming
            // (mirrors the original FIRE -> GAME_SLASH2 routing).
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
