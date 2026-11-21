import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

# 在 SpawnAfterimage 开头插入：优先用 Spine 克隆做残影
old_head = '''        /// <summary>生成全身残影：按"离玩家最近的一批 MeshRenderer"来复制，避免找不到身体。</summary>
        public void SpawnAfterimage(float life = 0.22f)
        {
            ResolveMainMesh();'''

new_head = '''        /// <summary>生成全身残影：Spine 角色走"克隆整个可视对象 + 定格姿势"，不是复制网格。</summary>
        public void SpawnAfterimage(float life = 0.22f)
        {
            if (TrySpawnSpineGhost(life))
            {
                Trim();
                return;
            }

            ResolveMainMesh();'''
if old_head in t:
    t = t.replace(old_head, new_head, 1)
    print("head patched")
else:
    print("head not found")

# 追加 Spine 克隆残影实现
if "TrySpawnSpineGhost" not in t.split("public void SpawnAfterimage")[0]:
    anchor = "        /// <summary>生成全身残影"
    i = t.index(anchor)
    method = '''        private SkeletonMecanim _spineVisual;

        /// <summary>Spine 全身残影：克隆可视对象，把 Animator 定格在当前帧，再换成纯色材质。</summary>
        private bool TrySpawnSpineGhost(float life)
        {
            if (_spineVisual == null)
            {
                var root = transform.root != null ? transform.root : transform;
                _spineVisual = root.GetComponentInChildren<SkeletonMecanim>(true);
            }

            if (_spineVisual == null) return false;

            var source = _spineVisual.gameObject;
            var mat = GhostMaterial();
            if (mat == null) return false;

            var clone = Instantiate(source, source.transform.parent);
            clone.name = "Afterimage";

            var cloneAnim = clone.GetComponentInChildren<Animator>(true);
            var srcAnim = source.GetComponentInChildren<Animator>(true);

            if (cloneAnim != null && srcAnim != null)
            {
                // 把克隆体的动画定格在原体的当前帧
                var info = srcAnim.GetCurrentAnimatorStateInfo(0);
                cloneAnim.Play(info.fullStateHash, 0, info.normalizedTime);
                cloneAnim.speed = 0f;
                cloneAnim.Update(0f);
            }

            foreach (var r in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = mat;
            }

            _afterimages.Add(clone);
            Destroy(clone, Mathf.Max(0.02f, life));
            return true;
        }

'''
    t = t[:i] + method + t[i:]

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("spine ghost added")

# 打开残影生成（PlayerMelee）
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
s = io.open(g, encoding="utf-8").read()
old_block = '''            // 残影：暂时关闭 —— 角色是 Spine(SkeletonMecanim)，运行时拿不到身体网格（Submesh 顶点为 0）。
            // 要做得先克隆整个 Spine 可视对象，见报告。'''
new_block = '''            // 残影计时（第三段挥砍的拖影，Spine 克隆版）
            if (_combo >= 3 && _elapsed < afterimageWindow)
            {
                _afterimageTimer -= dt;

                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = afterimageInterval;
                    SpawnAfterimage();
                }
            }'''
if old_block in s:
    s = s.replace(old_block, new_block, 1)
    print("afterimage re-enabled")
else:
    print("afterimage block not found")

# 残影间隔放宽到 0.06（克隆体较重）
s = s.replace("private float afterimageInterval = 0.04f;", "private float afterimageInterval = 0.06f;")
io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)

# 蹲瞄准：Base 层直接播抬起片段再保持
gs = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
u = io.open(gs, encoding="utf-8").read()
old = '''                _anim.SetCrouch(true);
                _anim.SetCrouchingWeight(1f);
                SetAimWeightImmediate(0f);
                FollowCrouchBase();'''
new = '''                _anim.SetCrouch(true);
                _anim.SetCrouchingWeight(1f);
                SetAimWeightImmediate(0f);

                // 全部在 Base 层：播"蹲瞄准抬起"再保持
                var hold = CrouchAimHoldState();

                if (hold != null)
                {
                    if (_anim.IsPlaying(hold)) _anim.SyncCurrent(hold);
                    else _anim.ForcePlay(hold);
                }
                else
                {
                    FollowCrouchBase();
                }'''
if old in u:
    u = u.replace(old, new, 1)
    print("crouch base patched")
else:
    print("crouch base block not found")
io.open(gs, "w", encoding="utf-8", newline="\r\n").write(u)
