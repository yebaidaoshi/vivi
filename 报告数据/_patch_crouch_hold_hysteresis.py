import io

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Scripts\Player\PlayerGun.cs"
lines = io.open(p, encoding="utf-8").read().split("\n")

# 找到 "_anim.SetCrouchingWeight(_aimCrouch ? 1f : 0f);" 这一行，在其后插入保持逻辑
out = []
inserted = False
for i, l in enumerate(lines):
    out.append(l)

    if (not inserted) and "_anim.SetCrouchingWeight(_aimCrouch ? 1f : 0f);" in l:
        block = [
            "",
            "            // 蹲瞄准保持：状态被 Mecanim 切走后（要 0.15s 连续不在才重播）再拉回来，避免来回闪",
            "            if (_aimCrouch)",
            "            {",
            "                var crouchHold = CrouchAimHoldState();",
            "",
            "                if (crouchHold != null)",
            "                {",
            "                    if (_anim.IsStateFullPath(crouchHold))",
            "                    {",
            "                        _crouchExitTimer = 0f;",
            "                    }",
            "                    else",
            "                    {",
            "                        _crouchExitTimer += _motor.DeltaTime;",
            "",
            "                        if (_crouchExitTimer >= 0.15f)",
            "                        {",
            "                            _anim.PlayByFullPath(crouchHold);",
            "                            _crouchExitTimer = 0f;",
            "                        }",
            "                    }",
            "                }",
            "            }",
        ]
        out.extend(block)
        inserted = True

text = "\n".join(out)

# 字段
if "_crouchExitTimer" not in text.split("private void TickHolding")[0]:
    text = text.replace("        private float _crouchReplayWait;",
                        "        private float _crouchReplayWait;\n        private float _crouchExitTimer;", 1)
    if "_crouchExitTimer" not in text.split("private void TickHolding")[0]:
        text = text.replace("        private bool _crouchAimProbed;",
                            "        private bool _crouchAimProbed;\n        private float _crouchExitTimer;", 1)

io.open(p, "w", encoding="utf-8", newline="\r\n").write(text)
print("inserted:", inserted, "field:", "_crouchExitTimer" in text)
