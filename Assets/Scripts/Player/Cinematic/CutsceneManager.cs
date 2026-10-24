using System.Collections;
<<<<<<< Updated upstream
using UnityEngine;
using UnityEngine.Playables;
using Player;
using Chronos; // 引入 Chronos 命名空间

public class CutsceneManager : MonoBehaviour
{
    public PlayableDirector director;
    public GameObject gameplayPlayer;
    public GameObject cinematicPlayer;

=======
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using Player;

public class CutsceneManager : MonoBehaviour
{
    [Header("Timeline 导演")]
    public PlayableDirector director;

    [Header("玩家：游戏体 & 电影体")]
    public GameObject gameplayPlayer;
    public GameObject cinematicPlayer;

    [Header("玩家电影体：实际移动的子物体")]
    public Transform cinematicVisual;

    [Header("NPC 电影体（过场时激活，平时隐藏）")]
    public GameObject[] npcCinematicActors;

    [Header("相机")]
    public Camera mainCamera;

    [Header("黑幕过渡（可选）")]
    public bool useFadeOut = true;
    public float fadeDuration = 0.3f;
    public Image fadeImage;

    [Header("上下黑边（Letterbox）")]
    public bool useLetterbox = true;
    public float letterboxTargetHeight = 120f;
    public float letterboxDuration = 0.5f;
    public GameObject letterboxRoot;
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;

    [Header("过场时隐藏的 UI（淡出/淡入）")]
    public CanvasGroup[] hiddenDuringCutscene;
    public float uiFadeDuration = 0.3f;

    [Header("动画进度同步")]
    [Tooltip("是否把电影体最后一帧的动画进度同步到游戏体 Idle 上")]
    public bool syncAnimationProgress = true;
    [Tooltip("游戏体 Idle 状态名（必须与 Animator 中的完全一致）")]
    public string gameplayIdleStateName = "Idle";

    [Header("对话系统")]
    public DialogueManager dialogueManager;

    [Header("对话期间的默认动画")]
    [Tooltip("对话时是否让角色播放默认动画（可关闭）")]
    public bool useDialogueIdleAnimation = true;

    [Tooltip("玩家电影体对话期间播放的动画名")]
    public string playerDialogueAnimName = "Idle";

    [Tooltip("每个 NPC 对话期间的动画配置")]
    public DialogueActorAnimConfig[] npcDialogueAnims;

    // 内部引用
>>>>>>> Stashed changes
    private PlayerController playerController;
    private Animator gameplayAnimator;
    private Rigidbody2D gameplayRigidbody;
    private Renderer[] gameplayRenderers;
<<<<<<< Updated upstream
    private LocalClock localClock; // Chronos Local Clock 引用
    private bool isCutscenePlaying = false;

=======
    private ScoutCameraFinal cameraScript;

    // 对话期间 Spine 驱动
    private Spine.Unity.SkeletonAnimation _playerSpine;
    private List<Spine.Unity.SkeletonAnimation> _allDialogueSpines = new List<Spine.Unity.SkeletonAnimation>();
    private bool _isInDialogue = false;

    // 对话期间保存玩家电影体的位置（子物体 cinematicVisual）
    private Vector3 _dialogueSavedPosition;
    private Quaternion _dialogueSavedRotation;
    private bool _hasSavedCinematicPos = false;

    // 对话期间保存玩家电影体根物体（cinematicPlayer）的位置
    private Vector3 _dialogueSavedPlayerRootPosition;
    private Quaternion _dialogueSavedPlayerRootRotation;

    // 临时禁用的 RootMotion 组件（对话结束后恢复）
    private List<MonoBehaviour> _disabledRootMotions = new List<MonoBehaviour>();

    // 状态
    private bool isCutscenePlaying = false;

    // 缓存
    private RigidbodyType2D _originalBodyType;
    private Vector3 _lastCinematicPosition;
    private Quaternion _lastCinematicRotation;
    private Vector3 _lastCinematicScale;

    // 电影体初始状态
    private Vector3 _cinematicInitialPosition;
    private Quaternion _cinematicInitialRotation;
    private Vector3 _cinematicInitialScale;

>>>>>>> Stashed changes
    void Start()
    {
        gameplayAnimator = gameplayPlayer.GetComponent<Animator>();
        gameplayRigidbody = gameplayPlayer.GetComponent<Rigidbody2D>();
        playerController = gameplayPlayer.GetComponent<PlayerController>();
        gameplayRenderers = gameplayPlayer.GetComponentsInChildren<Renderer>();

<<<<<<< Updated upstream
        // 获取 Local Clock 组件
        localClock = gameplayPlayer.GetComponent<LocalClock>();
        if (localClock == null)
            Debug.LogWarning("未找到 LocalClock，Chronos 暂停功能将不可用。");

        if (playerController == null)
            Debug.LogError("未找到 PlayerController！");

        SetGameplayVisible(true);
        cinematicPlayer.SetActive(false);
        director.stopped += OnCutsceneStopped;
    }

=======
        if (playerController == null)
            Debug.LogError("[CutsceneManager] 未找到 PlayerController！");

        if (cinematicVisual == null && cinematicPlayer != null)
        {
            cinematicVisual = cinematicPlayer.transform.Find("vivi_Cinematic");
            if (cinematicVisual == null)
                Debug.LogError("[CutsceneManager] 未找到 vivi_Cinematic！");
        }

        if (cinematicPlayer != null)
        {
            _playerSpine = cinematicPlayer.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
            if (_playerSpine == null)
                Debug.LogWarning("[CutsceneManager] 未找到玩家电影体的 SkeletonAnimation。");
        }

        RebuildDialogueSpineList();

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) cameraScript = mainCamera.GetComponent<ScoutCameraFinal>();

        if (gameplayRigidbody != null)
            _originalBodyType = gameplayRigidbody.bodyType;

        if (cinematicPlayer != null)
        {
            _cinematicInitialPosition = cinematicPlayer.transform.position;
            _cinematicInitialRotation = cinematicPlayer.transform.rotation;
            _cinematicInitialScale = cinematicPlayer.transform.localScale;
        }

        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }

        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0, 0);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0, 0);
        if (letterboxRoot != null) letterboxRoot.SetActive(false);

        if (hiddenDuringCutscene != null)
        {
            foreach (var cg in hiddenDuringCutscene)
            {
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }
        }

        SetGameplayVisible(true);
        if (cinematicPlayer != null) cinematicPlayer.SetActive(false);
        HideAllNpcActors();

        director.stopped += OnCutsceneStopped;

        if (dialogueManager != null)
            dialogueManager.gameObject.SetActive(true);
    }

    // ============================================================
    // 收集所有需要驱动的 Spine
    // ============================================================
    private void RebuildDialogueSpineList()
    {
        _allDialogueSpines.Clear();

        if (_playerSpine != null)
            _allDialogueSpines.Add(_playerSpine);

        if (npcDialogueAnims != null)
        {
            foreach (var cfg in npcDialogueAnims)
            {
                if (cfg == null || cfg.actor == null) continue;

                var spine = cfg.actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
                if (spine != null && !_allDialogueSpines.Contains(spine))
                    _allDialogueSpines.Add(spine);
            }
        }
    }

    // ============================================================
    // Update：对话期间手动驱动 Spine 动画
    // ============================================================
    private void Update()
    {
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

    // ============================================================
    // LateUpdate：在动画系统之后恢复位置，防止 Spine 根骨骼位移推走角色
    // ============================================================
    private void LateUpdate()
    {
        if (!_isInDialogue) return;
        if (!_hasSavedCinematicPos) return;

        if (cinematicVisual != null)
        {
            cinematicVisual.position = _dialogueSavedPosition;
            cinematicVisual.rotation = _dialogueSavedRotation;
        }

        if (cinematicPlayer != null)
        {
            cinematicPlayer.transform.position = _dialogueSavedPlayerRootPosition;
            cinematicPlayer.transform.rotation = _dialogueSavedPlayerRootRotation;
        }
    }

    // ============================================================
    // 触发过场
    // ============================================================
>>>>>>> Stashed changes
    public void TriggerCutscene()
    {
        if (playerController == null) return;

        playerController.cutsceneMode = true;
        isCutscenePlaying = true;

<<<<<<< Updated upstream
        // ★ 暂停 Chronos Local Clock：将时间流速设为 0
        if (localClock != null)
            localClock.localTimeScale = 0f;

        // 清零物理速度
        if (gameplayRigidbody != null)
        {
            gameplayRigidbody.velocity = Vector2.zero;
            gameplayRigidbody.angularVelocity = 0f;
            // 保险：关闭物理模拟，防止任何残留物理
            gameplayRigidbody.simulated = false;
        }
=======
        if (cameraScript != null)
            cameraScript.enabled = false;
>>>>>>> Stashed changes

        StartCoroutine(WaitForIdleAndSwitch());
    }

<<<<<<< Updated upstream
=======
    // ============================================================
    // 等待自然 Idle → 切换到电影体
    // ============================================================
>>>>>>> Stashed changes
    IEnumerator WaitForIdleAndSwitch()
    {
        while (true)
        {
            if (gameplayAnimator == null) yield break;
            AnimatorStateInfo stateInfo = gameplayAnimator.GetCurrentAnimatorStateInfo(0);
<<<<<<< Updated upstream
            if (stateInfo.IsName("Idle")) break;
            yield return null;
        }

        // 同步初始位置
        cinematicPlayer.transform.position = gameplayPlayer.transform.position;
        cinematicPlayer.transform.rotation = gameplayPlayer.transform.rotation;
        cinematicPlayer.transform.localScale = gameplayPlayer.transform.localScale;

        // ★ 关键：不隐藏游戏体，只禁用渲染
        SetGameplayVisible(false);
        cinematicPlayer.SetActive(true);
=======
            if (stateInfo.IsName("Idle"))
                break;
            yield return null;
        }

        if (gameplayRigidbody != null)
        {
            gameplayRigidbody.velocity = Vector2.zero;
            gameplayRigidbody.angularVelocity = 0f;
            gameplayRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (playerController != null)
            playerController.ControlLocomotion = false;

        if (cinematicPlayer != null)
        {
            cinematicPlayer.transform.position = gameplayPlayer.transform.position;
            cinematicPlayer.transform.rotation = gameplayPlayer.transform.rotation;
            cinematicPlayer.transform.localScale = gameplayPlayer.transform.localScale;
        }

        SetGameplayVisible(false);
        if (cinematicPlayer != null) cinematicPlayer.SetActive(true);
        ShowAllNpcActors();

        if (useLetterbox && letterboxRoot != null)
            letterboxRoot.SetActive(true);

        if (useLetterbox)
            StartCoroutine(LetterboxAnimation(0f, letterboxTargetHeight, letterboxDuration));

        if (hiddenDuringCutscene != null && hiddenDuringCutscene.Length > 0)
            StartCoroutine(FadeUI(0f, uiFadeDuration));

>>>>>>> Stashed changes
        director.Play();

        StartCoroutine(SyncPositionDuringCutscene());
    }

<<<<<<< Updated upstream
=======
    // ============================================================
    // 过场期间同步位置
    // ============================================================
>>>>>>> Stashed changes
    IEnumerator SyncPositionDuringCutscene()
    {
        while (isCutscenePlaying)
        {
<<<<<<< Updated upstream
            // 游戏体仍处于激活状态，Transform 可正常修改
            gameplayPlayer.transform.position = cinematicPlayer.transform.position;
            gameplayPlayer.transform.rotation = cinematicPlayer.transform.rotation;
            gameplayPlayer.transform.localScale = cinematicPlayer.transform.localScale;

            if (gameplayRigidbody != null)
            {
                gameplayRigidbody.position = cinematicPlayer.transform.position;
                gameplayRigidbody.rotation = cinematicPlayer.transform.rotation.eulerAngles.z;
                gameplayRigidbody.velocity = Vector2.zero;
                gameplayRigidbody.angularVelocity = 0f;
            }

=======
            if (!_isInDialogue && cinematicVisual != null)
            {
                _lastCinematicPosition = cinematicVisual.position;
                _lastCinematicRotation = cinematicVisual.rotation;
                _lastCinematicScale = cinematicVisual.localScale;

                gameplayPlayer.transform.position = _lastCinematicPosition;
                gameplayPlayer.transform.rotation = _lastCinematicRotation;

                if (gameplayRigidbody != null)
                {
                    gameplayRigidbody.position = _lastCinematicPosition;
                    gameplayRigidbody.rotation = _lastCinematicRotation.eulerAngles.z;
                    gameplayRigidbody.velocity = Vector2.zero;
                    gameplayRigidbody.angularVelocity = 0f;
                }
            }
>>>>>>> Stashed changes
            yield return null;
        }
    }

<<<<<<< Updated upstream
    private void SetGameplayVisible(bool visible)
    {
        foreach (var r in gameplayRenderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private void OnCutsceneStopped(PlayableDirector obj)
    {
        isCutscenePlaying = false;

        // 最终位置同步
        Vector3 finalPos = cinematicPlayer.transform.position;
        Quaternion finalRot = cinematicPlayer.transform.rotation;
        Vector3 finalScale = cinematicPlayer.transform.localScale;

        gameplayPlayer.transform.position = finalPos;
        gameplayPlayer.transform.rotation = finalRot;
        gameplayPlayer.transform.localScale = finalScale;
=======
    // ============================================================
    // 对话入口
    // ============================================================
    public void StartDialogueAndPause(List<DialogueLine> lines)
    {
        if (dialogueManager == null)
        {
            Debug.LogWarning("[CutsceneManager] 未绑定 DialogueManager，无法播放对话！");
            return;
        }

        if (cinematicVisual != null)
        {
            _dialogueSavedPosition = cinematicVisual.position;
            _dialogueSavedRotation = cinematicVisual.rotation;
            _hasSavedCinematicPos = true;
        }

        if (cinematicPlayer != null)
        {
            _dialogueSavedPlayerRootPosition = cinematicPlayer.transform.position;
            _dialogueSavedPlayerRootRotation = cinematicPlayer.transform.rotation;
        }

        DisableRootMotionComponents();

        director.Pause();
        _isInDialogue = true;

        if (useDialogueIdleAnimation)
            PlayAllDialogueAnimations();

        dialogueManager.StartDialogue(lines, () =>
        {
            _isInDialogue = false;
            _hasSavedCinematicPos = false;

            RestoreRootMotionComponents();

            director.Resume();
        });
    }

    // ============================================================
    // 禁用 / 恢复 RootMotion 组件
    // ============================================================
    private void DisableRootMotionComponents()
    {
        _disabledRootMotions.Clear();

        if (_playerSpine == null) return;

        var allComponents = _playerSpine.GetComponents<MonoBehaviour>();
        foreach (var mb in allComponents)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (typeName.Contains("RootMotion") || typeName.Contains("SkeletonRootMotion"))
            {
                if (mb.enabled)
                {
                    mb.enabled = false;
                    _disabledRootMotions.Add(mb);
                }
            }
        }

        if (cinematicPlayer != null)
        {
            var allMbs = cinematicPlayer.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in allMbs)
            {
                if (mb == null) continue;
                if (_disabledRootMotions.Contains(mb)) continue;

                string typeName = mb.GetType().Name;
                if (typeName.Contains("RootMotion") || typeName.Contains("SkeletonRootMotion"))
                {
                    if (mb.enabled)
                    {
                        mb.enabled = false;
                        _disabledRootMotions.Add(mb);
                    }
                }
            }
        }
    }

    private void RestoreRootMotionComponents()
    {
        foreach (var mb in _disabledRootMotions)
        {
            if (mb != null) mb.enabled = true;
        }
        _disabledRootMotions.Clear();
    }

    // ============================================================
    // 播放对话动画
    // ============================================================
    private void PlayAllDialogueAnimations()
    {
        if (_playerSpine != null)
            PlaySpineAnimation(_playerSpine, playerDialogueAnimName, "玩家");

        if (npcDialogueAnims != null)
        {
            foreach (var cfg in npcDialogueAnims)
            {
                if (cfg == null || cfg.actor == null) continue;

                var spine = cfg.actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
                if (spine == null) continue;

                PlaySpineAnimation(spine, cfg.idleAnimName, cfg.actor.name);
            }
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

    // ============================================================
    // 过场结束
    // ============================================================
    private void OnCutsceneStopped(PlayableDirector obj)
    {
        isCutscenePlaying = false;
        _isInDialogue = false;
        _hasSavedCinematicPos = false;

        RestoreRootMotionComponents();

        StartCoroutine(FinishCutsceneWithFade());
        director.stopped -= OnCutsceneStopped;
    }

    // ============================================================
    // 结束流程
    // ============================================================
    private IEnumerator FinishCutsceneWithFade()
    {
        if (useFadeOut && fadeImage != null)
        {
            yield return StartCoroutine(FadeTo(1f, fadeDuration));

            SwitchBackToGameplay();

            if (useLetterbox)
            {
                if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0, 0);
                if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0, 0);
                if (letterboxRoot != null) letterboxRoot.SetActive(false);
            }

            yield return new WaitForSeconds(0.05f);

            if (hiddenDuringCutscene != null && hiddenDuringCutscene.Length > 0)
                StartCoroutine(FadeUI(1f, uiFadeDuration));

            yield return StartCoroutine(FadeTo(0f, fadeDuration));
        }
        else
        {
            SwitchBackToGameplay();

            if (useLetterbox)
                StartCoroutine(LetterboxAnimation(letterboxTargetHeight, 0f, letterboxDuration));

            if (hiddenDuringCutscene != null && hiddenDuringCutscene.Length > 0)
                yield return StartCoroutine(FadeUI(1f, uiFadeDuration));

            if (useLetterbox)
            {
                yield return new WaitForSeconds(letterboxDuration);
                if (letterboxRoot != null) letterboxRoot.SetActive(false);
            }
        }
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;

        float startAlpha = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
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
            elapsed += Time.deltaTime;
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

    private IEnumerator FadeUI(float targetAlpha, float duration)
    {
        if (hiddenDuringCutscene == null || hiddenDuringCutscene.Length == 0)
            yield break;

        float[] startAlphas = new float[hiddenDuringCutscene.Length];
        for (int i = 0; i < hiddenDuringCutscene.Length; i++)
        {
            if (hiddenDuringCutscene[i] != null)
                startAlphas[i] = hiddenDuringCutscene[i].alpha;
        }

        if (targetAlpha < 0.5f)
        {
            foreach (var cg in hiddenDuringCutscene)
            {
                if (cg != null)
                {
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < hiddenDuringCutscene.Length; i++)
            {
                if (hiddenDuringCutscene[i] != null)
                    hiddenDuringCutscene[i].alpha = Mathf.Lerp(startAlphas[i], targetAlpha, smoothT);
            }
            yield return null;
        }

        bool interactable = targetAlpha > 0.5f;
        foreach (var cg in hiddenDuringCutscene)
        {
            if (cg != null)
            {
                cg.alpha = targetAlpha;
                cg.interactable = interactable;
                cg.blocksRaycasts = interactable;
            }
        }
    }

    private void SwitchBackToGameplay()
    {
        float cinematicProgress = 0f;
        if (syncAnimationProgress)
            cinematicProgress = GetCinematicAnimationProgress();

        Vector3 finalPos = _lastCinematicPosition;
        Quaternion finalRot = _lastCinematicRotation;

        gameplayPlayer.transform.position = finalPos;
        gameplayPlayer.transform.rotation = finalRot;
>>>>>>> Stashed changes

        if (gameplayRigidbody != null)
        {
            gameplayRigidbody.position = finalPos;
            gameplayRigidbody.rotation = finalRot.eulerAngles.z;
            gameplayRigidbody.velocity = Vector2.zero;
            gameplayRigidbody.angularVelocity = 0f;
<<<<<<< Updated upstream
            // 恢复物理模拟
            gameplayRigidbody.simulated = true;
            Physics2D.SyncTransforms();
        }

        // 恢复渲染
        SetGameplayVisible(true);
        cinematicPlayer.SetActive(false);

        // ★ 恢复 Chronos Local Clock：将时间流速设回 1
        if (localClock != null)
            localClock.localTimeScale = 1f;

        // 恢复输入
        if (playerController != null)
            playerController.cutsceneMode = false;

        director.stopped -= OnCutsceneStopped;
=======
            gameplayRigidbody.bodyType = _originalBodyType;
            Physics2D.SyncTransforms();
        }

        SetGameplayVisible(true);
        if (cinematicPlayer != null) cinematicPlayer.SetActive(false);
        HideAllNpcActors();

        if (playerController != null)
        {
            playerController.ControlLocomotion = true;
            playerController.cutsceneMode = false;
        }

        if (syncAnimationProgress)
            SyncGameplayAnimationProgress(cinematicProgress);

        HandleCameraAfterCutscene();
        ResetCinematicPlayer();
    }

    private float GetCinematicAnimationProgress()
    {
        if (_playerSpine == null || _playerSpine.AnimationState == null)
            return 0f;

        var track = _playerSpine.AnimationState.GetCurrent(0);
        if (track == null || track.Animation == null || track.Animation.Duration <= 0f)
            return 0f;

        float progress = (track.TrackTime % track.Animation.Duration) / track.Animation.Duration;
        return Mathf.Clamp01(progress);
    }

    private void SyncGameplayAnimationProgress(float normalizedProgress)
    {
        if (gameplayAnimator == null) return;

        gameplayAnimator.Play(gameplayIdleStateName, 0, normalizedProgress);
        gameplayAnimator.Update(0f);
    }

    private void HandleCameraAfterCutscene()
    {
        if (mainCamera == null || cameraScript == null) return;

        Vector3 finalCamPos = mainCamera.transform.position;
        Quaternion finalCamRot = mainCamera.transform.rotation;

        cameraScript.SetTarget(gameplayPlayer.transform);

        mainCamera.transform.position = finalCamPos;
        mainCamera.transform.rotation = finalCamRot;

        cameraScript.enabled = true;
    }

    private void ResetCinematicPlayer()
    {
        if (cinematicPlayer == null) return;

        cinematicPlayer.transform.position = _cinematicInitialPosition;
        cinematicPlayer.transform.rotation = _cinematicInitialRotation;
        cinematicPlayer.transform.localScale = _cinematicInitialScale;

        if (cinematicVisual != null)
        {
            cinematicVisual.localPosition = Vector3.zero;
            cinematicVisual.localRotation = Quaternion.identity;
            cinematicVisual.localScale = Vector3.one;
        }

        if (_playerSpine != null)
        {
            _playerSpine.AnimationState.ClearTracks();
            _playerSpine.Skeleton.SetToSetupPose();
            _playerSpine.AnimationState.Apply(_playerSpine.Skeleton);
        }

        director.time = 0;
        director.Evaluate();

        cinematicPlayer.SetActive(false);
    }

    private void SetGameplayVisible(bool visible)
    {
        foreach (var r in gameplayRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    private void ShowAllNpcActors()
    {
        if (npcCinematicActors == null) return;
        foreach (var npc in npcCinematicActors)
        {
            if (npc != null) npc.SetActive(true);
        }
    }

    private void HideAllNpcActors()
    {
        if (npcCinematicActors == null) return;
        foreach (var npc in npcCinematicActors)
        {
            if (npc != null) npc.SetActive(false);
        }
>>>>>>> Stashed changes
    }
}