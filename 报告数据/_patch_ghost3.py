import io

# ---- PlayerEvade: 残影材质优先 Sprites/Default（透明混合，网格也能用） ----
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

old = '''            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return overlayMaterial;

            _ghostMaterial = new Material(shader) { name = "Afterimage(Unlit)", hideFlags = HideFlags.HideAndDontSave };
            _ghostMaterial.color = new Color(1f, 1f, 1f, 0.45f);

            return _ghostMaterial;'''

new = '''            // Sprites/Default：透明混合、网格也能用，URP 下可见
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) return overlayMaterial;

            _ghostMaterial = new Material(shader) { name = "Afterimage", hideFlags = HideFlags.HideAndDontSave };

            var ghostColor = new Color(1f, 1f, 1f, 0.5f);
            if (_ghostMaterial.HasProperty("_Color")) _ghostMaterial.SetColor("_Color", ghostColor);
            if (_ghostMaterial.HasProperty("_BaseColor")) _ghostMaterial.SetColor("_BaseColor", ghostColor);

            return _ghostMaterial;'''

if old in t:
    t = t.replace(old, new)
    print("ghost material patched")
else:
    print("ghost material block not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)

# ---- PlayerGun: 补 FollowCrouchBase ----
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

if "private void FollowCrouchBase()" not in s:
    anchor = "        private void TickIdle(PlayerIntent intent, bool wantAds)"
    i = s.index(anchor)
    method = '''        /// <summary>蹲姿基座保持（没有蹲瞄准状态时的兜底）。</summary>
        private void FollowCrouchBase()
        {
            if (_anim.IsPlaying(PlayerAnimDriver.States.Crouching))
            {
                _anim.SyncCurrent(PlayerAnimDriver.States.Crouching);
            }
            else
            {
                _anim.ForcePlay(PlayerAnimDriver.States.Crouching);
            }
        }

'''
    s = s[:i] + method + s[i:]

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
print("FollowCrouchBase added:", "private void FollowCrouchBase()" in s)
