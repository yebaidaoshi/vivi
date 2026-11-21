import io

# ---------------- PlayerEvade: 全身残影（优先 SkeletonRenderer 合并网格） ----------------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

start = t.index("        /// <summary>生成一张全身残影")
end = t.index("        private void HideSilhouette()")

new = '''        /// <summary>生成一张全身残影：优先用 Spine 的合并网格（整身一张），否则逐个 Submesh 复制。</summary>
        public void SpawnAfterimage(float life = 0.22f)
        {
            ResolveMainMesh();

            var mat = GhostMaterial();
            if (mat == null || mat.shader == null) return;

            var root = transform.root != null ? transform.root : transform;

            // 1) 优先：Spine 的 SkeletonRenderer（合并网格 = 整个角色）
            var skel = root.GetComponentInChildren<SkeletonRenderer>(true);
            if (skel != null)
            {
                var skelMeshFilter = skel.GetComponent<MeshFilter>();
                if (skelMeshFilter == null) skelMeshFilter = skel.GetComponentInChildren<MeshFilter>(true);

                if (skelMeshFilter != null && skelMeshFilter.sharedMesh != null)
                {
                    CreateGhost(skelMeshFilter, mat, life);
                    Trim();
                    return;
                }
            }

            // 2) 兜底：逐个 MeshRenderer（多个 Submesh）
            var parts = 0;

            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null) continue;
                if (mr.gameObject.name.StartsWith("Afterimage")) continue;
                if (mr.transform.parent != null && mr.transform.parent.name.StartsWith("Afterimage")) continue;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                if (parts >= maxAfterimageParts) break;
                parts++;

                CreateGhost(mf, mat, life);
            }

            Trim();
        }

        private void CreateGhost(MeshFilter source, Material mat, float life)
        {
            var go = new GameObject("Afterimage");
            go.transform.SetParent(source.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var gmf = go.AddComponent<MeshFilter>();
            gmf.sharedMesh = Instantiate(source.sharedMesh);

            var srcRenderer = source.GetComponent<MeshRenderer>();
            var gmr = go.AddComponent<MeshRenderer>();
            gmr.sharedMaterial = mat;

            if (srcRenderer != null)
            {
                gmr.sortingLayerID = srcRenderer.sortingLayerID;
                gmr.sortingOrder = srcRenderer.sortingOrder + 1;
            }

            gmr.shadowCastingMode = ShadowCastingMode.Off;
            gmr.receiveShadows = false;

            _afterimages.Add(go);
            Destroy(go, Mathf.Max(0.02f, life));
        }

        private void Trim()
        {
            while (_afterimages.Count > maxAfterimages)
            {
                var oldest = _afterimages[0];
                _afterimages.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }
        }

'''
t = t[:start] + new + t[end:]

# 完美闪避也放残影
if "SpawnDodgeAfterimages" not in t:
    anchor = "        public void SpawnAfterimage(float life = 0.22f)"
    i = t.index(anchor)
    method = '''        /// <summary>完美闪避的残影（原版闪避也有残影拖尾）。</summary>
        public void SpawnDodgeAfterimages()
        {
            SpawnAfterimage(0.3f);
        }

'''
    t = t[:i] + method + t[i:]

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("afterimage skeleton-first done")

# ---------------- PlayerEvade: NotifyPerfectDodge 里触发残影 ----------------
t = io.open(p, encoding="utf-8").read()
if "NotifyPerfectDodge" in t and "SpawnDodgeAfterimages();" not in t:
    i = t.index("        public void NotifyPerfectDodge(GameObject attacker)")
    brace = t.index("{", i) + 1
    t = t[:brace] + "\n            SpawnDodgeAfterimages();\n" + t[brace:]
    io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
    print("perfect dodge afterimage done")

# ---------------- PlayerGun: 蹲瞄准重播加冷却，避免闪一下 ----------------
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

old = '''                    if (_anim.IsPlaying(hold)) _anim.SyncCurrent(hold);
                    else _anim.ForcePlay(hold);'''
new = '''                    if (_anim.IsPlaying(hold))
                    {
                        _anim.SyncCurrent(hold);
                        _crouchReplayWait = 0f;
                    }
                    else
                    {
                        // 刚播完时先等一下再重播，避免每帧重播导致"闪一下"
                        _crouchReplayWait += _motor.DeltaTime;

                        if (_crouchReplayWait >= 0.12f)
                        {
                            _anim.ForcePlay(hold);
                            _crouchReplayWait = 0f;
                        }
                    }'''
if old in s:
    s = s.replace(old, new, 1)
    print("crouch replay cooldown")
else:
    print("crouch replay block not found")

if "_crouchReplayWait" not in s.split("private void TickHolding")[0]:
    s = s.replace("        private bool _crouchAimProbed;", "        private bool _crouchAimProbed;\n        private float _crouchReplayWait;", 1)

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
print("gun done")
