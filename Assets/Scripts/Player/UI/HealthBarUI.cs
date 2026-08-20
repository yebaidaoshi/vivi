using UnityEngine;
using UnityEngine.UI;
using Player; // 引入 IDamageable 接口所在命名空间

/// <summary>
/// 挂载在 Slider 或 Image 上，自动绑定角色血量，实时更新显示百分比。
/// </summary>
[RequireComponent(typeof(Slider))]
public class HealthBarUI : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("留空则自动在父级或根物体查找 IDamageable")]
    public GameObject target;

    [Header("颜色（可选）")]
    public Gradient healthColorGradient = new Gradient()
    {
        colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.red, 0f),
            new GradientColorKey(Color.yellow, 0.5f),
            new GradientColorKey(Color.green, 1f)
        }
    };
    public Image fillImage; // 若指定，则随血量改变颜色

    private Slider slider;
    private IDamageable damageable;
    private float lastHealth = -1f;
    private float lastMax = -1f;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        if (fillImage == null)
        {
            // 尝试自动获取 Fill 区域的 Image（通常在 Fill 子物体上）
            var fillRect = transform.Find("Fill Area/Fill");
            if (fillRect != null)
                fillImage = fillRect.GetComponent<Image>();
        }
    }

    private void Start()
    {
        // 解析目标
        if (target == null)
            target = FindTarget();

        if (target != null)
            damageable = target.GetComponent<IDamageable>();

        if (damageable == null)
        {
            Debug.LogWarning($"[HealthBarUI] 在 {target?.name ?? "null"} 上未找到 IDamageable，血条将隐藏。");
            gameObject.SetActive(false);
            return;
        }

        // 首次更新
        UpdateHealth();
    }

    private void Update()
    {
        // 每帧检测血量变化（兼容无事件的情况）
        if (damageable != null)
            UpdateHealth();
    }

    private void UpdateHealth()
    {
        // 通过反射或接口获取当前/最大血量（IDamageable 未定义 MaxHealth，需额外读取）
        // 由于 IDamageable 只有 TakeDamage 和 IsDead，没有 MaxHealth，
        // 需要尝试转换为具体类型。
        float current = 0f, max = 1f;
        if (damageable is PlayerHealth ph)
        {
            current = ph.CurrentHealth;
            max = ph.MaxHealth;
        }
        else if (damageable is Enemy.EnemyHealth eh)
        {
            // EnemyHealth 有公共字段 maxHealth，但它是 private，需通过反射或增加公共属性。
            // 我们通过反射获取（或修改 EnemyHealth 添加 public int MaxHealth => maxHealth;）
            // 此处用反射：
            var maxField = eh.GetType().GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (maxField != null)
                max = (int)maxField.GetValue(eh);
            else
                max = 100f; // 降级默认值

            // 获取当前血量（EnemyHealth 有 currentHealth，也是 private）
            var curField = eh.GetType().GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (curField != null)
                current = (int)curField.GetValue(eh);
            else
                current = 0f;
        }
        else
        {
            // 降级：使用接口无法获取具体值，隐藏血条
            gameObject.SetActive(false);
            return;
        }

        // 避免重复更新
        if (Mathf.Approximately(current, lastHealth) && Mathf.Approximately(max, lastMax))
            return;

        lastHealth = current;
        lastMax = max;

        float percent = Mathf.Clamp01(current / max);
        slider.value = percent;

        // 改变颜色
        if (fillImage != null)
            fillImage.color = healthColorGradient.Evaluate(percent);
    }

    /// <summary>
    /// 自动寻找目标：先在本物体及其父级查找，若没有则尝试在根物体查找。
    /// </summary>
    private GameObject FindTarget()
    {
        // 从自身向上遍历查找 IDamageable
        Transform t = transform;
        while (t != null)
        {
            var comp = t.GetComponent<IDamageable>();
            if (comp != null)
                return t.gameObject;
            t = t.parent;
        }

        // 若 Canvas 挂载在角色下，则 Canvas 的根就是角色根
        var root = transform.root;
        if (root != transform)
        {
            var comp = root.GetComponent<IDamageable>();
            if (comp != null)
                return root.gameObject;
        }

        return null;
    }

    /// <summary>
    /// 外部手动指定目标（用于动态生成）。
    /// </summary>
    public void SetTarget(GameObject newTarget)
    {
        target = newTarget;
        damageable = target?.GetComponent<IDamageable>();
        if (damageable != null)
        {
            gameObject.SetActive(true);
            UpdateHealth();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}