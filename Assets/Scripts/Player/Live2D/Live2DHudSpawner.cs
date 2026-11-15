using UnityEngine;
using UnityEngine.SceneManagement;
using Player;

/// <summary>
/// 常驻的场景观察者：每个场景加载后等玩家出现，然后生成一次 Live2DRealtimeHud。
/// 由 Live2DHudBootstrap 自动创建，不需要手动挂。
/// </summary>
public class Live2DHudSpawner : MonoBehaviour
{
    /// <summary>玩家可能比场景晚几秒才实例化，这段时间内一直重试。</summary>
    private const float WatchSeconds = 8f;

    private Live2DHudConfig _config;
    private bool _configLoaded;
    private bool _loggedMissingConfig;
    private bool _spawned;
    private float _watchUntil;
    private float _pollTimer;

    private void Awake()
    {
        LoadConfig();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        BeginWatch();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginWatch();
    }

    private void Update()
    {
        if (_spawned || Time.unscaledTime > _watchUntil)
        {
            return;
        }

        _pollTimer -= Time.unscaledDeltaTime;

        if (_pollTimer > 0f)
        {
            return;
        }

        _pollTimer = 0.25f;
        Poll();
    }

    private void BeginWatch()
    {
        _spawned = false;
        _pollTimer = 0f;
        _watchUntil = Time.unscaledTime + WatchSeconds;
        Poll();
    }

    private void Poll()
    {
        if (_spawned)
        {
            return;
        }

        LoadConfig();

        if (!_configLoaded)
        {
            return;   // 资源加载失败会再等一帧重试（比如刚进游戏时 Resources 还没准备好）
        }

        if (_config == null || !_config.hudEnabled)
        {
            _spawned = true;
            return;
        }

        if (FindObjectOfType<PlayerController>() == null)
        {
            return;   // 这个场景没有玩家：主菜单 / 过场，不生成
        }

        if (FindObjectOfType<Live2DRealtimeHud>() != null)
        {
            _spawned = true;
            return;
        }

        var hud = Live2DRealtimeHud.Spawn(_config);
        _spawned = hud != null;

        if (hud == null)
        {
            _watchUntil = 0f;   // 生成失败（配置 / Prefab 有问题）就别一直重试
        }
    }

    private void LoadConfig()
    {
        if (_configLoaded)
        {
            return;
        }

        _config = Resources.Load<Live2DHudConfig>(Live2DHudBootstrap.ConfigResourcePath);

        if (_config == null)
        {
            // 兼容：万一资源被改名了，就在 Resources/Live2D 下随便找一个
            var all = Resources.LoadAll<Live2DHudConfig>("Live2D");

            if (all != null && all.Length > 0)
            {
                _config = all[0];
            }
        }

        _configLoaded = true;

        if (_config == null && !_loggedMissingConfig)
        {
            _loggedMissingConfig = true;
            Debug.LogWarning(
                "[Live2D HUD] 找不到 Live2DHudConfig（应在 Assets/Resources/Live2D/Live2DHudConfig.asset）。" +
                "游戏内实时 Live2D 不会显示。");
        }
    }
}
