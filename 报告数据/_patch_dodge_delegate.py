import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
lines = io.open(p, encoding="utf-8").read().split("\n")

start = None
end = None
for i, l in enumerate(lines):
    if "public void SpawnDodgeAfterimages()" in l:
        start = i - 1  # 连同上一行注释一起换掉
    if start is not None and "private System.Collections.IEnumerator DodgeTrailRoutine()" in l:
        # 找到该方法体的结束（配对大括号）
        depth = 0
        j = i + 1
        while j < len(lines):
            depth += lines[j].count("{") - lines[j].count("}")
            if depth == 0 and "}" in lines[j]:
                end = j
                break
            j += 1
        break

if start is not None and end is not None:
    new = [
        "        /// <summary>完美闪避的残影：和第三段攻击走完全同一条路径（FlashSilhouette → PlayAfterimageTrail）。</summary>",
        "        public void SpawnDodgeAfterimages()",
        "        {",
        "            FlashSilhouette(0.28f);",
        "        }",
    ]
    lines = lines[:start] + new + lines[end + 1:]
    io.open(p, "w", encoding="utf-8", newline="\r\n").write("\n".join(lines))
    print("replaced lines", start + 1, "-", end + 1)
else:
    print("not found", start, end)
