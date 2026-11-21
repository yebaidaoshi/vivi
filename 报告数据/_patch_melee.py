import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
t = io.open(p, encoding="utf-8").read()

# 1) 字段
if "_afterimageTimer" not in t:
    t = t.replace("        private PlayerEvade _evade;",
                  "        private PlayerEvade _evade;\n        private float _afterimageTimer;", 1)
    if "_afterimageTimer" not in t:
        t = t.replace("        private int _combo;",
                      "        private int _combo;\n        private float _afterimageTimer;", 1)

# 2) 残影方法（放在 FlashThirdHitSilhouette 后面）
anchor = "        /// <summary>第三段挥砍的剪影"
if "SpawnAfterimage()" not in t and anchor in t:
    idx = t.index(anchor)
    method = '''        /// <summary>第三段挥砍：连续生成残影（原版是拖影，不是闪一下）。</summary>
        private void SpawnAfterimage()
        {
            if (_evade == null && _motor != null) _evade = _motor.GetComponent<PlayerEvade>();
            if (_evade == null) _evade = Object.FindObjectOfType<PlayerEvade>();
            if (_evade != null) _evade.SpawnAfterimage(0.24f);
        }

'''
    t = t[:idx] + method + t[idx:]

# 3) 在 TickActiveAttack 开头每 0.04s 生成一张残影（前 0.32s）
marker = "        private void TickActiveAttack(PlayerIntent intent, bool canCancel)"
if "残影计时" not in t and marker in t:
    i = t.index(marker)
    brace = t.index("{", i) + 1
    block = '''
            // 残影计时（第三段挥砍的拖影）
            if (_combo >= 3 && _elapsed < 0.32f)
            {
                _afterimageTimer -= _motor != null ? _motor.DeltaTime : Time.deltaTime;

                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = 0.04f;
                    SpawnAfterimage();
                }
            }
'''
    t = t[:brace] + block + t[brace:]

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("ok", "_afterimageTimer" in t, "SpawnAfterimage" in t, "残影计时" in t)
