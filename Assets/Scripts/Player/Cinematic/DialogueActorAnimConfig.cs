using UnityEngine;

/// <summary>
/// 对话期间某个角色的动画配置。
/// </summary>
[System.Serializable]
public class DialogueActorAnimConfig
{
    [Tooltip("角色电影体（含 SkeletonAnimation 的 GameObject）")]
    public GameObject actor;

    [Tooltip("对话期间该角色播放的动画名（必须与 Spine 资源中的名字完全一致）")]
    public string idleAnimName = "Idle";
}