using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ExtraAction
{
    public string actorId;
    public string motion = "";
    [Tooltip("这个动作播放完后接着播什么？（比如笑完变严肃）")]
    public string postMotion = ""; 
    public bool loopMotion = false;
}

[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string text;

    [Header("说话者表现配置")]
    public string actorId = "vivi";
    
    [Tooltip("第一个动作")]
    public string motion = "";
    [Tooltip("第一个动作播放完后，接着播放这个动作（留空则回到默认呼吸）")]
    public string postMotion = "";
    [Tooltip("第一个动作是否循环？（如果循环，就不会触发后续动作）")]
    public bool loopMotion = false;

    [Header("额外角色表现")]
    public List<ExtraAction> extraActions = new List<ExtraAction>();
}