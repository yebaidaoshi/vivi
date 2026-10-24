using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ScreenFader : MonoBehaviour
{
    [Header("UI 组件")]
    public Image fadeImage;
    public TextMeshProUGUI loadingText;

    [Header("过渡参数")]
    public float fadeDuration = 1f;
    public bool fadeInOnStart = false;

    private static ScreenFader _instance;
    private bool _pendingFadeIn = false;

    public static ScreenFader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ScreenFader>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ScreenFader");
                    _instance = go.AddComponent<ScreenFader>();
                }
                DontDestroyOnLoad(_instance.gameObject);
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>();
        if (fadeImage == null)
        {
            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(transform);
            fadeImage = imgObj.AddComponent<Image>();
        }

        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // 初始全透明
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        transform.SetAsLastSibling();

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        EnableRaycast(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[ScreenFader] OnSceneLoaded 触发 | 场景: {scene.name} | pendingFadeIn: {_pendingFadeIn}");

        if (_pendingFadeIn)
        {
            _pendingFadeIn = false;
            StartCoroutine(FadeInAndHideLoading());
        }
    }

    void Start()
    {
        if (fadeInOnStart)
        {
            SetAlpha(1f);
            StartCoroutine(FadeIn());
        }
    }

    // ============================================================
    // ★ 调试版：FadeOutWithLoading
    // ============================================================
    public IEnumerator FadeOutWithLoading()
    {
        Debug.LogWarning($"[ScreenFader] FadeOutWithLoading 被调用！\n调用堆栈:\n{System.Environment.StackTrace}");

        yield return StartCoroutine(FadeOut());
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "加载中...";
        }
        _pendingFadeIn = true;
    }

    public IEnumerator FadeInAndHideLoading()
    {
        Debug.Log($"[ScreenFader] FadeInAndHideLoading 被调用");

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);
        yield return StartCoroutine(FadeIn());
    }

    // ============================================================
    // ★ 调试版：FadeOut
    // ============================================================
    public IEnumerator FadeOut()
    {
        Debug.LogWarning($"[ScreenFader] FadeOut 被调用！\n调用堆栈:\n{System.Environment.StackTrace}");

        EnableRaycast(true);
        float timer = 0f;
        Color color = fadeImage.color;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        color.a = 1f;
        fadeImage.color = color;

        Debug.Log("[ScreenFader] FadeOut 完成（屏幕全黑）");
    }

    public IEnumerator FadeIn()
    {
        Debug.Log("[ScreenFader] FadeIn 开始");

        float timer = 0f;
        Color color = fadeImage.color;
        color.a = 1f;
        fadeImage.color = color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        color.a = 0f;
        fadeImage.color = color;
        EnableRaycast(false);

        Debug.Log("[ScreenFader] FadeIn 完成（屏幕透明）");
    }

    private void EnableRaycast(bool enable)
    {
        if (fadeImage != null)
            fadeImage.raycastTarget = enable;
    }

    private void SetAlpha(float a)
    {
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }
}