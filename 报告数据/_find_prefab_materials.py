import os, re, io, json

ROOT = r"D:\HE\unity\Git\GITUHUB\vivi"
ASSETS = os.path.join(ROOT, "Assets")
GO_DIR = os.path.join(ASSETS, "GameObject")

# guid -> .mat / .shader path
guid2path = {}
for dp, dn, fn in os.walk(ASSETS):
    for f in fn:
        if f.endswith(".meta"):
            p = os.path.join(dp, f)
            try:
                head = io.open(p, encoding="utf-8", errors="ignore").read(200)
            except Exception:
                continue
            m = re.search(r"guid:\s*([0-9a-f]{32})", head)
            if m:
                guid2path[m.group(1)] = os.path.join(dp, f[:-5])

# 只保留材质/着色器
def is_mat(p):
    lp = p.lower()
    return lp.endswith(".mat") or lp.endswith(".shader")

pat_block = re.compile(r"m_Materials:\s*\n((?:\s*-\s*\{fileID:\s*\d+,\s*guid:\s*[0-9a-f]{32},\s*type:\s*2\}\s*\n)+)")
pat_single = re.compile(r"m_Material:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*2\}")
pat_guid = re.compile(r"guid:\s*([0-9a-f]{32})")

rows = []
for dp, dn, fn in os.walk(GO_DIR):
    for f in fn:
        if not f.endswith(".prefab"):
            continue
        p = os.path.join(dp, f)
        txt = io.open(p, encoding="utf-8", errors="ignore").read()
        guids = set()
        for blk in pat_block.findall(txt):
            for g in pat_guid.findall(blk):
                guids.add(g)
        for g in pat_single.findall(txt):
            guids.add(g)
        mats = sorted({guid2path[g] for g in guids if g in guid2path and is_mat(guid2path[g])})
        rows.append((os.path.relpath(p, ROOT).replace("\\", "/"), mats))

rows.sort()
out = ["# Assets/GameObject 下所有 prefab 用到的材质", "", "由 `报告数据\\_find_prefab_materials.py` 生成；只列 .mat/.shader（贴图/音频不算）。", ""]
missing = 0
for path, mats in rows:
    out.append("## " + os.path.basename(path))
    out.append("`" + path + "`")
    if mats:
        for m in mats:
            out.append("- " + m.replace("\\", "/"))
    else:
        out.append("- （无材质引用：可能只用 Spine 的 SkeletonGraphic / 运行时生成）")
        missing += 1
    out.append("")

dst = os.path.join(ROOT, "报告数据", "_GameObject_材质清单.md")
io.open(dst, "w", encoding="utf-8", newline="\r\n").write("\n".join(out))
print("prefabs:", len(rows), "no-material:", missing)
print("written:", os.path.getsize(dst))
for path, mats in rows[:15]:
    print(os.path.basename(path), "->", len(mats))
