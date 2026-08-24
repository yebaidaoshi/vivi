using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Player; // PlayerJump / PlayerGun

public class GameCompleteTrigger : MonoBehaviour
{
    [Header("过渡设置")]
    public float fadeDuration = 1.5f;
    public string mainMenuSceneName = "MainMenu";

    [Header("通关UI")]
    public GameObject victoryPanel;

    [Header("通关文字（内容）")]
    public string victoryMessage = "🎉 通关！🎉";

    private bool isCompleted = false;
    private ScreenFader fader;

    private void Start()
    {
        fader = ScreenFader.Instance;
        if (victoryPanel != null)
        {
            // 如果 victoryPanel 自带 Canvas，提升排序
            Canvas canvas = victoryPanel.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
            }
            victoryPanel.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCompleted) return;
        if (!other.CompareTag("PlayerPresence")) return;

        isCompleted = true;
        StartCoroutine(CompleteGame());
    }

    IEnumerator CompleteGame()
    {
        // ---- 禁用玩家 ----
        GameObject player = GameObject.FindGameObjectWithTag("PlayerPresence");
        if (player != null)
        {
            var jump = player.GetComponent<PlayerJump>();
            if (jump != null) jump.enabled = false;

            var gun = player.GetComponent<PlayerGun>();
            if (gun != null) gun.enabled = false;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        // ---- 淡出 ----
        if (fader == null) fader = ScreenFader.Instance;
        if (fader == null) yield break;
        yield return StartCoroutine(fader.FadeOut());

        // ---- 显示胜利面板，仅设置文字内容，不破坏你的布局 ----
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            victoryPanel.transform.SetAsLastSibling(); // 确保同 Canvas 下显示最上层

            // 设置文字内容，保留你在编辑器中定义的所有格式
            var tmpText = victoryPanel.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = victoryMessage;
            }
            else
            {
                var legacyText = victoryPanel.GetComponentInChildren<UnityEngine.UI.Text>();
                if (legacyText != null)
                {
                    legacyText.text = victoryMessage;
                }
                else
                {
                    Debug.LogWarning("未找到 Text 组件，仅显示 Panel 本身。");
                }
            }

            // 确保 Panel 本身不透明
            CanvasGroup group = victoryPanel.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;

            Debug.Log("胜利面板已显示：" + victoryPanel.name);
        }
        else
        {
            Debug.LogError("victoryPanel 未设置！");
        }

        // ---- 等待后返回主菜单 ----
        yield return new WaitForSeconds(3f);
        SceneManager.LoadScene(mainMenuSceneName);
    }
}