import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerEvade.cs"
t = io.open(p, encoding="utf-8").read()

old = '''        /// <summary>完美闪避的残影。</summary>
        public void SpawnDodgeAfterimages()
        {
            // 完美闪避也走拖尾（不是单张）
            PlayAfterimageTrail();
        }'''

new = '''        /// <summary>完美闪避的残影：用协程直接铺一串，不依赖 LateUpdate 的拖尾计时。</summary>
        public void SpawnDodgeAfterimages()
        {
            StopCoroutine(nameof(DodgeTrailRoutine));
            StartCoroutine(DodgeTrailRoutine());
        }

        private System.Collections.IEnumerator DodgeTrailRoutine()
        {
            CaptureAfterimage(afterimageLifetime);

            var count = Mathf.Max(0, afterimageCount - 1);

            for (var i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(Mathf.Max(0.005f, afterimageInterval));
                CaptureAfterimage(afterimageLifetime);
            }
        }'''

if old in t:
    t = t.replace(old, new, 1)
    print("dodge coroutine added")
else:
    print("dodge block not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
