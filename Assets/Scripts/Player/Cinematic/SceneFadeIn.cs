using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SceneFadeIn : MonoBehaviour
{
    [Tooltip("黑幕保持全黑的时长（秒，真实时间）")]
    public float holdDuration = 0.4f;

    [Tooltip("淡出时长（秒，真实时间）")]
    public float fadeDuration = 0.6f;

    private Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();

        Color c = _image.color;
        c.a = 1f;
        _image.color = c;

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
    }

    void Start()
    {
        StartCoroutine(FadeOutRoutine());
    }

    IEnumerator FadeOutRoutine()
    {
        // ★ 用 Realtime，不受 Time.timeScale 影响
        yield return new WaitForSecondsRealtime(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // ★ 用 unscaledDeltaTime，同样不受 timeScale 影响
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            Color c = _image.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            _image.color = c;
            yield return null;
        }

        Color final = _image.color;
        final.a = 0f;
        _image.color = final;

        gameObject.SetActive(false);
    }
}