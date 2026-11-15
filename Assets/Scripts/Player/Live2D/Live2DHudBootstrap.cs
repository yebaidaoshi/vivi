using UnityEngine;

/// <summary>
/// 进游戏时自动把"实时 Live2D"挂上，不需要在场景里摆任何东西。
///
/// 判断规则很简单：场景里出现 PlayerController（也就是真正在玩的关卡）才生成；
/// 主菜单、纯过场场景不生成（过场里的 Live2D 由 DialogueManager 那套负责）。
/// </summary>
public static class Live2DHudBootstrap
{
    /// <summary>配置资源路径（相对任意 Resources 目录，不含扩展名）。</summary>
    public const string ConfigResourcePath = "Live2D/Live2DHudConfig";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Object.FindObjectOfType<Live2DHudSpawner>() != null)
        {
            return;
        }

        var host = new GameObject("[Live2D HUD Spawner]");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<Live2DHudSpawner>();
    }
}
