using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

        // ★ 获取或创建 Image
        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>();
        if (fadeImage == null)
        {
            // 如果还是没有，自己创建一个 Image
            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(transform);
            fadeImage = imgObj.AddComponent<Image>();
        }

        // ★ 强制 Image 铺满父 Canvas
        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // ★ 设置颜色为黑色，完全不透明
        fadeImage.color = Color.black;

        // ★ 将本物体移到 Canvas 的最底部（渲染顺序最后，即在最上层）
        transform.SetAsLastSibling();

        // 隐藏加载文字
        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        // 初始关闭射线检测
        EnableRaycast(false);
    }

    void Start()
    {
        if (fadeInOnStart)
        {
            SetAlpha(1f);
            StartCoroutine(FadeIn());
        }
    }

    public IEnumerator FadeOutWithLoading()
    {
        yield return StartCoroutine(FadeOut());
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "加载中...";
        }
    }

    public IEnumerator FadeInAndHideLoading()
    {
        if (loadingText != null)
            loadingText.gameObject.SetActive(false);
        yield return StartCoroutine(FadeIn());
    }

    public IEnumerator FadeOut()
    {
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
    }

    public IEnumerator FadeIn()
    {
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