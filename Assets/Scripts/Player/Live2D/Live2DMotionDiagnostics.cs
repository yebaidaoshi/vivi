using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Live2D.Cubism.Framework.MotionFade;

/// <summary>
/// 挂在和 Live2DActor 同一个物体上（每个角色各挂一个），运行游戏或右键组件选择"运行动作诊断"，
/// 控制台会逐个列出该角色的动作片段是否正确对应到 CubismFadeMotionList 里的淡入淡出数据。
///
/// SDK 靠 AnimationClip 里名为 "InstanceId" 的事件，去 FadeMotionList.MotionInstanceIds 里查对应的
/// CubismFadeMotionData（淡入淡出时间、MotionLength 等）。对应错了，动作就会"播不到一半就停"、
/// 或者根本不播。这个脚本把这一整条链路打印出来。
/// </summary>
[RequireComponent(typeof(Live2DActor))]
public class Live2DMotionDiagnostics : MonoBehaviour
{
    public bool runOnStart = true;

    void Start()
    {
        if (runOnStart) Run();
    }

    [ContextMenu("运行动作诊断")]
    public void Run()
    {
        var actor = GetComponent<Live2DActor>();
        var fade = GetComponent<CubismFadeController>();
        var list = fade != null ? fade.CubismFadeMotionList : null;

        var sb = new StringBuilder();
        sb.AppendLine($"===== [Live2D 动作诊断] 角色 {actor.actorId} =====");

        if (fade == null)
        {
            sb.AppendLine("✗ 这个物体上没有 CubismFadeController 组件");
        }
        else if (list == null)
        {
            sb.AppendLine("✗ CubismFadeController 的 Cubism Fade Motion List 是空的");
        }
        else
        {
            int ids = list.MotionInstanceIds != null ? list.MotionInstanceIds.Length : 0;
            int objs = list.CubismFadeMotionObjects != null ? list.CubismFadeMotionObjects.Length : 0;
            sb.AppendLine($"FadeMotionList = {list.name}：登记了 {ids} 个 InstanceId，{objs} 个淡入淡出数据"
                          + (ids != objs ? "   ✗ 两个数量不一致！" : ""));
        }

        var clips = new List<AnimationClip>();
        if (actor.idleClip != null) clips.Add(actor.idleClip);
        foreach (var c in actor.motionClips)
            if (c != null && !clips.Contains(c)) clips.Add(c);

        var usedBy = new Dictionary<int, string>();   // 淡入淡出数据序号 -> 第一个用它的片段名
        int bad = 0;

        foreach (var clip in clips)
        {
            var events = clip.events.Where(e => e.functionName == "InstanceId").ToArray();
            string evText = events.Length == 0 ? "无" : string.Join("/", events.Select(e => e.intParameter));

            int index = -1;
            if (list != null && list.MotionInstanceIds != null)
            {
                foreach (var e in events)
                {
                    index = System.Array.IndexOf(list.MotionInstanceIds, e.intParameter);
                    if (index >= 0) break;
                }
                if (index < 0) index = System.Array.IndexOf(list.MotionInstanceIds, clip.GetInstanceID());
            }

            var problems = new List<string>();
            CubismFadeMotionData data = null;

            if (events.Length == 0) problems.Add("片段里没有 InstanceId 事件");
            if (index < 0) problems.Add("没有登记进 FadeMotionList（SDK 找不到它的淡入淡出数据）");
            else if (list.CubismFadeMotionObjects == null || index >= list.CubismFadeMotionObjects.Length)
                problems.Add($"登记序号 {index} 超出淡入淡出数据数组");
            else
            {
                data = list.CubismFadeMotionObjects[index];
                if (data == null) problems.Add("登记了但淡入淡出数据是空引用");
            }

            string dataText = "-";
            if (data != null)
            {
                string fadeName = Path.GetFileNameWithoutExtension(data.MotionName ?? "").Replace(".motion3", "");
                dataText = $"{fadeName} 长度{data.MotionLength:0.00}s 淡入{data.FadeInTime:0.0} 淡出{data.FadeOutTime:0.0}";

                if (!string.IsNullOrEmpty(fadeName) && !string.Equals(fadeName, clip.name, System.StringComparison.OrdinalIgnoreCase))
                    problems.Add($"对应到了别的动作的淡入淡出数据（{fadeName}）");
                if (Mathf.Abs(data.MotionLength - clip.length) > 0.05f)
                    problems.Add($"淡入淡出数据的长度 {data.MotionLength:0.00}s 与片段长度 {clip.length:0.00}s 不一致");
                if (usedBy.TryGetValue(index, out var other))
                    problems.Add($"和 {other} 指向同一个淡入淡出数据");
                else
                    usedBy[index] = clip.name;
                if (data.FadeInTime + data.FadeOutTime > clip.length + 0.01f)
                    problems.Add("提示: 淡入+淡出时间比动作本身还长");
            }

            bool hasError = problems.Any(p => !p.StartsWith("提示"));
            if (hasError) bad++;
            sb.AppendLine($"{(hasError ? "✗" : "✓")} {clip.name,-22} 长度{clip.length:0.00}s  InstanceId事件:{evText,-12} 淡入淡出数据:{dataText}"
                          + (problems.Count > 0 ? "\n      → " + string.Join("； ", problems) : ""));
        }

        sb.AppendLine(bad == 0 ? "全部动作与淡入淡出数据对应正常。" : $"有 {bad} 个动作的对应关系有问题（见上面的 ✗ 行）。");

        if (bad == 0) Debug.Log(sb.ToString());
        else Debug.LogWarning(sb.ToString());
    }
}
