import io

g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

old = '''            if (_aimCrouch)
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
            }'''

new = '''            if (_aimCrouch)
            {
                // 蹲瞄准：保持蹲瞄准状态，绝不能被下面的站立 Aim_SMG_Hold 覆盖
                var hold = CrouchAimHoldState();

                if (_vibrationTimer > 0f)
                {
                    _vibrationTimer -= _motor.DeltaTime;
                }
                else if (hold != null)
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

                    // 蹲射的后坐动画
                    if (_anim.HasState(CrouchAimFireState))
                    {
                        _anim.ForcePlay(CrouchAimFireState);
                        _vibrationTimer = 0.12f;
                    }
                }

                return;
            }'''

if old in s:
    s = s.replace(old, new, 1)
    print("crouch fire patched")
else:
    print("crouch fire block not found")

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
