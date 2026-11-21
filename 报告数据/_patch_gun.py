import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

start_marker = "            bool wantCrouch = _crouched;"
end_marker = "            bool moving = !_aimCrouch && _vibrationTimer <= 0f"

i = t.index(start_marker)
j = t.index(end_marker)

new = '''            bool wantCrouch = _crouched;
            if (wantCrouch != _aimCrouch && _vibrationTimer <= 0f)
            {
                if (wantCrouch)
                {
                    // 瞄准中按蹲：直接切到蹲瞄准状态（不播"蹲→站"片段）
                    _aimCrouch = true;
                    var crouchHold = CrouchAimHoldState();
                    if (crouchHold != null) _anim.ForcePlay(crouchHold);
                    else { _anim.SetCrouch(true); _anim.SetCrouchingWeight(1f); ParkBaseSmgHold(); }
                }
                else
                {
                    BeginCrouchAimTransition(false);
                    TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                    return;
                }
            }

            UpdateAdsAim(intent, allowFaceFlip);

            if (_aimCrouch)
            {
                // 蹲瞄准：保持蹲瞄准状态，绝不能被下面的站立 Aim_SMG_Hold 覆盖
                var hold = CrouchAimHoldState();

                if (hold != null)
                {
                    _anim.SyncCurrent(hold);
                }

                if (intent.ReloadPressed && _ammo < _settings.magazineCapacity)
                {
                    StartReload();
                    return;
                }

                if (intent.Fire && _fireCooldown <= 0f)
                {
                    TryFire(allowRecoil: false);
                }

                return;
            }

            _anim.SetCrouch(_aimCrouch);
'''

t = t[:i] + new + t[j:]
io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("patched TickHolding")
