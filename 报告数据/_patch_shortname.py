import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
t = io.open(p, encoding="utf-8").read()

old = '''        // 完整路径规则：Base Layer / <子状态机> / <状态名>（状态名本身带前缀，如 Crouch/Crouch_Aim_SMG_Hold）
        private static readonly string[] CrouchAimCandidates =
        {
            "Base Layer/Crouch/Crouch_Aim_SMG_Hold",
            "Base Layer/Crouch/Crouch_Aim_SMG",
            "Base Layer/Crouch_Aim_SMG_Hold",
            "Crouch/Crouch_Aim_SMG_Hold",
        };'''

new = '''        // 优先用"短状态名"（Animator.Play 支持唯一状态名），再退回子状态机路径
        private static readonly string[] CrouchAimCandidates =
        {
            "Crouch_Aim_SMG_Hold",
            "Crouch_Aim_SMG",
            "Crouch/Crouch_Aim_SMG_Hold",
            "Base Layer/Crouch/Crouch_Aim_SMG_Hold",
        };'''

if old in t:
    t = t.replace(old, new, 1)
    print("candidates reordered")
else:
    print("candidates not found")

# 诊断日志加上 Animator 层名
old_log = '''            Debug.Log($"[CrouchAim] 蹲瞄准状态 = {(_crouchAimHold == null ? "(没找到，Animator 里缺 Crouch_Aim_SMG_Hold)" : _crouchAimHold)}");'''
new_log = '''            var layerNames = "";

            if (animator != null)
            {
                for (var i = 0; i < animator.layerCount; i++)
                {
                    layerNames += animator.GetLayerName(i) + "|";
                }
            }

            Debug.Log($"[CrouchAim] 蹲瞄准状态 = {(_crouchAimHold == null ? "(没找到)" : _crouchAimHold)}  层=[{layerNames}]  控制器={(_animController != null ? _animController.name : "?")}");'''
if old_log in t:
    t = t.replace(old_log, new_log, 1)
    print("log patched")
else:
    print("log not found")

io.open(p, "w", encoding="utf-8", newline="\r\n").write(t)
print("done")
