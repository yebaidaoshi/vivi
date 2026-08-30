using UnityEngine;

/// <summary>
/// 挂载到触发区域（Box Collider 2D，勾选 Is Trigger），
/// 当玩家进入时触发过场动画。
/// </summary>
public class CutsceneTrigger_01 : MonoBehaviour
{
    [Header("过场管理器引用")]
    public CutsceneManager cutsceneManager;  // 拖入场景中的 CutsceneManager 物体

    [Header("触发设置")]
    public bool triggerOnce = true;          // 是否只触发一次
    public string playerTag = "Player";      // 玩家的 Tag，默认 "Player"

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 防止重复触发（如果启用了 triggerOnce）
        if (triggerOnce && hasTriggered)
            return;

        // 检查进入的是不是玩家
        if (!other.CompareTag(playerTag))
            return;

        // 检查管理器引用是否缺失
        if (cutsceneManager == null)
        {
            Debug.LogError("CutsceneTrigger_01: 未绑定 CutsceneManager！");
            return;
        }

        // 触发过场
        cutsceneManager.TriggerCutscene();

        // 标记已触发
        if (triggerOnce)
            hasTriggered = true;
    }

    // 可选：在 Scene 视图中绘制触发区域范围（便于调试）
    private void OnDrawGizmos()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
        }
    }
}