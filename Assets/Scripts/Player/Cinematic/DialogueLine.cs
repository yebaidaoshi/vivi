using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [Tooltip("说话人名字")]
    public string speakerName;

    [TextArea(2, 5)]
    [Tooltip("对话内容")]
    public string text;

    [Tooltip("该句对话的最短显示时长（秒），防止玩家秒点跳过")]
    public float minDisplayTime = 0.3f;
}