import json, io, os, re

SPINE = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\TextAsset\vivi\Heroine4.1.json"
ROOT = r"D:\HE\unity\Git\GITUHUB\vivi\Assets"
APPLY = True  # 先 dry-run

d = json.load(io.open(SPINE, encoding="utf-8"))
a = d.get("animations")
spine_names = set(a.keys()) if isinstance(a, dict) else set(x.get("name") for x in a)

pat = re.compile(r"(?m)^(\s*m_Name:\s*)([^\r\n]+)$")
report = []

for dp, dn, fn in os.walk(ROOT):
    if "Live2D" in dp:
        continue
    for f in fn:
        if not f.endswith(".anim"):
            continue
        p = os.path.join(dp, f)
        txt = io.open(p, encoding="utf-8", errors="ignore").read()
        m = pat.search(txt)
        if not m:
            continue
        name = m.group(2).strip()
        if name in spine_names:
            continue
        # 尝试把第一个下划线换成斜杠
        if "_" in name:
            cand = name.replace("_", "/", 1)
            if cand in spine_names:
                report.append((os.path.relpath(p, ROOT), name, cand))
                if APPLY:
                    txt = pat.sub(lambda mm: mm.group(1) + cand, txt, count=1)
                    io.open(p, "w", encoding="utf-8", newline="\r\n").write(txt)

print("需要改名的 clip 数:", len(report))
for r in report[:20]:
    print("  ", r[0], "|", r[1], "->", r[2])
if not APPLY:
    print("\n（dry-run，没有写入）")

