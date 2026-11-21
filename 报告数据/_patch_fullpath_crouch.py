import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

# 候选名改成完整路径（带 Base Layer 前缀）
old = '''        private static readonly string[] CrouchAimCandidates =
        {
            "Crouch_Aim_SMG_Hold",
            "Crouch/Crouch_Aim_SMG_Hold",
            "Crouch_Aim_SMG",
        };'''
new = '''        private static readonly string[] CrouchAimCandidates =
        {
            "Base Layer/Crouch/Crouch_Aim_SMG_Hold",
            "Base Layer/Crouch_Aim_SMG_Hold",
            "Crouch/Crouch_Aim_SMG_Hold",
            "Crouch_Aim_SMG_Hold",
        };'''
if old in t:
    t = t.replace(old, new, 1)
    print("candidates patched")
else:
    print("candidates block not found")

# CrouchAimHoldState 用 HasState(hash) 判断完整路径
old_probe = '''            foreach (var name in CrouchAimCandidates)
            {
                if (_anim.HasState(name))
                {
                    _crouchAimHold = name;
                    break;
                }
            }'''
new_probe = '''            var animator = GetComponent<Animator>();

            foreach (var name in CrouchAimCandidates)
            {
                if (animator != null && animator.HasState(0, Animator.StringToHash(name)))
                {
                    _crouchAimHold = name;
                    break;
                }
            }'''
if old_probe in t:
    t = t.replace(old_probe, new_probe, 1)
    print("probe patched")
else:
    print("probe block not found")

# TickIdle 蹲下抬枪：改为按完整路径播放
old_idle = '''                if (_aimCrouch)
                {
                    var crouchHold = Mathf.Abs(intent.Move) > 0.1f ? null : CrouchAimHoldState();
                    if (crouchHold != null)
                    {
                        _anim.ForcePlay(crouchHold);
                        _phase = AdsPhase.Holding;
                        return;
                    }
'''
new_idle = '''                if (_aimCrouch)
                {
                    var crouchHold = CrouchAimHoldState();

                    if (crouchHold != null && _anim.PlayByFullPath(crouchHold))
                    {
                        _phase = AdsPhase.Holding;
                        return;
                    }
'''
if old_idle in t:
    t = t.replace(old_idle, new_idle, 1)
    print("idle patched")
else:
    print("idle block not found")

# TickHolding 蹲保持：用完整路径检测 + 重播
old_hold = '''            _anim.SetCrouch(_aimCrouch);
            _anim.SetCrouchingWeight(_aimCrouch ? 1f : 0f);
            UpdateAdsAim(intent, allowFaceFlip);'''
new_hold = '''            _anim.SetCrouch(_aimCrouch);
            _anim.SetCrouchingWeight(_aimCrouch ? 1f : 0f);

            if (_aimCrouch)
            {
                var hold = CrouchAimHoldState();

                if (hold != null && !_anim.IsStateFullPath(hold))
                {
                    _anim.PlayByFullPath(hold);
                }
            }

            UpdateAdsAim(intent, allowFaceFlip);'''
if old_hold in t:
    t = t.replace(old_hold, new_hold, 1)
    print("holding patched")
else:
    print("holding block not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
