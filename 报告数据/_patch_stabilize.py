import io

# 1) PlayerMelee: 暂停残影生成（当前拿不到身体网格，只会克隆 0 顶点的空网格）
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
s = io.open(g, encoding="utf-8").read()

old = '''            // 残影计时（第三段挥砍的拖影）
            if (_combo >= 3 && _elapsed < afterimageWindow)
            {
                _afterimageTimer -= dt;

                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = afterimageInterval;
                    SpawnAfterimage();
                }
            }'''
new = '''            // 残影：暂时关闭 —— 角色是 Spine(SkeletonMecanim)，运行时拿不到身体网格（Submesh 顶点为 0）。
            // 要做得先克隆整个 Spine 可视对象，见报告。'''
if old in s:
    s = s.replace(old, new, 1)
    print("afterimage spawn disabled")
else:
    print("afterimage block not found")

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)

# 2) PlayerGun: 蹲瞄准不再强制 Animator 状态（回退到稳定行为：允许开火、保持蹲姿基座）
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

old_hold = '''            if (_aimCrouch)
            {
                // 蹲瞄准：保持蹲瞄准状态，绝不能被下面的站立 Aim_SMG_Hold 覆盖
                var hold = CrouchAimHoldState();

                if (_aimCrouch && hold != null)
                {
                    if (_anim.IsPlaying(hold))
                    {
                        _anim.SyncCurrent(hold);
                    }
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

new_hold = '''            if (_aimCrouch)
            {
                // 蹲瞄准：保持蹲姿基座 + 瞄准层，允许开火；不再强切 Animator 状态（会闪/丢姿势）
                _anim.SetCrouch(true);
                _anim.SetCrouchingWeight(1f);
                SetAimWeightImmediate(0f);
                FollowCrouchBase();

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

# 实际文件可能因前几次替换导致块内容不同，做一次宽松匹配
import re
pattern = re.compile(r"            if \(_aimCrouch\)\n            \{.*?\n                return;\n            \}", re.S)
m = pattern.search(t)
if m:
    t = t[:m.start()] + new_hold + t[m.end():]
    print("crouch aim block replaced")
else:
    print("crouch aim block not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
