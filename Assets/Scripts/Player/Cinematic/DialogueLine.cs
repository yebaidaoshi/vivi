using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [Tooltip("说话人名字（仅用于UI显示）")]
    public string speakerName;

    [TextArea(2, 5)]
    [Tooltip("对话内容")]
    public string text;

    [Tooltip("该句对话的最短显示时长（秒），防止玩家秒点跳过")]
    public float minDisplayTime = 0.3f;

    [Header("Live2D 表现配置（可选）")]
    [Tooltip("对应场景中 Live2DActor 的 actorId")]
    public string actorId = "vivi";

    [Tooltip("要切换的表情名（不带.exp3后缀，例如 Face_Smile1），留空不改变")]
    public string expression = "";

    [Tooltip("要播放的动作名，留空不播放")]
    public string motion = "";
}