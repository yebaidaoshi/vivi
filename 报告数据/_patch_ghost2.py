import io

# ---------- PlayerEvade: 残影用运行时 URP Unlit 材质兜底 ----------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

if "_ghostMaterial" not in t:
    t = t.replace("        private float _savedTimeScale", "        private float _savedTimeScale", 1)

    anchor = "        public void SpawnAfterimage(float life = 0.22f)"
    i = t.index(anchor)
    field = '''        private Material _ghostMaterial;

        /// <summary>残影材质：优先用 URP Unlit 运行时材质（原 PlayerSilhouette.mat 的 shader 可能不渲染）。</summary>
        private Material GhostMaterial()
        {
            if (_ghostMaterial != null) return _ghostMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return overlayMaterial;

            _ghostMaterial = new Material(shader) { name = "Afterimage(Unlit)", hideFlags = HideFlags.HideAndDontSave };
            _ghostMaterial.color = new Color(1f, 1f, 1f, 0.45f);

            return _ghostMaterial;
        }

'''
    t = t[:i] + field + t[i:]

# 残影渲染器改用 GhostMaterial
t = t.replace("            mr.sharedMaterial = overlayMaterial;\n            mr.shadowCastingMode = ShadowCastingMode.Off;",
              "            mr.sharedMaterial = GhostMaterial();\n            mr.shadowCastingMode = ShadowCastingMode.Off;")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)

# ---------- PlayerGun: 蹲瞄准不再强切状态下入奇怪姿势 ----------
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

# TickIdle 蹲下抬枪：静止才切蹲瞄准状态，移动中只保留蹲姿 + 瞄准层
old_idle = '''                if (_aimCrouch)
                {
                    var crouchHold = CrouchAimHoldState();
                    if (crouchHold != null)
                    {
                        _anim.ForcePlay(crouchHold);
                        _phase = AdsPhase.Holding;
                        return;
                    }
'''
new_idle = '''                if (_aimCrouch)
                {
                    var crouchHold = Mathf.Abs(intent.Move) > 0.1f ? null : CrouchAimHoldState();
                    if (crouchHold != null)
                    {
                        _anim.ForcePlay(crouchHold);
                        _phase = AdsPhase.Holding;
                        return;
                    }
'''
if old_idle in s:
    s = s.replace(old_idle, new_idle)
else:
    s = s.replace("if (crouchHold != null)\n                    {\n                        _anim.ForcePlay(crouchHold);\n                        _phase = AdsPhase.Holding;\n                        return;\n                    }",
                  "if (crouchHold != null)\n                    {\n                        _anim.ForcePlay(crouchHold);\n                        _phase = AdsPhase.Holding;\n                        return;\n                    }")

# TickHolding 里进入蹲瞄准：移动中不要强切
old_hold = "                    var crouchHold = CrouchAimHoldState();\n                    if (crouchHold != null) _anim.ForcePlay(crouchHold);\n                    else { _anim.SetCrouch(true); _anim.SetCrouchingWeight(1f); ParkBaseSmgHold(); }"
new_hold = "                    var crouchHold = Mathf.Abs(intent.Move) > 0.1f ? null : CrouchAimHoldState();\n                    if (crouchHold != null) _anim.ForcePlay(crouchHold);\n                    else { _anim.SetCrouch(true); _anim.SetCrouchingWeight(1f); FollowCrouchBase(); }"
if old_hold in s:
    s = s.replace(old_hold, new_hold)

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
print("done")
