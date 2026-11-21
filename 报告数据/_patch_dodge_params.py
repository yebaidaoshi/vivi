import io, re

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()
orig = t

def rep(old, new, why):
    global t
    if old in t:
        t = t.replace(old, new, 1)
        print("OK  ", why)
    else:
        print("MISS", why)

# ---- 1) 独立参数字段（加在 maxAfterimages 之后）----
rep(
"        [SerializeField] private int maxAfterimages = 24;",
"""        [SerializeField] private int maxAfterimages = 24;

        [Header("完美闪避残影（独立参数，不影响第三段攻击）")]
        [SerializeField] private Color dodgeAfterimageColor = new Color(0.55f, 0.82f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float dodgeGhostAlpha = 0.5f;
        [SerializeField] private int dodgeAfterimageCount = 4;
        [SerializeField] private float dodgeAfterimageInterval = 0.045f;
        [SerializeField] private float dodgeAfterimageLife = 0.3f;""",
"dodge fields")

# ---- 2) ghost 数据加色调/透明度 ----
rep(
"""            public float Age;
            public float Life;""",
"""            public float Age;
            public float Life;
            public Color Tint = Color.white;
            public float Alpha = 0.5f;""",
"ghost fields")

# ---- 3) trail 保存本次的参数 ----
rep(
"""            public void PlayAfterimageTrail(int count = -1, float interval = -1f)""",
"""            public void PlayAfterimageTrail(int count = -1, float interval = -1f, float life = -1f, Color? tint = null, float alpha = -1f)""",
"trail signature")

rep(
"""            _trailRemaining = count > 0 ? count : Mathf.Max(1, afterimageCount);
            _trailInterval = interval > 0f ? interval : Mathf.Max(0.005f, afterimageInterval);""",
"""            _trailRemaining = count > 0 ? count : Mathf.Max(1, afterimageCount);
            _trailInterval = interval > 0f ? interval : Mathf.Max(0.005f, afterimageInterval);
            _trailLife = life > 0f ? life : afterimageLifetime;
            _trailTint = tint ?? afterimageColor;
            _trailAlpha = alpha >= 0f ? alpha : ghostAlpha;""",
"trail params")

# ---- 4) trail 字段 ----
rep(
"""        private float _trailInterval;""",
"""        private float _trailInterval;
        private float _trailLife;
        private Color _trailTint = Color.white;
        private float _trailAlpha = 0.5f;""",
"trail fields")

# ---- 5) Tick 里用 trail 参数 ----
rep(
"""                    CaptureAfterimage(afterimageLifetime);""",
"""                    CaptureAfterimage(_trailLife, _trailTint, _trailAlpha);""",
"tick capture")

# ---- 6) CaptureAfterimage 签名 + 传入 ghost ----
rep(
"""        private bool CaptureAfterimage(float life)
        {""",
"""        private bool CaptureAfterimage(float life)
        {
            return CaptureAfterimage(life, afterimageColor, ghostAlpha);
        }

        private bool CaptureAfterimage(float life, Color tint, float alpha)
        {""",
"capture overload")

rep(
"""            var ghost = new AfterimageGhost { Life = Mathf.Max(0.02f, life) };""",
"""            var ghost = new AfterimageGhost
            {
                Life = Mathf.Max(0.02f, life),
                Tint = tint,
                Alpha = Mathf.Clamp01(alpha)
            };""",
"ghost init")

# ---- 7) ApplyGhostAlpha 用 ghost 自己的颜色/透明度 ----
rep(
"""            var c = afterimageColor;
            c.a = Mathf.Clamp01(ghostAlpha) * Mathf.Clamp01(fade);""",
"""            var c = ghost.Tint;
            c.a = Mathf.Clamp01(ghost.Alpha) * Mathf.Clamp01(fade);""",
"apply alpha")

# ---- 8) 完美闪避用自己的参数 ----
rep(
"""        public void SpawnDodgeAfterimages()
        {
            FlashSilhouette(0.28f);
        }""",
"""        public void SpawnDodgeAfterimages()
        {
            PlayAfterimageTrail(dodgeAfterimageCount, dodgeAfterimageInterval,
                dodgeAfterimageLife, dodgeAfterimageColor, dodgeGhostAlpha);
        }""",
"dodge params")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("changed:", t != orig)
