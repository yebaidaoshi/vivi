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

    [Header("打字机效果")]
    public bool useTypewriter = true;
    public float typewriterSpeed = 0.03f;

    // 内部状态
    private bool isDialogueActive = false;
    private bool isTyping = false;
    private bool waitingForClick = false;
    private Queue<DialogueLine> dialogueQueue = new Queue<DialogueLine>();
    private Coroutine typewriterCoroutine;
    private float currentLineStartTime;
    private string currentFullText = "";

    private System.Action onDialogueComplete;

    void Start()
    {
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (continueHint != null) continueHint.SetActive(false);

        if (clickCatcher != null)
        {
            clickCatcher.onClick.RemoveListener(OnClick);
            clickCatcher.onClick.AddListener(OnClick);
        }
        else
        {
            Debug.LogWarning("[DialogueManager] ClickCatcher 未绑定！");
        }
    }

    void Update()
    {
        if (isDialogueActive && waitingForClick)
        {
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0))
            {
                if (isTyping)
                    SkipTypewriter();
                else
                    ShowNextLine();
            }
        }
    }

    /// <summary>
    /// 开始一段对话序列。
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, System.Action onComplete = null)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        dialogueQueue.Clear();
        foreach (var line in lines)
            dialogueQueue.Enqueue(line);

        onDialogueComplete = onComplete;
        isDialogueActive = true;

        if (dialogueRoot != null) dialogueRoot.SetActive(true);

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

        if (speakerNameText != null)
            speakerNameText.text = line.speakerName;

        if (continueHint != null)
            continueHint.SetActive(false);

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

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

        if (dialogueText != null)
            dialogueText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text += fullText[i];

            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        isTyping = false;
        waitingForClick = true;
        if (continueHint != null) continueHint.SetActive(true);
    }

    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        if (dialogueText != null)
            dialogueText.text = currentFullText;

        isTyping = false;
        waitingForClick = true;
        if (continueHint != null) continueHint.SetActive(true);
    }

    public void OnClick()
    {
        if (!isDialogueActive) return;

        if (isTyping)
        {
            SkipTypewriter();
            return;
        }

        if (Time.unscaledTime - currentLineStartTime < 0.1f)
            return;

        ShowNextLine();
    }

    private void EndDialogue()
    {
        isDialogueActive = false;
        waitingForClick = false;
        isTyping = false;

        if (dialogueRoot != null) dialogueRoot.SetActive(false);

        var callback = onDialogueComplete;
        onDialogueComplete = null;
        callback?.Invoke();
    }

    public bool IsActive => isDialogueActive;
}