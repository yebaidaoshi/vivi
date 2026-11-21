import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
t = io.open(p, encoding="utf-8").read()

marker = "        private void TickActiveAttack(PlayerIntent intent, bool gunBusy)\n        {\n            float dt = _motor.DeltaTime;"
if "残影计时" not in t:
    block = marker + '''
            // 残影计时（第三段挥砍的拖影）
            if (_combo >= 3 && _elapsed < 0.32f)
            {
                _afterimageTimer -= dt;

                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = 0.04f;
                    SpawnAfterimage();
                }
            }'''
    t = t.replace(marker, block, 1)

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("inserted:", "残影计时" in t)
