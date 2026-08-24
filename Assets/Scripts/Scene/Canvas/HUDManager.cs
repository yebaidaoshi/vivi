using UnityEngine;
using TMPro;
using Player;

public class HUDManager : MonoBehaviour
{
    [Header("UI 文字（请把场景里的 AmmoText 拖进来）")]
    [SerializeField] private TextMeshProUGUI ammoText; // 只有这个必须手动拖一下

    [Header("显示格式")]
    [SerializeField] private string ammoFormat = "按下\"R\"换弹 当前子弹：{0}/{1}";

    private PlayerGun _playerGun;

    void Start()
    {
        // 1. 自动在场景里找 PlayerGun（不管你挂在哪，都能找到）
        _playerGun = FindObjectOfType<PlayerGun>(true);

        if (_playerGun == null)
        {
            Debug.LogError("【HUD】场景里没有找到 PlayerGun 组件！");
            if (ammoText != null) ammoText.text = "按下\"R\"换弹 未找到枪械";
            return;
        }

        // 2. 订阅事件
        _playerGun.OnAmmoChanged += UpdateUI;

        // 3. 首次刷新
        UpdateUI();
    }

    void UpdateUI()
    {
        if (ammoText == null) return;
        if (_playerGun == null) return;

        int current = _playerGun.Ammo;
        int max = _playerGun.MaxAmmo;

        // 核心：这里会把 {0}/{1} 替换成数字
        ammoText.text = string.Format(ammoFormat, current, max);
    }

    void OnDestroy()
    {
        if (_playerGun != null)
            _playerGun.OnAmmoChanged -= UpdateUI;
    }

    // ★ 万一自动查找还是失败，右键脚本组件，点这个手动刷新
    [ContextMenu("强制刷新子弹显示")]
    public void ForceRefresh()
    {
        if (_playerGun == null)
            _playerGun = FindObjectOfType<PlayerGun>(true);
        UpdateUI();
    }
}