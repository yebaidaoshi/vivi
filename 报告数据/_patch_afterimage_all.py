import io

# ---------------- PlayerEvade: 全身残影（每个 Submesh 一张）+ 参数 ----------------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

start = t.index("        /// <summary>\n        /// 生成一张残影")
end = t.index("        private void HideSilhouette()")

new_block = '''        [Header("残影（第三段挥砍）")]
        [Tooltip("残影透明度")]
        [SerializeField] private float ghostAlpha = 0.5f;
        [Tooltip("同时存在的残影上限（超过就销毁最旧的）")]
        [SerializeField] private int maxAfterimages = 24;
        [Tooltip("单个残影最多复制几个 Submesh（身体部件）")]
        [SerializeField] private int maxAfterimageParts = 12;

        private readonly System.Collections.Generic.List<GameObject> _afterimages = new System.Collections.Generic.List<GameObject>();

        /// <summary>生成一张全身残影：把当前每个 Submesh 的网格各克隆一份，life 秒后销毁。</summary>
        public void SpawnAfterimage(float life = 0.22f)
        {
            ResolveMainMesh();

            var mat = GhostMaterial();
            if (mat == null || mat.shader == null) return;

            var root = transform.root != null ? transform.root : transform;
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

                var go = new GameObject("Afterimage");
                go.transform.SetParent(mr.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var gmf = go.AddComponent<MeshFilter>();
                gmf.sharedMesh = Instantiate(mf.sharedMesh);

                var gmr = go.AddComponent<MeshRenderer>();
                gmr.sharedMaterial = mat;
                gmr.sortingLayerID = mr.sortingLayerID;
                gmr.sortingOrder = mr.sortingOrder + 1;
                gmr.shadowCastingMode = ShadowCastingMode.Off;
                gmr.receiveShadows = false;

                _afterimages.Add(go);
                Destroy(go, Mathf.Max(0.02f, life));
            }

            // 超过上限就销毁最旧的
            while (_afterimages.Count > maxAfterimages)
            {
                var oldest = _afterimages[0];
                _afterimages.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }
        }

'''
t = t[:start] + new_block + t[end:]

# GhostMaterial 用 ghostAlpha 参数
t = t.replace('var ghostColor = new Color(1f, 1f, 1f, 0.5f);',
              'var ghostColor = new Color(1f, 1f, 1f, Mathf.Clamp01(ghostAlpha));')

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("afterimage rewritten")

# ---------------- PlayerMelee: 残影参数 ----------------
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
s = io.open(g, encoding="utf-8").read()

if "afterimageInterval" not in s:
    anchor = "        [Tooltip(\"挥砍起手带的前冲速度"
    i = s.index(anchor)
    fields = '''        [Header("第三段残影")]
        [Tooltip("两张残影之间的间隔（秒）")]
        [SerializeField] private float afterimageInterval = 0.04f;
        [Tooltip("单张残影存活时间（秒）")]
        [SerializeField] private float afterimageLifetime = 0.24f;
        [Tooltip("起手后持续生成残影多久（秒）")]
        [SerializeField] private float afterimageWindow = 0.32f;

'''
    s = s[:i] + fields + s[i:]

s = s.replace("if (_combo >= 3 && _elapsed < 0.32f)", "if (_combo >= 3 && _elapsed < afterimageWindow)")
s = s.replace("_afterimageTimer = 0.04f;\n                    SpawnAfterimage();",
              "_afterimageTimer = afterimageInterval;\n                    SpawnAfterimage();")
s = s.replace("if (_evade != null) _evade.SpawnAfterimage(0.24f);",
              "if (_evade != null) _evade.SpawnAfterimage(afterimageLifetime);")

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
print("melee params added")

# ---------------- PlayerGun: 蹲瞄准保持（播完不要站起） ----------------
gs = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
u = io.open(gs, encoding="utf-8").read()

old = '''                if (_vibrationTimer > 0f)
                {
                    _vibrationTimer -= _motor.DeltaTime;
                }
                else if (hold != null)
                {
                    _anim.SyncCurrent(hold);
                }'''
new = '''                if (_vibrationTimer > 0f)
                {
                    _vibrationTimer -= _motor.DeltaTime;
                }
                else if (hold != null)
                {
                    // 蹲瞄准保持：状态一旦播完/被切走就立刻重播，避免"站起来"
                    if (_anim.IsPlaying(hold)) _anim.SyncCurrent(hold);
                    else _anim.ForcePlay(hold);
                }'''
if old in u:
    u = u.replace(old, new, 1)
    print("crouch hold patched")
else:
    print("crouch hold block not found")

io.open(gs, "w", encoding="utf-8", newline="\r\n").write(u)
