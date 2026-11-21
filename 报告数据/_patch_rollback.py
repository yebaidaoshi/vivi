import io, re

# ---------- PlayerGun: 彻底回退蹲瞄准特判，恢复稳定行为 ----------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

# 1) TickHolding 里的蹲特判整块删掉
m = re.search(r"\n            if \(_aimCrouch\)\n            \{.*?\n                return;\n            \}\n", t, re.S)
if m:
    t = t[:m.start()] + "\n" + t[m.end():]
    print("crouch block removed")
else:
    print("crouch block not found")

# 2) 每帧把蹲权重复位（不蹲就 0），避免卡在蹲姿
t = t.replace("            _anim.SetCrouch(_aimCrouch);\n            UpdateAdsAim(intent, allowFaceFlip);",
              "            _anim.SetCrouch(_aimCrouch);\n            _anim.SetCrouchingWeight(_aimCrouch ? 1f : 0f);\n            UpdateAdsAim(intent, allowFaceFlip);", 1)

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)

# ---------- PlayerMelee: 去掉运动模糊调用（视为实验失败） ----------
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
s = io.open(g, encoding="utf-8").read()

old = '''            // 第三段挥砍的"残影"：用 URP 运动模糊做拖影，Spine 角色不需要克隆
            if (_combo >= 3)
            {
                var t = Mathf.Clamp01(1f - _elapsed / Mathf.Max(0.01f, afterimageWindow));
                SetComboMotionBlur(t * motionBlurIntensity);
            }
            else
            {
                SetComboMotionBlur(0f);
            }'''
new = '''            // 残影：实验性实现已停用（Spine 克隆/材质覆盖都不成立），改用原版剪影 overlay。'''
if old in s:
    s = s.replace(old, new, 1)
    print("motion blur call removed")
else:
    print("motion blur call not found")

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
