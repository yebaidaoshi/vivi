import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
lines = io.open(p, encoding="utf-8").read().split("\n")

start = next((i for i, l in enumerate(lines) if "public bool TryStart(" in l), None)
if start is None:
    print("TryStart not found")
else:
    target = None
    for i in range(start, min(start + 40, len(lines))):
        if "PlayerAnimDriver.States.JumpEvade" in lines[i]:
            target = i
            break

    if target is None:
        print("JumpEvade line not found")
    else:
        insert = [
            "",
            "            // 闪避起手就铺残影（Update 上下文，和第三段攻击一致）",
            "            PlayAfterimageTrail();",
        ]
        lines = lines[:target + 1] + insert + lines[target + 1:]
        io.open(p, "w", encoding="utf-8", newline="\r\n").write("\n".join(lines))
        print("inserted after line", target + 1)
