import io

# ---------- PlayerAnimDriver: 支持指定层 ----------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerAnimDriver.cs"
t = io.open(p, encoding="utf-8").read()

if "PlayByFullPathOnLayer" not in t:
    anchor = "        public static class States"
    i = t.index(anchor)
    method = '''        /// <summary>在指定层上按"子状态机/状态"路径播放。</summary>
        public bool PlayByFullPathOnLayer(string fullPath, int layer, float normalizedTime = 0f)
        {
            if (string.IsNullOrEmpty(fullPath)) return false;

            var a = GetComponent<Animator>();
            if (a == null || layer < 0 || layer >= a.layerCount) return false;

            var hash = Animator.StringToHash(fullPath);
            if (!a.HasState(layer, hash)) return false;

            a.Play(hash, layer, Mathf.Clamp01(normalizedTime));
            return true;
        }

'''
    t = t[:i] + method + t[i:]

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("driver patched")

# ---------- PlayerGun: 蹲瞄准优先 Aim 层（层1），再 Base 层 ----------
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

old = '''                    if (crouchHold != null && _anim.PlayByFullPath(crouchHold))
                    {
                        _phase = AdsPhase.Holding;
                        return;
                    }'''
new = '''                    if (crouchHold != null)
                    {
                        // 蹲瞄准：优先 Aim 层（层1），再 Base 层；短名/路径都试
                        var candidates = new[]
                        {
                            "Crouch/Crouch_Aim_SMG_Hold",
                            "Crouch_Aim_SMG_Hold",
                            "Crouch/Crouch_Aim_SMG",
                            "Crouch_Aim_SMG",
                        };

                        foreach (var name in candidates)
                        {
                            if (_anim.PlayByFullPathOnLayer(name, 1))
                            {
                                _phase = AdsPhase.Holding;
                                return;
                            }

                            if (_anim.PlayByFullPathOnLayer(name, 0))
                            {
                                _phase = AdsPhase.Holding;
                                return;
                            }
                        }
                    }'''
if old in s:
    s = s.replace(old, new, 1)
    print("idle crouch patched")
else:
    print("idle crouch block not found")

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
