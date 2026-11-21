import io

# ---------------- PlayerMelee: 关掉克隆残影，改用 URP 运动模糊 ----------------
g = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerMelee.cs"
s = io.open(g, encoding="utf-8").read()

old = '''            // 残影计时（第三段挥砍的拖影，Spine 克隆版）
            if (_combo >= 3 && _elapsed < afterimageWindow)
            {
                _afterimageTimer -= dt;

                if (_afterimageTimer <= 0f)
                {
                    _afterimageTimer = afterimageInterval;
                    SpawnAfterimage();
                }
            }'''

new = '''            // 第三段挥砍的"残影"：用 URP 运动模糊做拖影，Spine 角色不需要克隆
            if (_combo >= 3)
            {
                var t = Mathf.Clamp01(1f - _elapsed / Mathf.Max(0.01f, afterimageWindow));
                SetComboMotionBlur(t * motionBlurIntensity);
            }
            else
            {
                SetComboMotionBlur(0f);
            }'''
if old in s:
    s = s.replace(old, new, 1)
    print("motion blur tick patched")
else:
    print("afterimage tick block not found")

# 加字段与方法
if "SetComboMotionBlur" not in s.split("private void BeginAttack")[0]:
    anchor = "        private float _afterimageTimer;"
    i = s.index(anchor)
    extra = '''
        [Header("第三段运动模糊（残影）")]
        [Tooltip("第三段挥砍时的最大运动模糊强度")]
        [SerializeField] private float motionBlurIntensity = 0.85f;

        private Volume _mbVolume;
        private UnityEngine.Rendering.Universal.MotionBlur _mb;

        /// <summary>设置第三段的运动模糊（0 = 关），首次调用时创建全局 Volume。</summary>
        private void SetComboMotionBlur(float intensity)
        {
            if (_mb == null && intensity <= 0.001f) return;

            if (_mb == null)
            {
                var go = new GameObject("ThirdHitMotionBlur");
                go.transform.SetParent(transform, false);

                _mbVolume = go.AddComponent<Volume>();
                _mbVolume.isGlobal = true;
                _mbVolume.priority = 100f;
                _mbVolume.weight = 1f;

                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _mb = profile.Add<UnityEngine.Rendering.Universal.MotionBlur>(true);
                _mb.active = true;
                _mb.mode.Override(UnityEngine.Rendering.Universal.MotionBlurMode.CameraAndObjects);
                _mb.quality.Override(UnityEngine.Rendering.Universal.MotionBlurQuality.Medium);
                _mb.intensity.Override(0f);
                _mbVolume.sharedProfile = profile;
            }

            _mb.intensity.Override(Mathf.Clamp(intensity, 0f, 1f));
        }

'''
    s = s[:i] + extra + s[i:]

io.open(g, "w", encoding="utf-8", newline="\r\n").write(s)
print("motion blur added:", "SetComboMotionBlur" in s)

# ---------------- PlayerGun: 蹲瞄准回退为稳定行为 ----------------
p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

old_hold = '''                _anim.SetCrouch(true);
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

new_hold = '''                _anim.SetCrouch(true);
                _anim.SetCrouchingWeight(1f);
                SetAimWeightImmediate(0f);
                FollowCrouchBase();'''

if old_hold in t:
    t = t.replace(old_hold, new_hold, 1)
    print("crouch reverted")
else:
    print("crouch block not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
