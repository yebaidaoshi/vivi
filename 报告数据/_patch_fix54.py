import io, re

# ---------------- PlayerEvade: 稳健地找主网格 ----------------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

start = t.index("        /// <summary>把主网格找出来")
end = t.index("        /// <summary>", start + 10)
new_method = '''        /// <summary>找到"身体"那块网格（取顶点最多的 MeshRenderer，Spine 有多个 Submesh 渲染器）。</summary>
        private void ResolveMainMesh()
        {
            // 已经找到有效网格就不再重复找
            if (_mainMeshFilter != null && _mainMeshFilter.sharedMesh != null && _mainMeshRenderer != null)
            {
                return;
            }

            MeshFilter bestFilter = null;
            MeshRenderer bestRenderer = null;
            var bestVerts = -1;

            var roots = new System.Collections.Generic.List<Transform> { transform, transform.root };

            foreach (var r in roots)
            {
                if (r == null) continue;

                foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;

                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    var verts = mf.sharedMesh.vertexCount;
                    if (verts > bestVerts)
                    {
                        bestVerts = verts;
                        bestFilter = mf;
                        bestRenderer = mr;
                    }
                }

                if (bestFilter != null) break;
            }

            if (bestFilter != null)
            {
                _mainMeshFilter = bestFilter;
                _mainMeshRenderer = bestRenderer;
                return;
            }

            // 最后兜底：重试一次 SkeletonRenderer
            var skel = GetComponentInParent<SkeletonRenderer>();
            if (skel == null) skel = GetComponentInChildren<SkeletonRenderer>(true);
            if (skel == null) return;

            var renderer = skel.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = skel.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null) return;

            _mainMeshRenderer = renderer;
            _mainMeshFilter = renderer.GetComponent<MeshFilter>();
        }

'''
t = t[:start] + new_method + t[end:]
io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("ResolveMainMesh rewritten")

# ---------------- PlayerGun: 蹲判定加输入 + 蹲瞄准过渡 ----------------
g = r"D:\HE\unity\GIT\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()

# 1) TickIdle: 蹲判定 = 状态 或 输入
s = s.replace("                _aimCrouch = _crouched;\n\n                if (_aimCrouch)",
              "                _aimCrouch = _crouched || intent.Crouch;\n\n                if (_aimCrouch)", 1)
s = s.replace("            if (wantAds)\n            {\n                _aimCrouch = _crouched;",
              "            if (wantAds)\n            {\n                _aimCrouch = _crouched || intent.Crouch;", 1)

# 2) TickHolding: 蹲变化时，蹲方向也走过渡片段
old = '''                if (wantCrouch)
                {
                    // 瞄准中按蹲：直接切到蹲瞄准状态（不播"蹲→站"片段）
                    _aimCrouch = true;
                    var crouchHold = Mathf.Abs(intent.Move) > 0.1f ? null : CrouchAimHoldState();
                    if (crouchHold != null) _anim.ForcePlay(crouchHold);
                    else { _anim.SetCrouch(true); _anim.SetCrouchingWeight(1f); FollowCrouchBase(); }
                }'''
new = '''                if (wantCrouch)
                {
                    // 瞄准中按蹲：先播 Aim/Aim_SMG_Hold_to_Crouch_Aim 过渡，再停在蹲瞄准
                    BeginCrouchAimTransition(true);
                    TickCrouchAimTransition(intent, wantAds, allowFaceFlip);
                    return;
                }'''
if old in s:
    s = s.replace(old, new, 1)
    print("holding crouch patched")
else:
    print("holding crouch block not found")

# 3) wantCrouch 用输入兜底
s = s.replace("            bool wantCrouch = _crouched;", "            bool wantCrouch = _crouched || intent.Crouch;", 1)

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
print("gun patched")
