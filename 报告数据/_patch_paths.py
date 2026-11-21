import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

old_cands = '''        private static readonly string[] CrouchAimCandidates =
        {
            "Base Layer/Crouch/Crouch_Aim_SMG_Hold",
            "Base Layer/Crouch_Aim_SMG_Hold",
            "Crouch/Crouch_Aim_SMG_Hold",
            "Crouch_Aim_SMG_Hold",
        };'''

new_cands = '''        // 完整路径规则：Base Layer / <子状态机> / <状态名>（状态名本身带前缀，如 Crouch/Crouch_Aim_SMG_Hold）
        private static readonly string[] CrouchAimCandidates =
        {
            "Base Layer/Crouch/Crouch_Aim_SMG_Hold",
            "Base Layer/Crouch/Crouch_Aim_SMG",
            "Base Layer/Crouch_Aim_SMG_Hold",
            "Crouch/Crouch_Aim_SMG_Hold",
        };'''

if old_cands in t:
    t = t.replace(old_cands, new_cands, 1)
    print("candidates updated")
else:
    print("candidates block not found")

# 过渡片段也用完整路径
old_tr = '''            var transition = toCrouch ? "Aim/Aim_SMG_Hold_to_Crouch_Aim" : "Crouch/Crouch_Aim_to_Stand_Aim";

            if (_anim.HasState(transition))
            {
                _anim.ForcePlay(transition);
            }
            else
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }'''

new_tr = '''            var transition = toCrouch
                ? "Base Layer/Aim/Aim_SMG_Hold_to_Crouch_Aim"
                : "Base Layer/Crouch/Crouch_Aim_to_Stand_Aim";

            if (!_anim.PlayByFullPath(transition))
            {
                // 兜底：老路径 / 默认片段
                if (!_anim.PlayByFullPath("Aim/Aim_SMG_Hold_to_Crouch_Aim"))
                {
                    _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
                }
            }'''

if old_tr in t:
    t = t.replace(old_tr, new_tr, 1)
    print("transition updated")
else:
    print("transition block not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
