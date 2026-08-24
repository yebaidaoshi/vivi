using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 挂载到任意 UI 物体上，控制其文字在悬停、按下、禁用等状态下的颜色。
/// 不依赖 Button 组件，可与原 Button 共存。
/// </summary>
public class TextHoverColor : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("文字组件（必须手动指定）")]
    public Graphic textGraphic; // 可以是 Text 或 TextMeshProUGUI

    [Header("颜色状态")]
    public Color normalColor = Color.white;
    public Color highlightedColor = Color.yellow;
    public Color pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    public Color disabledColor = Color.gray;

    private Button button;
    private bool isPointerOver = false; // 用 bool 跟踪悬停状态

    private void Awake()
    {
        button = GetComponent<Button>();
        if (textGraphic == null)
            Debug.LogError($"[TextHoverColor] {gameObject.name} 缺少 Text Graphic 引用！");
    }

    private void OnEnable()
    {
        if (button != null && !button.interactable)
            SetColorImmediate(disabledColor);
        else
            SetColorImmediate(normalColor);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        isPointerOver = true;
        textGraphic.CrossFadeColor(highlightedColor, 0.1f, false, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        isPointerOver = false;
        textGraphic.CrossFadeColor(normalColor, 0.1f, false, true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        textGraphic.CrossFadeColor(pressedColor, 0.05f, false, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        // 根据悬停标志决定恢复哪个颜色
        if (isPointerOver)
            textGraphic.CrossFadeColor(highlightedColor, 0.1f, false, true);
        else
            textGraphic.CrossFadeColor(normalColor, 0.1f, false, true);
    }

    // 当按钮的 interactable 改变时，可外部调用
    public void UpdateInteractable(bool interactable)
    {
        if (interactable)
            SetColorImmediate(normalColor);
        else
            SetColorImmediate(disabledColor);
    }

    private void SetColorImmediate(Color color)
    {
        if (textGraphic != null)
            textGraphic.color = color;
    }
}