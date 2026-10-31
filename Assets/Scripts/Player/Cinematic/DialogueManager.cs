using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject dialogueRoot;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public GameObject continueHint;
    public Button clickCatcher;

    [Header("对话框淡入淡出")]
    public CanvasGroup dialogueCanvasGroup;
    public float fadeDuration = 0.3f;

    [Header("背景变暗（2D Sprite）")]
    [Tooltip("把场景里的 DimBackground_Sprite 拖进来")]
    public SpriteRenderer dimBackgroundSprite;
    [Tooltip("目标变暗程度（0~1，建议0.5~0.8）")]
    public float dimAlpha = 0.6f;
    public float dimFadeSpeed = 3f;

    [Header("打字机效果")]
    public bool useTypewriter = true;
    public float typewriterSpeed = 0.03f;

    [Header("Live2D 功能")]
    public bool enableLive2D = true;
    public List<Live2DActor> live2DActors = new List<Live2DActor>();

    // 内部状态
    private bool isDialogueActive = false;
    private bool isTyping = false;
    private bool waitingForClick = false;
    private Queue<DialogueLine> dialogueQueue = new Queue<DialogueLine>();
    private Coroutine typewriterCoroutine;
    private Coroutine fadeCoroutine;
    private float currentLineStartTime;
    private string currentFullText = "";
    private System.Action onDialogueComplete;
    
    private float currentDimAlpha = 0f;

    void Start()
    {
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (continueHint != null) continueHint.SetActive(false);
        
        if (dialogueCanvasGroup != null)
            dialogueCanvasGroup.alpha = 0f;

        if (clickCatcher != null)
        {
            clickCatcher.onClick.RemoveListener(OnClick);
            clickCatcher.onClick.AddListener(OnClick);
        }

        if (dimBackgroundSprite != null)
        {
            Color c = dimBackgroundSprite.color;
            c.a = 0f;
            dimBackgroundSprite.color = c;
            dimBackgroundSprite.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (isDialogueActive && waitingForClick)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                if (isTyping) SkipTypewriter();
                else ShowNextLine();
            }
        }

        // 2D 黑幕平滑过渡
        if (dimBackgroundSprite != null)
        {
            float target = isDialogueActive ? dimAlpha : 0f;
            currentDimAlpha = Mathf.MoveTowards(currentDimAlpha, target, Time.unscaledDeltaTime * dimFadeSpeed);
            
            Color c = dimBackgroundSprite.color;
            c.a = currentDimAlpha;
            dimBackgroundSprite.color = c;

            if (currentDimAlpha <= 0.01f && dimBackgroundSprite.gameObject.activeSelf)
                dimBackgroundSprite.gameObject.SetActive(false);
            else if (currentDimAlpha > 0.01f && !dimBackgroundSprite.gameObject.activeSelf)
                dimBackgroundSprite.gameObject.SetActive(true);
        }
    }

    public void StartDialogue(List<DialogueLine> lines, System.Action onComplete = null)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        dialogueQueue.Clear();
        foreach (var line in lines) dialogueQueue.Enqueue(line);

        onDialogueComplete = onComplete;
        isDialogueActive = true;

        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (dialogueCanvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeDuration));
        }

        ShowNextLine();
    }

    private void ShowNextLine()
    {
        if (dialogueQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = dialogueQueue.Dequeue();
        currentLineStartTime = Time.unscaledTime;
        currentFullText = line.text;

        if (speakerNameText != null) speakerNameText.text = line.speakerName;
        if (continueHint != null) continueHint.SetActive(false);

        HandleLive2D(line);

        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);

        if (useTypewriter)
        {
            typewriterCoroutine = StartCoroutine(TypewriterRoutine(line.text));
        }
        else
        {
            if (dialogueText != null) dialogueText.text = line.text;
            isTyping = false;
            waitingForClick = true;
            if (continueHint != null) continueHint.SetActive(true);
        }
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        isTyping = true;
        waitingForClick = false;
        if (dialogueText != null) dialogueText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            if (dialogueText != null) dialogueText.text += fullText[i];
            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        isTyping = false;
        waitingForClick = true;
        if (continueHint != null) continueHint.SetActive(true);
    }

    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        if (dialogueText != null) dialogueText.text = currentFullText;
        isTyping = false;
        waitingForClick = true;
        if (continueHint != null) continueHint.SetActive(true);
    }

    public void OnClick()
    {
        if (!isDialogueActive) return;
        if (isTyping) { SkipTypewriter(); return; }
        if (Time.unscaledTime - currentLineStartTime < 0.1f) return;
        ShowNextLine();
    }

    private void EndDialogue()
    {
        isDialogueActive = false;
        waitingForClick = false;
        isTyping = false;

        // 对话彻底结束，所有人滑步退场
        if (enableLive2D)
        {
            foreach (var actor in live2DActors)
            {
                if (actor != null) actor.SetState(Live2DActor.ActorState.Hidden);
            }
        }

        if (dialogueCanvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(1f, 0f, fadeDuration, () => {
                if (dialogueRoot != null) dialogueRoot.SetActive(false);
                var callback = onDialogueComplete;
                onDialogueComplete = null;
                callback?.Invoke();
            }));
        }
        else
        {
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
            var callback = onDialogueComplete;
            onDialogueComplete = null;
            callback?.Invoke();
        }
    }

    private IEnumerator FadeCanvasGroup(float start, float end, float duration, System.Action onComplete = null)
    {
        float elapsed = 0f;
        dialogueCanvasGroup.alpha = start;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            dialogueCanvasGroup.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        dialogueCanvasGroup.alpha = end;
        onComplete?.Invoke();
    }

    private void HandleLive2D(DialogueLine line)
    {
        if (!enableLive2D) return;

        foreach (var actor in live2DActors)
        {
            if (actor == null) continue;

            if (actor.actorId == line.actorId)
            {
                // 谁说话，谁原地亮起（如果之前是隐藏的，直接原地淡入）
                actor.SetState(Live2DActor.ActorState.Talking);
                
                if (!string.IsNullOrEmpty(line.expression)) actor.SetExpression(line.expression);
                if (!string.IsNullOrEmpty(line.motion)) actor.PlayMotion(line.motion);
            }
            else
            {
                // 没说话的人，如果已经出场了，原地变暗（不移动）
                if (actor.currentState != Live2DActor.ActorState.Hidden)
                {
                    actor.SetState(Live2DActor.ActorState.Listening);
                }
            }
        }
    }

    public bool IsActive => isDialogueActive;
}