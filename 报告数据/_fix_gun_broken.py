import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
lines = io.open(p, encoding="utf-8").read().split("\n")

out = []
i = 0
fixed = False
while i < len(lines):
    l = lines[i]
    if (l.strip() == "if (_aimCrouch)" and i + 2 < len(lines)
            and "var hold = CrouchAimHoldState();" in lines[i + 1]
            and lines[i + 2].strip().startswith("&&")):
        # 丢掉 if / { / var hold 三行，保留 && 行并补上 moving 声明
        condition = lines[i + 2].strip()
        out.append("            bool moving = !_aimCrouch && _vibrationTimer <= 0f")
        out.append("                " + condition)
        i += 3
        fixed = True
        continue

    out.append(l)
    i += 1

io.open(p, "w", encoding="utf-8", newline="\r\n").write("\n".join(out))
print("fixed:", fixed)
