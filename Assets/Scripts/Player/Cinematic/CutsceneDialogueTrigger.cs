using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在场景中一个独立的 GameObject 上，作为 Timeline Signal 的接收器。
/// 支持两种过场管理器：CutsceneManager（场景内触发式）和 CutscenePlayer（纯过场场景）。
/// </summary>
public class CutsceneDialogueTrigger : MonoBehaviour
{
    [Header("管理器引用（二选一，根据场景类型）")]
    [Tooltip("场景内触发式过场（有玩家游戏体）")]
    public CutsceneManager cutsceneManager;

    [Tooltip("纯过场场景（无玩家游戏体）")]
    public CutscenePlayer cutscenePlayer;

    [Header("对话段落 1")]
    public List<DialogueLine> dialogue1 = new List<DialogueLine>();

    [Header("对话段落 2")]
    public List<DialogueLine> dialogue2 = new List<DialogueLine>();

    [Header("对话段落 3")]
    public List<DialogueLine> dialogue3 = new List<DialogueLine>();

    // ============================================================
    // 这些方法由 Timeline 的 Signal Receiver 调用
    // ============================================================

    public void PlayDialogue1() => PlayDialogue(dialogue1, "1");
    public void PlayDialogue2() => PlayDialogue(dialogue2, "2");
    public void PlayDialogue3() => PlayDialogue(dialogue3, "3");

    private void PlayDialogue(List<DialogueLine> lines, string index)
    {
        if (cutsceneManager != null)
        {
            cutsceneManager.StartDialogueAndPause(lines);
            return;
        }

        if (cutscenePlayer != null)
        {
            cutscenePlayer.StartDialogueAndPause(lines);
            return;
        }

        Debug.LogError($"[CutsceneDialogueTrigger] PlayDialogue{index}: 两个管理器都未绑定！");
    }
}