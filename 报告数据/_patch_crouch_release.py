import io

g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

# 1) 蹲着松瞄准：播 Crouch/Crouch_Aim_SMG_Release（基座层），否则原来的站立 Release
old_release = '''            _anim.SetCrouch(_aimCrouch);
            _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);'''
new_release = '''            _anim.SetCrouch(_aimCrouch);

            if (_aimCrouch && _anim.HasState(CrouchAimReleaseState))
            {
                _anim.ForcePlay(CrouchAimReleaseState);
            }
            else
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.AimSmgRelease);
            }'''
if old_release in s:
    s = s.replace(old_release, new_release, 1)
    print("release patched")
else:
    print("release block not found")

# 2) 蹲瞄准相关常量
if "CrouchAimReleaseState" not in s.split("private void EnterRelease")[0]:
    anchor = "        private static readonly string[] CrouchAimCandidates ="
    i = s.index(anchor)
    consts = '''        private const string CrouchAimReleaseState = "Crouch/Crouch_Aim_SMG_Release";
        private const string CrouchAimFireState = "Crouch/Crouch_Aim_SMG_vibration";

'''
    s = s[:i] + consts + s[i:]
    print("consts added")

# 3) 瞄准→蹲 用 Aim/Aim_SMG_Hold_to_Crouch_Aim 过渡，蹲→站 用 Crouch/Crouch_Aim_to_Stand_Aim
old_begin = '''            _crouchAimBlend = toCrouch ? 0f : 1f;
            _anim.SetCrouchingWeight(_crouchAimBlend);
            _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);'''
new_begin = '''            _crouchAimBlend = toCrouch ? 0f : 1f;
            _anim.SetCrouchingWeight(_crouchAimBlend);

            // 两种过渡片段：站→蹲 / 蹲→站
            var transition = toCrouch ? "Aim/Aim_SMG_Hold_to_Crouch_Aim" : "Crouch/Crouch_Aim_to_Stand_Aim";

            if (_anim.HasState(transition))
            {
                _anim.ForcePlay(transition);
            }
            else
            {
                _anim.ForcePlayAim(PlayerAnimDriver.States.CrouchAimToStandAim);
            }'''
if old_begin in s:
    s = s.replace(old_begin, new_begin, 1)
    print("transition patched")
else:
    print("transition block not found")

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
