using System.Reflection;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 游戏内实时 Live2D 的运行时冒烟测试。
/// 打开 forest 进 Play，确认 HUD 生成 / 绑定玩家 / 量到模型尺寸，
/// 然后手动驱动一次状态机（batchmode 下编辑器不推进帧循环），确认：
///   · 待机时播血量循环
///   · 掉血后播 Damage1 / Damage2
///
/// 菜单：Tools/Vivi/Live2D/运行时冒烟测试（会切场景、会进出 Play）
/// 命令行（注意不要加 -quit，由测试自己退出）：
///   Unity.exe -batchmode -nographics -projectPath &lt;工程&gt; -executeMethod Live2DHudPlayTest.RunBatch -logFile &lt;log&gt;
/// </summary>
[InitializeOnLoad]
public static class Live2DHudPlayTest
{
    private const string ScenePath = "Assets/Scenes/Game/forest.unity";
    private const string ActiveKey = "Live2DHudPlayTest.Active";
    private const string PhaseKey = "Live2DHudPlayTest.Phase";
    private const string ErrorKey = "Live2DHudPlayTest.Errors";
    private const string BatchKey = "Live2DHudPlayTest.Batch";
    private const string HealthKey = "Live2DHudPlayTest.Health";
    private const string AttemptKey = "Live2DHudPlayTest.Attempt";

    private static MethodInfo _tickPerformance;

    private static double _nextAt;

    static Live2DHudPlayTest()
    {
        // 进 Play 会域重载，静态字段全丢；靠 SessionState 把状态接上
        if (!SessionState.GetBool(ActiveKey, false))
        {
            return;
        }

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        _nextAt = EditorApplication.timeSinceStartup + 2.5;
    }

    [MenuItem("Tools/Vivi/Live2D/运行时冒烟测试（会切场景）")]
    public static void RunMenu()
    {
        Start(false);
    }

    public static void RunBatch()
    {
        Start(true);
    }

    private static void Start(bool batch)
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(BatchKey, batch);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetInt(ErrorKey, 0);
        SessionState.SetInt(AttemptKey, 0);

        _tickPerformance = typeof(Live2DRealtimeHud).GetMethod(
            "TickPerformance", BindingFlags.NonPublic | BindingFlags.Instance);

        Debug.Log("===== [实时 Live2D 冒烟测试] =====");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        _nextAt = EditorApplication.timeSinceStartup + 3.0;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup < _nextAt)
        {
            return;
        }

        switch (SessionState.GetInt(PhaseKey, 0))
        {
            case 0:
                // 等 HUD 生成 + 绑定玩家（batchmode 下 Start 会跑，只是帧不推进）
                if (!WaitHudReady())
                {
                    var attempts = SessionState.GetInt(AttemptKey, 0) + 1;
                    SessionState.SetInt(AttemptKey, attempts);

                    if (attempts <= 10)
                    {
                        _nextAt = EditorApplication.timeSinceStartup + 0.5;
                        break;
                    }

                    Fail("等了 5 秒 HUD 还是没有绑定玩家 / 没量到模型尺寸");
                }

                SessionState.SetInt(PhaseKey, 1);
                _nextAt = EditorApplication.timeSinceStartup + 0.1;
                break;

            case 1:
                CheckIdleLoop();
                SessionState.SetInt(PhaseKey, 2);
                _nextAt = EditorApplication.timeSinceStartup + 0.1;
                break;

            case 2:
                ApplyDamage();
                SessionState.SetInt(PhaseKey, 3);
                _nextAt = EditorApplication.timeSinceStartup + 0.1;
                break;

            case 3:
                CheckDamageReaction();
                SessionState.SetInt(PhaseKey, 4);
                _nextAt = EditorApplication.timeSinceStartup + 0.2;
                break;

            case 4:
                Finish();
                break;
        }
    }

    private static void Fail(string message)
    {
        SessionState.SetInt(ErrorKey, SessionState.GetInt(ErrorKey, 0) + 1);
        Debug.LogWarning("[实时 Live2D 冒烟测试] ✗ " + message);
    }

    private static Live2DRealtimeHud FindHud()
    {
        return Object.FindObjectOfType<Live2DRealtimeHud>();
    }

    /// <summary>手动跑一次 HUD 的状态机（batchmode 下帧循环不推进，只能自己调）。</summary>
    private static void DriveTick(Live2DRealtimeHud hud)
    {
        if (hud == null)
        {
            return;
        }

        // 进 Play 时域重载会把静态字段清掉，这里懒加载一次
        if (_tickPerformance == null)
        {
            _tickPerformance = typeof(Live2DRealtimeHud).GetMethod(
                "TickPerformance", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (_tickPerformance == null)
        {
            Fail("反射找不到 Live2DRealtimeHud.TickPerformance");
            return;
        }

        _tickPerformance.Invoke(hud, null);
    }

    private static bool WaitHudReady()
    {
        var hud = FindHud();

        if (hud == null || !hud.HasPlayer || !hud.IsReady)
        {
            return false;
        }

        Debug.Log($"[实时 Live2D 冒烟测试] ✓ HUD 已生成并绑定玩家：{hud.DebugInfo}");
        return true;
    }

    private static void CheckIdleLoop()
    {
        var hud = FindHud();

        if (hud == null)
        {
            Fail("HUD 不见了");
            return;
        }

        DriveTick(hud);

        Debug.Log($"[实时 Live2D 冒烟测试] 手动驱动一帧后：State={hud.CurrentState}  Motion={hud.CurrentMotion}  {hud.DebugInfo}");

        if (string.IsNullOrEmpty(hud.CurrentMotion))
        {
            Fail("状态机跑完还是没有任何动作");
            return;
        }

        if (!hud.CurrentMotion.StartsWith("HP_"))
        {
            Debug.Log($"[实时 Live2D 冒烟测试] 注意：玩家当前不是待机（{hud.CurrentState}），播的是 {hud.CurrentMotion}");
        }
        else
        {
            Debug.Log($"[实时 Live2D 冒烟测试] ✓ 待机时在播血量循环 {hud.CurrentMotion}");
        }
    }

    private static void ApplyDamage()
    {
        var hud = FindHud();
        var health = hud != null ? hud.BoundHealth : Object.FindObjectOfType<PlayerHealth>();

        if (health == null)
        {
            Fail("场景里找不到 PlayerHealth");
            return;
        }

        var before = health.CurrentHealth;
        SessionState.SetInt(HealthKey, before);
        health.TakeDamage(25, Vector2.zero, null);

        if (health.CurrentHealth == before)
        {
            Debug.Log("[实时 Live2D 冒烟测试] 真实伤害被无敌帧挡了，改为直接触发掉血事件");
            health.OnHealthChanged?.Invoke(Mathf.Max(0, before - 25), health.MaxHealth);
        }
        else
        {
            Debug.Log($"[实时 Live2D 冒烟测试] 真实伤害生效（{before} → {health.CurrentHealth}）");
        }
    }

    private static void CheckDamageReaction()
    {
        var hud = FindHud();

        if (hud == null)
        {
            Fail("受击后 HUD 不见了");
            return;
        }

        DriveTick(hud);

        var motion = hud.CurrentMotion;
        Debug.Log($"[实时 Live2D 冒烟测试] 受击后：State={hud.CurrentState}  Motion={motion}  {hud.DebugInfo}");

        if (string.IsNullOrEmpty(motion) || !motion.StartsWith("Damage"))
        {
            Fail($"受击后没播 Damage 动作（当前 {motion}）");
        }
        else
        {
            Debug.Log($"[实时 Live2D 冒烟测试] ✓ 受击动作已触发：{motion}");
        }
    }

    private static void Finish()
    {
        var errors = SessionState.GetInt(ErrorKey, 0);

        if (errors == 0) Debug.Log("===== [实时 Live2D 冒烟测试] 通过 =====");
        else Debug.LogError($"===== [实时 Live2D 冒烟测试] 失败：{errors} 处问题 =====");

        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;

        if (SessionState.GetBool(BatchKey, false))
        {
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
        else if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
    }
}
