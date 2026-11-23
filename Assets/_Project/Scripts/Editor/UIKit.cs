using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NED2024.EditorTools
{
    /// <summary>Helpers that build uGUI hierarchies from code with a consistent dark horror look.</summary>
    public static class UIKit
    {
        public static Font BodyFont;
        public static Font TitleFont;
        public static Sprite Panel;      // sliced rounded rectangle
        public static Color Bone = new Color(0.93f, 0.89f, 0.76f);
        public static Color Blood = new Color(0.05f, 0.55f, 0.62f);   // accent colour (temple teal)
        public static Color Gold = new Color(1f, 0.78f, 0.3f);
        public static Color Ink = new Color(0.05f, 0.05f, 0.06f, 0.88f);

        public static void Init()
        {
            BodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/Rajdhani-Medium.ttf");
            if (BodyFont == null) BodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TitleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/Cinzel.ttf");
            if (TitleFont == null) TitleFont = BodyFont;
            Panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            // Scale with screen size so the layout looks the same at any resolution or aspect ratio.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
        }

        public static Image Box(string name, Transform parent, Color color, bool sliced = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sliced && Panel != null) { img.sprite = Panel; img.type = Image.Type.Sliced; }
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool title = false, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = title ? TitleFont : BodyFont;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button MenuButton(string name, Transform parent, string label, Vector2 size, AudioClip click)
        {
            var img = Box(name, parent, new Color(0.08f, 0.08f, 0.09f, 0.82f));
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.sizeDelta = size;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.5f, 1.2f, 0.6f, 1f);
            colors.pressedColor = new Color(0.9f, 0.7f, 0.3f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var text = Label("Label", img.transform, label.ToUpper(), 30, Bone, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            var sfx = img.gameObject.AddComponent<NED2024.Core.ButtonSfx>();
            sfx.ClickClip = click;
            return btn;
        }

        public static DefaultControls.Resources StandardResources()
        {
            return new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        public static Slider LabeledSlider(Transform parent, string label, float min, float max, Vector2 size)
        {
            var row = Rect(label + "Row", parent);
            row.sizeDelta = size;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;

            var text = Label("Label", row, label, 26, Bone);
            Anchor(text.rectTransform, new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);

            var go = DefaultControls.CreateSlider(StandardResources());
            go.name = label + "Slider";
            go.transform.SetParent(row, false);
            var rt = go.GetComponent<RectTransform>();
            Anchor(rt, new Vector2(0.42f, 0.2f), new Vector2(1, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 0); rt.offsetMax = new Vector2(0, 0);
            var slider = go.GetComponent<Slider>();
            slider.minValue = min; slider.maxValue = max;
            var bg = go.transform.Find("Background").GetComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.16f, 1f);
            var fill = go.transform.Find("Fill Area/Fill").GetComponent<Image>();
            fill.color = Blood;
            go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Bone;
            return slider;
        }

        public static Dropdown LabeledDropdown(Transform parent, string label, string[] options, Vector2 size)
        {
            var row = Rect(label + "Row", parent);
            row.sizeDelta = size;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;
            var text = Label("Label", row, label, 26, Bone);
            Anchor(text.rectTransform, new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);

            var go = DefaultControls.CreateDropdown(StandardResources());
            go.name = label + "Dropdown";
            go.transform.SetParent(row, false);
            var rt = go.GetComponent<RectTransform>();
            Anchor(rt, new Vector2(0.42f, 0.1f), new Vector2(1, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var dd = go.GetComponent<Dropdown>();
            dd.ClearOptions();
            dd.AddOptions(new System.Collections.Generic.List<string>(options));
            dd.captionText.fontSize = 24;
            dd.captionText.color = Bone;
            dd.itemText.fontSize = 24;
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 1f);
            return dd;
        }

        public static Toggle LabeledToggle(Transform parent, string label, Vector2 size)
        {
            var row = Rect(label + "Row", parent);
            row.sizeDelta = size;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;
            var text = Label("Label", row, label, 26, Bone);
            Anchor(text.rectTransform, new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);

            var go = DefaultControls.CreateToggle(StandardResources());
            go.name = label + "Toggle";
            go.transform.SetParent(row, false);
            var rt = go.GetComponent<RectTransform>();
            Anchor(rt, new Vector2(0.42f, 0.5f), new Vector2(0.42f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(40, 40));
            var lbl = go.GetComponentInChildren<Text>();
            if (lbl != null) Object.DestroyImmediate(lbl.gameObject);
            var bgRt = go.transform.Find("Background").GetComponent<RectTransform>();
            bgRt.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(40, 40);
            var check = go.transform.Find("Background/Checkmark").GetComponent<Image>();
            check.color = new Color(0.7f, 0.05f, 0.05f, 1f);
            return go.GetComponent<Toggle>();
        }
    }
}
