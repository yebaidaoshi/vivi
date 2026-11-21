import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

start = t.index("        /// <summary>生成一张全身残影")
end = t.index("        private void HideSilhouette()")

new = '''        /// <summary>生成全身残影：按"离玩家最近的一批 MeshRenderer"来复制，避免找不到身体。</summary>
        public void SpawnAfterimage(float life = 0.22f)
        {
            ResolveMainMesh();

            var mat = GhostMaterial();
            if (mat == null || mat.shader == null) return;

            var center = transform.position;
            var picked = 0;

            // 收集离玩家很近的 MeshRenderer（含其他根对象上的身体部件）
            var list = new System.Collections.Generic.List<MeshFilter>();

            foreach (var mr in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (mr == null || !mr.enabled) continue;
                if (mr.gameObject.name.StartsWith("Afterimage")) continue;
                if (mr.transform.parent != null && mr.transform.parent.name.StartsWith("Afterimage")) continue;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var d = Vector3.Distance(mr.bounds.center, center);
                if (d > 3f) continue;

                list.Add(mf);
            }

            // 顶点多的优先（身体 > 武器）
            list.Sort((a, b) => b.sharedMesh.vertexCount.CompareTo(a.sharedMesh.vertexCount));

            System.Text.StringBuilder diag = null;
            if (!_ghostDiagDone)
            {
                _ghostDiagDone = true;
                diag = new System.Text.StringBuilder("[Ghost] 可用网格: ");
            }

            foreach (var mf in list)
            {
                if (picked >= maxAfterimageParts) break;
                picked++;

                CreateGhost(mf, mat, life);

                if (diag != null)
                {
                    diag.Append(mf.name).Append("(").Append(mf.sharedMesh.vertexCount).Append(") ");
                }
            }

            if (diag != null) Debug.Log(diag.ToString());

            Trim();
        }

        private bool _ghostDiagDone;

'''
t = t[:start] + new + t[end:]

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("scene-wide ghost done")

# 蹲瞄准：去掉重播（避免闪）
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
s = io.open(g, encoding="utf-8").read()
old = '''                    if (_anim.IsPlaying(hold))
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
new = '''                    if (_anim.IsPlaying(hold))
                    {
                        _anim.SyncCurrent(hold);
                    }'''
if old in s:
    s = s.replace(old, new, 1)
    print("crouch replay removed")
else:
    print("crouch replay block not found")
io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
