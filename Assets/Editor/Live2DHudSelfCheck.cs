using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Rendering;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 游戏内实时 Live2D 的配置自检。
/// 菜单：Tools/Vivi/Live2D/自检实时 HUD 配置
/// 命令行：Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod Live2DHudSelfCheck.Run -logFile &lt;log&gt;
///
/// 查的是最容易出问题的那条链：
///   配置里的动作片段 → AnimationClip 的 InstanceId 事件 → CubismFadeMotionList 里对应的淡入淡出数据。
/// 对不上就会"播不出来 / 播一半停"。顺带实例化一次 prefab，确认模型能在运行时起来、顺便量出立绘尺寸。
/// </summary>
public static class Live2DHudSelfCheck
{
    private const string ConfigPath = "Live2D/Live2DHudConfig";

    [MenuItem("Tools/Vivi/Live2D/自检实时 HUD 配置")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var errors = 0;

        sb.AppendLine("===== [实时 Live2D 自检] =====");

        var config = Resources.Load<Live2DHudConfig>(ConfigPath);

        if (config == null)
        {
            Debug.LogError($"✗ 找不到 Resources/{ConfigPath}.asset");
            return;
        }

        sb.AppendLine($"✓ 配置资源：{ConfigPath}");

        var prefab = SafePrefab(config);

        if (prefab == null)
        {
            sb.AppendLine("✗ modelPrefab 没填 / 引用丢失（Inspector 里重新拖一次 Vivi.prefab）");
            Debug.LogError(sb.ToString());
            return;
        }

        sb.AppendLine($"✓ 模型 prefab：{prefab.name}");
        sb.AppendLine($"{Mark(prefab.GetComponent<CubismModel>() != null, ref errors)} prefab 有 CubismModel");
        sb.AppendLine($"{Mark(prefab.GetComponent<CubismMotionController>() != null, ref errors)} prefab 有 CubismMotionController");
        sb.AppendLine($"{Mark(prefab.GetComponent<CubismRenderController>() != null, ref errors)} prefab 有 CubismRenderController");
        sb.AppendLine($"{Mark(prefab.GetComponent<Live2DActor>() != null, ref errors)} prefab 有 Live2DActor");

        var fade = prefab.GetComponent<CubismFadeController>();
        sb.AppendLine($"{Mark(fade != null, ref errors)} prefab 有 CubismFadeController");

        var fadeList = fade != null ? fade.CubismFadeMotionList : null;

        if (fadeList == null || fadeList.MotionInstanceIds == null || fadeList.CubismFadeMotionObjects == null)
        {
            sb.AppendLine("✗ CubismFadeController 上没挂 CubismFadeMotionList，任何动作都播不了");
            Debug.LogError(sb.ToString());
            return;
        }

        sb.AppendLine($"✓ FadeMotionList：{fadeList.MotionInstanceIds.Length} 个 InstanceId / {fadeList.CubismFadeMotionObjects.Length} 个淡入淡出数据");

        // ---------- 逐个片段检查 ----------
        var clips = config.motionClips ?? new AnimationClip[0];
        sb.AppendLine($"\n配置里登记了 {clips.Length} 个动作片段：");

        var clipNames = new HashSet<string>();
        var okClips = 0;

        foreach (var clip in clips)
        {
            if (clip == null)
            {
                sb.AppendLine("✗ 有一个空引用（Inspector 里丢掉 / 删掉了）");
                errors++;
                continue;
            }

            clipNames.Add(clip.name);

            var events = clip.events.Where(e => e.functionName == "InstanceId").ToArray();
            var index = -1;

            foreach (var e in events)
            {
                index = System.Array.IndexOf(fadeList.MotionInstanceIds, e.intParameter);

                if (index >= 0) break;
            }

            if (index < 0)
            {
                sb.AppendLine($"✗ {clip.name}：没有登记进 FadeMotionList，播放会被 SDK 拒绝");
                errors++;
                continue;
            }

            if (index >= fadeList.CubismFadeMotionObjects.Length || fadeList.CubismFadeMotionObjects[index] == null)
            {
                sb.AppendLine($"✗ {clip.name}：登记的淡入淡出数据是空引用");
                errors++;
                continue;
            }

            var data = fadeList.CubismFadeMotionObjects[index];
            var fadeName = Path.GetFileNameWithoutExtension(data.MotionName ?? "").Replace(".motion3", "");
            var lengthOk = Mathf.Abs(data.MotionLength - clip.length) <= 0.05f;

            if (!lengthOk)
            {
                sb.AppendLine($"✗ {clip.name}：淡入淡出数据长度 {data.MotionLength:0.00}s ≠ 片段长度 {clip.length:0.00}s（对应到了 {fadeName}）");
                errors++;
                continue;
            }

            okClips++;
        }

        sb.AppendLine($"→ 可用 {okClips} / {clips.Length} 个");

        // ---------- 配置里引用的动作名是否都存在 ----------
        var referenced = new List<KeyValuePair<string, string>>
        {
            Pair("血量满", config.hp100Motion),
            Pair("血量 75%", config.hp75Motion),
            Pair("血量 50%", config.hp50Motion),
            Pair("血量 25%", config.hp25Motion),
            Pair("血量 1%", config.hp01Motion),
            Pair("血量 0", config.hp00Motion),
            Pair("受击 1", config.damageMotion1),
            Pair("受击 2", config.damageMotion2),
            Pair("死亡", config.deathMotion),
            Pair("挥砍", config.attackMotion),
            Pair("闪避", config.evadeMotion),
            Pair("滞空", config.airMotion),
            Pair("瞄准", config.aimMotion),
            Pair("蓄力", config.magicMotion),
            Pair("被束缚", config.boundMotion),
            Pair("回血", config.healMotion),
            Pair("开枪", config.fireMotion),
            Pair("换弹", config.reloadMotion),
            Pair("蹲下", config.crouchMotion),
            Pair("完美闪避", config.perfectEvadeMotion)
        };

        if (config.idleFlavorMotions != null)
        {
            for (var i = 0; i < config.idleFlavorMotions.Length; i++)
            {
                referenced.Add(Pair($"待机小动作 {i + 1}", config.idleFlavorMotions[i]));
            }
        }

        sb.AppendLine("\n配置里引用的动作名：");

        foreach (var pair in referenced)
        {
            if (string.IsNullOrEmpty(pair.Value))
            {
                sb.AppendLine($"· {pair.Key}：留空（跳过）");
                continue;
            }

            if (!clipNames.Contains(pair.Value))
            {
                sb.AppendLine($"✗ {pair.Key}：\"{pair.Value}\" 不在配置的动作片段里");
                errors++;
                continue;
            }

            sb.AppendLine($"✓ {pair.Key}：{pair.Value}");
        }

        // ---------- 实例化一次，确认模型能起来 + 量尺寸 ----------
        sb.AppendLine("\n实例化测试：");
        errors += MeasureInstance(prefab, config, sb);

        sb.AppendLine();
        sb.AppendLine(errors == 0
            ? $"自检通过：{okClips} 个动作可用，配置里的动作名全部能找到。"
            : $"自检发现 {errors} 处问题（见上面的 ✗）。");

        if (errors == 0) Debug.Log(sb.ToString());
        else Debug.LogError(sb.ToString());
    }

    private static int MeasureInstance(GameObject prefab, Live2DHudConfig config, StringBuilder sb)
    {
        GameObject instance = null;

        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        catch (System.Exception e)
        {
            sb.AppendLine($"✗ 实例化 prefab 失败：{e.Message}");
            return 1;
        }

        if (instance == null)
        {
            sb.AppendLine("✗ 实例化 prefab 返回 null");
            return 1;
        }

        var errors = 0;

        try
        {
            instance.transform.position = Vector3.zero;

            var model = instance.GetComponent<CubismModel>();
            var actor = instance.GetComponent<Live2DActor>();

            sb.AppendLine(actor != null
                ? $"✓ Live2DActor 存在（actorId={actor.actorId}）"
                : "✗ 没有 Live2DActor");

            if (model == null)
            {
                sb.AppendLine("✗ 没有 CubismModel");
                errors++;
            }
            else
            {
                var drawables = model.Drawables;
                sb.AppendLine($"✓ CubismModel 起来了：Drawables = {(drawables == null ? 0 : drawables.Length)}");

                if (drawables == null || drawables.Length == 0)
                {
                    sb.AppendLine("✗ drawable 数量为 0，模型没初始化成功");
                    errors++;
                }
                else
                {
                    var ok = TryMeasureDrawableBounds(drawables, out var min, out var max);

                    if (!ok)
                    {
                        sb.AppendLine("✗ 顶点还没初始化，量不到包围盒（运行时第一帧之后再量就行，这里只影响自检）");
                    }
                    else
                    {
                        var size = max - min;
                        sb.AppendLine($"✓ 立绘本地尺寸：宽 {size.x:0.000}  高 {size.y:0.000}  中心 ({(min.x + max.x) * 0.5f:0.000}, {(min.y + max.y) * 0.5f:0.000})");
                        sb.AppendLine($"  实时 HUD 取景：bustFraction={config.bustFraction:0.00} " +
                                      $"（从头往下露 {size.y * config.bustFraction:0.000} 单位高），" +
                                      $"屏幕框 {config.bustSize.x:0.00}×{config.bustSize.y:0.00} @ 左上角 ({config.bustTopLeftAnchor.x:0.00},{config.bustTopLeftAnchor.y:0.00})");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            sb.AppendLine($"✗ 实例化后又出错：{e.Message}");
            errors++;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }

        return errors;
    }

    private static bool TryMeasureDrawableBounds(CubismDrawable[] drawables, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var any = false;

        foreach (var drawable in drawables)
        {
            if (drawable == null) continue;

            var vertices = drawable.VertexPositions;

            if (vertices == null || vertices.Length == 0) continue;

            foreach (var v in vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
                any = true;
            }
        }

        if (!any) return false;

        var size = max - min;
        return size.x > 0.01f && size.y > 0.01f;
    }

    private static GameObject SafePrefab(Live2DHudConfig config)
    {
        try
        {
            var prefab = config.modelPrefab;
            return prefab == null ? null : prefab;
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private static KeyValuePair<string, string> Pair(string key, string value)
    {
        return new KeyValuePair<string, string>(key, value ?? "");
    }

    private static string Mark(bool ok, ref int errors)
    {
        if (ok) return "✓";

        errors++;
        return "✗";
    }
}
