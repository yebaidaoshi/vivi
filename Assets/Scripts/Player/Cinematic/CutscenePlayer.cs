using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutscenePlayer : MonoBehaviour
{
    [Header("Timeline 导演")]
    public PlayableDirector director;

    [Header("自动播放")]
    public float startDelay = 0.5f;

    [Header("场景切换")]
    public string nextSceneName = "Level_01";
    public bool useAsyncLoad = true;

    [Header("黑幕过渡")]
    public bool useFadeOut = true;
    public float fadeDuration = 0.5f;
    public Image fadeImage;

    [Header("上下黑边")]
    public bool useLetterbox = true;
    public float letterboxTargetHeight = 120f;
    public float letterboxDuration = 0.5f;
    public GameObject letterboxRoot;
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;

    [Header("跳过功能")]
    public bool allowSkip = false;
    public KeyCode skipKey = KeyCode.Escape;
    public GameObject skipHintUI;

    [Header("跨场景保留")]
    [Tooltip("是否让 CutscenePlayer 跨场景保留（用于在新场景淡出黑幕）")]
    public bool persistAcrossScenes = true;

    [Header("对话系统")]
    public DialogueManager dialogueManager;

    [Header("对话期间的默认动画")]
    [Tooltip("对话时是否让角色播放默认动画（可关闭）")]
    public bool useDialogueIdleAnimation = true;

    [Tooltip("每个角色对话期间播放的动画配置（包含电影体本身和 NPC）")]
    public DialogueActorAnimConfig[] dialogueAnims;

    // 对话期间 Spine 驱动
    private List<Spine.Unity.SkeletonAnimation> _allDialogueSpines = new List<Spine.Unity.SkeletonAnimation>();
    private bool _isInDialogue = false;

    // 内部状态
    private bool isPlaying = false;
    private bool isEnding = false;
    private bool _fadeStarted = false;

    void Awake()
    {
        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _fadeStarted = false;

        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = useFadeOut ? 1f : 0f;
            fadeImage.color = c;
        }

        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0, 0);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0, 0);
        if (letterboxRoot != null) letterboxRoot.SetActive(false);

        RebuildDialogueSpineList();

        if (director != null)
        {
            director.stopped += OnCutsceneStopped;
        }
        else
        {
            Debug.LogError("[CutscenePlayer] director 未绑定！");
            return;
        }

        if (dialogueManager != null)
            dialogueManager.gameObject.SetActive(true);

        StartCoroutine(PlayRoutine());
    }

    private void RebuildDialogueSpineList()
    {
        _allDialogueSpines.Clear();

        if (dialogueAnims == null) return;

        foreach (var cfg in dialogueAnims)
        {
            if (cfg == null || cfg.actor == null) continue;

            var spine = cfg.actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
            if (spine != null && !_allDialogueSpines.Contains(spine))
                _allDialogueSpines.Add(spine);
        }
    }

    void Update()
    {
        if (allowSkip && isPlaying && !isEnding && Input.GetKeyDown(skipKey))
            SkipCutscene();

        if (!_isInDialogue) return;
        if (!useDialogueIdleAnimation) return;
        if (_allDialogueSpines.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < _allDialogueSpines.Count; i++)
        {
            var spine = _allDialogueSpines[i];
            if (spine == null || spine.AnimationState == null) continue;

            spine.AnimationState.Update(dt);
            spine.AnimationState.Apply(spine.Skeleton);
            spine.Skeleton.UpdateWorldTransform();
        }
    }

    public void StartDialogueAndPause(List<DialogueLine> lines)
    {
        if (dialogueManager == null)
        {
            Debug.LogWarning("[CutscenePlayer] 未绑定 DialogueManager，无法播放对话！");
            return;
        }

        director.Pause();
        _isInDialogue = true;

        if (useDialogueIdleAnimation)
            PlayAllDialogueAnimations();

        dialogueManager.StartDialogue(lines, () =>
        {
            _isInDialogue = false;
            director.Resume();
        });
    }

    private void PlayAllDialogueAnimations()
    {
        if (dialogueAnims == null) return;

        foreach (var cfg in dialogueAnims)
        {
            if (cfg == null || cfg.actor == null) continue;

            var spine = cfg.actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
            if (spine == null) continue;

            PlaySpineAnimation(spine, cfg.idleAnimName, cfg.actor.name);
        }
    }

    private void PlaySpineAnimation(Spine.Unity.SkeletonAnimation spine, string animName, string label)
    {
        if (spine == null || spine.AnimationState == null) return;
        if (string.IsNullOrEmpty(animName)) return;
        if (spine.Skeleton.Data.FindAnimation(animName) == null) return;

        spine.AnimationState.SetAnimation(0, animName, true);
        spine.AnimationState.Update(0f);
        spine.AnimationState.Apply(spine.Skeleton);
        spine.Skeleton.UpdateWorldTransform();
    }

    IEnumerator PlayRoutine()
    {
        yield return new WaitForSecondsRealtime(startDelay);

        if (useFadeOut && fadeImage != null)
        {
            yield return StartCoroutine(FadeTo(0f, fadeDuration));
        }

        if (useLetterbox && letterboxRoot != null)
            letterboxRoot.SetActive(true);

        if (useLetterbox)
            StartCoroutine(LetterboxAnimation(0f, letterboxTargetHeight, letterboxDuration));

        if (allowSkip && skipHintUI != null)
            skipHintUI.SetActive(true);

        isPlaying = true;
        director.Play();

        StartCoroutine(WaitAndFadeInRoutine());
    }

    IEnumerator WaitAndFadeInRoutine()
    {
        float waitTimeout = 3f;
        float waited = 0f;
        while (director.state != PlayState.Playing && waited < waitTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (director.state != PlayState.Playing) yield break;

        while (director.time <= 0.01 && !isEnding && director.state == PlayState.Playing)
        {
            yield return null;
        }

        double targetTime = director.duration - fadeDuration;

        if (targetTime <= 0.1) yield break;

        while (director.time < targetTime && !isEnding)
        {
            if (director.time <= 0.01 && director.state != PlayState.Playing
                && director.state != PlayState.Paused)
            {
                yield break;
            }
            yield return null;
        }

        if (!isEnding && useFadeOut && fadeImage != null)
        {
            _fadeStarted = true;
            yield return StartCoroutine(FadeTo(1f, fadeDuration));
        }
    }

    private void OnCutsceneStopped(PlayableDirector obj)
    {
        if (isEnding) return;
        director.stopped -= OnCutsceneStopped;

        if (this == null || !gameObject.activeInHierarchy) return;

        StartCoroutine(EndRoutine());
    }

    IEnumerator EndRoutine()
    {
        isEnding = true;

        if (skipHintUI != null)
            skipHintUI.SetActive(false);

        if (useFadeOut && fadeImage != null && !_fadeStarted)
        {
            Color c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
        }

        if (useLetterbox)
        {
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0, 0);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0, 0);
            if (letterboxRoot != null) letterboxRoot.SetActive(false);
        }

        yield return null;

        yield return StartCoroutine(LoadNextSceneAndFadeOut());
    }

    IEnumerator LoadNextSceneAndFadeOut()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[CutscenePlayer] nextSceneName 为空！");
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[CutscenePlayer] 场景 '{nextSceneName}' 不在 Build Settings 中！");
            yield break;
        }

        if (useAsyncLoad)
        {
            AsyncOperation async = SceneManager.LoadSceneAsync(nextSceneName);
            async.allowSceneActivation = false;
            while (async.progress < 0.9f) yield return null;
            async.allowSceneActivation = true;
            while (!async.isDone) yield return null;
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // ★ 优先使用全局 ScreenFader 进行淡入，彻底防止死黑屏
        if (ScreenFader.Instance != null)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeIn());
        }
        else if (useFadeOut && fadeImage != null)
        {
            yield return StartCoroutine(FadeTo(0f, fadeDuration));
        }

        if (fadeImage != null && fadeImage.transform != null && !fadeImage.transform.IsChildOf(transform))
            Destroy(fadeImage.gameObject);

        if (letterboxRoot != null && letterboxRoot.transform != null && !letterboxRoot.transform.IsChildOf(transform))
            Destroy(letterboxRoot);

        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    private void SkipCutscene()
    {
        if (isEnding) return;
        director.Stop();
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;

        float startAlpha = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = fadeImage.color;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            fadeImage.color = c;
            yield return null;
        }

        Color final = fadeImage.color;
        final.a = targetAlpha;
        fadeImage.color = final;
    }

    private IEnumerator LetterboxAnimation(float fromHeight, float toHeight, float duration)
    {
        if (letterboxTop == null || letterboxBottom == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float h = Mathf.Lerp(fromHeight, toHeight, smoothT);

            letterboxTop.sizeDelta = new Vector2(0, h);
            letterboxBottom.sizeDelta = new Vector2(0, h);
            yield return null;
        }

        letterboxTop.sizeDelta = new Vector2(0, toHeight);
        letterboxBottom.sizeDelta = new Vector2(0, toHeight);
    }

    void OnValidate()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();
    }
}