using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ExtraAction
{
    [Tooltip("要执行动作的角色 ID")]
    public string actorId;
    [Tooltip("动作名字")]
    public string motion;
    [Tooltip("是否循环播放这个动作")]
    public bool loopMotion = false;
}

[System.Serializable]
public class DialogueLine
{
    [Tooltip("说话人名字（仅用于UI显示）")]
    public string speakerName;

    [TextArea(2, 5)]
    [Tooltip("对话内容")]
    public string text;

    [Header("说话者表现配置")]
    [Tooltip("对应场景中 Live2DActor 的 actorId")]
    public string actorId = "vivi";

    [Tooltip("要切换的表情名（不带.exp3后缀），留空不改变")]
    public string expression = "";

    [Tooltip("要播放的动作名，留空不播放")]
    public string motion = "";

    [Tooltip("说话者的这个动作是否循环播放？")]
    public bool loopMotion = false;

    [Header("额外角色动作（让其他人也动起来）")]
    [Tooltip("在这里配置其他没说话的人，让他们也做特定的动作")]
    public List<ExtraAction> extraActions = new List<ExtraAction>();
}