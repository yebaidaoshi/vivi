using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vivi.Slash
{
    /// <summary>
    /// 预览场景控制器：空格播放当前刀光，左右键切换，数字 1–9 直选。
    /// 打开 <c>Assets/VFX/Slash/Scenes/SlashPreview</c> 后按 Play。
    /// </summary>
    public class SlashPreviewController : MonoBehaviour
    {
        [SerializeField] private GameObject[] prefabs;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private int facing = 1;
        [SerializeField] private bool autoPlay;
        [SerializeField] private float autoInterval = 0.8f;

        private int _index;
        private float _autoTimer;
        private GameObject _last;
        private Text _titleText;
        private Text _hintText;

        private void Start()
        {
            CollectPrefabsIfEmpty();
            BuildHud();
            RefreshHud();
            if (prefabs != null && prefabs.Length > 0)
            {
                PlayCurrent();
            }
        }

        private void Update()
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                PlayCurrent();
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                _index = (_index + 1) % prefabs.Length;
                PlayCurrent();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                _index = (_index - 1 + prefabs.Length) % prefabs.Length;
                PlayCurrent();
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                facing = -facing;
                PlayCurrent();
            }

            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < prefabs.Length)
                {
                    _index = i;
                    PlayCurrent();
                }
            }

            if (autoPlay)
            {
                _autoTimer += Time.deltaTime;
                if (_autoTimer >= autoInterval)
                {
                    _autoTimer = 0f;
                    _index = (_index + 1) % prefabs.Length;
                    PlayCurrent();
                }
            }
        }

        private void PlayCurrent()
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return;
            }

            _index = Mathf.Clamp(_index, 0, prefabs.Length - 1);
            if (_last != null)
            {
                Destroy(_last);
            }

            GameObject prefab = prefabs[_index];
            if (prefab == null)
            {
                RefreshHud();
                return;
            }

            _last = SlashVfx.Play(prefab, transform.position + spawnOffset, facing);
            RefreshHud();
        }

        private void BuildHud()
        {
            var canvasGo = new GameObject("SlashPreviewHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var panel = CreateUiObject("Panel", canvasGo.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(36f, -36f);
            panelRt.sizeDelta = new Vector2(1180f, 220f);

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.05f, 0.07f, 0.72f);

            _titleText = CreateLabel(panel.transform, "Title", font, 42, FontStyle.Bold,
                new Vector2(28f, -20f), new Vector2(1124f, 64f));
            _hintText = CreateLabel(panel.transform, "Hint", font, 30, FontStyle.Normal,
                new Vector2(28f, -88f), new Vector2(1124f, 116f));
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateLabel(Transform parent, string name, Font font, int size,
            FontStyle style, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void RefreshHud()
        {
            if (_titleText == null || _hintText == null)
            {
                return;
            }

            string slashName = (prefabs != null && prefabs.Length > 0 && prefabs[_index] != null)
                ? prefabs[_index].name
                : "(no prefab)";
            int count = prefabs != null ? prefabs.Length : 0;
            string face = facing >= 0 ? "右" : "左";
            _titleText.text = $"刀光预览  {_index + 1}/{count}  {slashName}";
            _hintText.text =
                $"Space 重放    A / D 切换    F 翻转朝向（当前：{face}）    1–9 直选\n" +
                "预制体：Assets/VFX/Slash/Prefabs  — 可直接拖到 PlayerMelee";
        }

        private void CollectPrefabsIfEmpty()
        {
            if (prefabs != null && prefabs.Length > 0)
            {
                return;
            }

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/VFX/Slash/Prefabs" });
            var loaded = new System.Collections.Generic.List<GameObject>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    loaded.Add(prefab);
                }
            }

            loaded.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            prefabs = loaded.ToArray();
#endif
        }
    }
}
