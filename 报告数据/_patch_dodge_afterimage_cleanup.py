import io, os

repo = r"D:\HE\unity\Git\GITUHUB\vivi"

# 1) 完美闪避 → 残影拖尾
p = os.path.join(repo, r"Assets\Scripts\Player\PlayerEvade.cs")
t = io.open(p, encoding="utf-8").read()

old = '''        public void SpawnDodgeAfterimages()
        {
            SpawnAfterimage(0.3f);
        }'''
new = '''        public void SpawnDodgeAfterimages()
        {
            // 完美闪避也走拖尾（不是单张）
            PlayAfterimageTrail();
        }'''
if old in t:
    t = t.replace(old, new, 1)
    print("dodge trail patched")
else:
    print("dodge block not found")

# 2) 删掉 "没找到网格" 的诊断打印块
lines = t.split("\n")
out = []
skip = False
for l in lines:
    if (not skip) and 'var sb = new System.Text.StringBuilder("[Afterimage]' in l:
        skip = True
        continue
    if skip:
        if "Debug.LogWarning(sb.ToString(), this);" in l:
            skip = False
        continue
    out.append(l)
t = "\n".join(out)
io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("evade diag removed")

# 3) PlayerAnimDriver：去掉 SMG 路由日志
p2 = os.path.join(repo, r"Assets\Scripts\Player\PlayerAnimDriver.cs")
d = io.open(p2, encoding="utf-8").read()

old_route = '''            else if (logSmgRouting)
            {
                Debug.Log($"[PlayerAnimDriver] SMG 路由：{leaf} → {(hash == full ? _animator.GetLayerName(0) + "." + SmgMachine + "." + leaf : SmgPrefix + leaf)}", this);
            }

'''
if old_route in d:
    d = d.replace(old_route, "", 1)
    print("routing log removed")
else:
    print("routing log not found")

io.open(p2, "w", encoding="utf-8", newline="\r\n").write(d)
