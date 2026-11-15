using UnityEngine;

/// <summary>
/// 游戏内实时 Live2D 的配置（Resources/Live2D/Live2DHudConfig.asset）。
/// 里面既有"模型 + 整套动作片段"的资源引用，也有"什么状态播什么动作"的映射表。
/// 想改表现不用动代码，直接改这个资源即可。
/// </summary>
[CreateAssetMenu(fileName = "Live2DHudConfig", menuName = "Vivi/Live2D 实时 HUD 配置")]
public class Live2DHudConfig : ScriptableObject
{
    [Header("总开关")]
    [Tooltip("取消勾选后，游戏里完全不生成实时 Live2D")]
    public bool hudEnabled = true;

    [Header("资源")]
    [Tooltip("Vivi 的 Live2D prefab（Assets/Live2D/Cubism/Live2D/vivi/Vivi.prefab）")]
    public GameObject modelPrefab;

    [Tooltip("这个角色要用到的全部动作片段（必须都登记在 prefab 的 Cubism Fade Motion List 里）")]
    public AnimationClip[] motionClips;

    [Header("屏幕位置 / 大小（左上角、血条旁边，只露上半身）")]
    [Tooltip("立绘框左上角相对屏幕左上角的偏移（0,0 = 紧贴屏幕左上角；默认往下让开血条那一行）")]
    public Vector2 bustTopLeftAnchor = new Vector2(0.02f, 0.10f);

    [Tooltip("立绘框占整个屏幕的比例（宽, 高）")]
    public Vector2 bustSize = new Vector2(0.19f, 0.36f);

    [Tooltip("只露上半身：从头顶往下取整身高的多少（0.45 ≈ 头 + 胸；调小 = 更近的大头）")]
    [Range(0.1f, 1f)] public float bustFraction = 0.45f;

    [Tooltip("立绘额外缩放，一般保持 1；想放大靠调小 bustFraction")]
    public float bustScale = 1f;

    [Tooltip("立绘专用相机的 URP Renderer 索引（1 = CubismURPRenderer，上面有开启的 CubismRenderPassFeature）")]
    public int hudCameraRendererIndex = 1;

    [Tooltip("立绘专用相机只渲染这一层（项目里已有 L2D 层）")]
    public string hudCameraLayerName = "L2D";

    [Tooltip("立绘 UI 的 Canvas 排序层（越大越靠前，UI 会盖在场景上）")]
    public int canvasSortingOrder = 500;

    [Tooltip("立绘离相机的距离（只影响深度，不影响大小）")]
    public float depthFromCamera = 1f;

    [Tooltip("勾选 = 朝右（默认朝右，镜像一次就朝左）")]
    public bool faceRight = true;

    [Header("淡入 / 透明度（全部作用在 UI 那一层，整体一致，不会出现「某些部位更透」）")]
    [Range(0f, 1f)] public float opacity = 1f;
    public float fadeInSeconds = 0.9f;
    [Tooltip("死亡后立绘保留多少不透明度")]
    [Range(0f, 1f)] public float deadOpacity = 0.55f;
    [Tooltip("死亡后多久淡到 deadOpacity 秒")]
    public float deadFadeSeconds = 1.5f;
    [Tooltip("死亡时的整体压暗色（乘色，1 = 不变暗）")]
    public Color deadTint = new Color(0.72f, 0.72f, 0.72f, 1f);

    [Tooltip("过场演出（PlayerController.cutsceneMode）时把立绘淡出。默认关，免得一开场看不到立绘还以为没生成")]
    public bool hideDuringCutscene = false;

    [Header("血量 → 循环动作（原版就是按血量换的身体待机）")]
    public string hp100Motion = "HP_100";
    public string hp75Motion = "HP_075";
    public string hp50Motion = "HP_050";
    public string hp25Motion = "HP_025";
    public string hp01Motion = "HP_001";
    public string hp00Motion = "HP_000";

    [Header("事件动作")]
    [Tooltip("受击（两个交替播，避免每次都一样）")]
    public string damageMotion1 = "Damage1";
    public string damageMotion2 = "Damage2";

    [Tooltip("死亡时先播这一个，播完自动回到 hp00Motion 循环")]
    public string deathMotion = "Damage2";

    [Tooltip("近战挥砍")]
    public string attackMotion = "Face_Serious1";

    [Tooltip("闪避 / 无敌闪避")]
    public string evadeMotion = "Face_Surprise1";

    [Tooltip("跳起 / 滞空")]
    public string airMotion = "Face_Surprise2";

    [Tooltip("举枪瞄准（循环）")]
    public string aimMotion = "Face_Calm1";

    [Tooltip("魔法蓄力（循环）")]
    public string magicMotion = "Face_Serious2";

    [Tooltip("被敌人束缚（循环）")]
    public string boundMotion = "Face_Sad1";

    [Tooltip("回血")]
    public string healMotion = "Face_Smile1";

    [Tooltip("开枪（一次性反应）")]
    public string fireMotion = "Face_Serious3";

    [Tooltip("换弹")]
    public string reloadMotion = "Face_Lookup2";

    [Tooltip("蹲下 / 滑铲")]
    public string crouchMotion = "Face_Normal1";

    [Tooltip("完美闪避（躲开敌人攻击）")]
    public string perfectEvadeMotion = "Face_Smile2";

    [Tooltip("这些一次性反应各压住多久（秒）")]
    public float reactionHoldSeconds = 0.8f;

    [Header("待机小动作（站久了随机来一个，让它活着）")]
    public string[] idleFlavorMotions = { "Motion_IdontKnow", "Face_Behave1", "Face_Lookup2", "Face_Smile1", "Face_Sad2" };

    [Tooltip("待机多少秒后开始随机小动作（下限 / 上限）")]
    public Vector2 idleFlavorDelay = new Vector2(11f, 22f);

    [Tooltip("受击动作至少压住多久（秒），防止连击时状态抖动")]
    public float damageHoldSeconds = 0.35f;

    [Header("调试")]
    [Tooltip("在屏幕左上角显示一行实时状态（F2 也可以切换）")]
    public bool showDebugLine = false;

    [Tooltip("动作名在当前模型里找不到时打警告（调试用，正式跑可以关）")]
    public bool warnOnMissingMotion = false;
}
