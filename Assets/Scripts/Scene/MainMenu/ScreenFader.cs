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
        if (_pendingFadeIn)
        {
            _pendingFadeIn = false;
            StartCoroutine(FadeInAndHideLoading());
        }
        else if (fadeImage != null && fadeImage.color.a > 0.9f)
        {
            StartCoroutine(FadeIn());
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

    public IEnumerator FadeOutWithLoading()
    {
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
            color.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
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
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
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