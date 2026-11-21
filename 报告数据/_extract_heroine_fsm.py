import json, io, os, sys

p = r"D:\HE\unity\Git\GITUHUB\vivi\报告数据\_HeroineFsm.json"
d = json.load(io.open(p, encoding="utf-8"))
fsms = d.get("fsms") if isinstance(d, dict) else d
if fsms is None:
    fsms = d
print("fsms:", len(fsms))

hero = [f for f in fsms if "heroine" in str(f.get("host", "")).lower() or "vivi" in str(f.get("host", "")).lower()]
print("hero fsms:", len(hero))
for f in hero:
    print(" -", f.get("host"), "/", f.get("name"), "states=", len(f.get("states", [])))

out = []
for f in hero:
    out.append("=" * 60)
    out.append("HOST: %s   FSM: %s   start=%s  states=%s" % (f.get("host"), f.get("name"), f.get("startState"), len(f.get("states", []))))
    for g in f.get("globalTransitions", []) or []:
        out.append("  GLOBAL %s -> %s" % (g.get("evt"), g.get("to")))
    for s in f.get("states", []):
        out.append("  --- %s" % s.get("name"))
        out.append("      actions : " + ", ".join(s.get("actions") or []))
        for t in s.get("transitions", []) or []:
            out.append("      on      : %s -> %s" % (t.get("evt"), t.get("to")))
        if s.get("parameters"):
            out.append("      params  : " + " | ".join(str(x) for x in s["parameters"]))

dst = r"D:\HE\unity\Git\GITUHUB\vivi\报告数据\_Heroine_FSM_细节.txt"
io.open(dst, "w", encoding="utf-8", newline="\r\n").write("\n".join(out))
print("written:", os.path.getsize(dst), "bytes")
