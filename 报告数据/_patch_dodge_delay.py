import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

def rep(old, new, why):
    global t
    if old in t:
        t = t.replace(old, new, 1)
        print("OK  ", why)
    else:
        print("MISS", why)

# 1) 新字段：闪避残影首张延迟
rep(
"        [SerializeField] private float dodgeAfterimageLife = 0.3f;",
"""        [SerializeField] private float dodgeAfterimageLife = 0.3f;
        [Tooltip("闪避残影的第一张延迟多少秒（想晚几帧出就调大，0.05 ≈ 3 帧）")]
        [SerializeField] private float dodgeAfterimageDelay = 0.06f;""",
"dodge delay field")

# 2) trail 增加 firstDelay 参数 + 字段
rep(
"        public void PlayAfterimageTrail(int count = -1, float interval = -1f, float life = -1f, Color? tint = null, float alpha = -1f)",
"        public void PlayAfterimageTrail(int count = -1, float interval = -1f, float life = -1f, Color? tint = null, float alpha = -1f, float firstDelay = -1f)",
"trail signature")

rep(
"                _trailTimer = Mathf.Max(0f, afterimageFirstDelay);",
"                _trailTimer = Mathf.Max(0f, firstDelay >= 0f ? firstDelay : afterimageFirstDelay);",
"trail first delay use")

# 3) 闪避用自己的首张延迟
rep(
"""            PlayAfterimageTrail(dodgeAfterimageCount, dodgeAfterimageInterval,
                dodgeAfterimageLife, dodgeAfterimageColor, dodgeGhostAlpha);""",
"""            PlayAfterimageTrail(dodgeAfterimageCount, dodgeAfterimageInterval,
                dodgeAfterimageLife, dodgeAfterimageColor, dodgeGhostAlpha, dodgeAfterimageDelay);""",
"dodge pass delay")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
