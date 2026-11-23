using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NED2024.Core;
using NED2024.Interactions;
using NED2024.Player;
using NED2024.UI;

namespace NED2024.EditorTools
{
    public static partial class GameBuilder
    {
        private static AudioClip ClickClip => AssetKit.Clip("click");

        // ------------------------------------------------------------ shared panels

        private static SettingsPanel BuildSettingsPanel(Transform parent)
        {
            var panel = UIKit.Box("SettingsPanel", parent, new Color(0.04f, 0.04f, 0.05f, 0.95f));
            panel.raycastTarget = true;
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 880));

            var title = UIKit.Label("Title", panel.transform, "SETTINGS", 64, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(0, 90));

            var list = UIKit.Rect("List", panel.transform);
            UIKit.Anchor(list, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            list.offsetMin = new Vector2(70, 120); list.offsetMax = new Vector2(-70, -120);
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 22; v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = false; v.childControlHeight = false; v.childForceExpandWidth = false; v.childForceExpandHeight = false;

            var rowSize = new Vector2(760, 56);
            var master = UIKit.LabeledSlider(list, "Master Volume", 0f, 1f, rowSize);
            var music = UIKit.LabeledSlider(list, "Music Volume", 0f, 1f, rowSize);
            var sfx = UIKit.LabeledSlider(list, "SFX Volume", 0f, 1f, rowSize);
            var sens = UIKit.LabeledSlider(list, "Mouse Sensitivity", 0.2f, 3f, rowSize);
            var quality = UIKit.LabeledDropdown(list, "Graphics Quality", new[] { "Low", "Medium", "High" }, rowSize);
            var resolution = UIKit.LabeledDropdown(list, "Resolution", new[] { "1920 x 1080" }, rowSize);
            var fullscreen = UIKit.LabeledToggle(list, "Fullscreen", rowSize);

            var back = UIKit.MenuButton("BackButton", panel.transform, "Back", new Vector2(320, 70), ClickClip);
            UIKit.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(320, 70));

            var sp = panel.gameObject.AddComponent<SettingsPanel>();
            Wire(sp, "masterSlider", master);
            Wire(sp, "musicSlider", music);
            Wire(sp, "sfxSlider", sfx);
            Wire(sp, "sensitivitySlider", sens);
            Wire(sp, "qualityDropdown", quality);
            Wire(sp, "resolutionDropdown", resolution);
            Wire(sp, "fullscreenToggle", fullscreen);
            Wire(sp, "backButton", back);
            Wire(sp, "qualityAssets", new System.Collections.Generic.List<Object>(GraphicsSetup.Tiers));
            return sp;
        }

        private static Image FullscreenPanel(string name, Transform parent, Color color)
        {
            var img = UIKit.Box(name, parent, color, false);
            UIKit.Stretch(img.rectTransform);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>Vertical column of menu buttons. Returns the buttons in order.</summary>
        private static RectTransform ButtonColumn(Transform parent, Vector2 pos, out Button[] buttons, params string[] labels)
        {
            var col = UIKit.Rect("Buttons", parent);
            UIKit.Anchor(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(520, labels.Length * 90f));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 16; v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = false; v.childControlHeight = false; v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                buttons[i] = UIKit.MenuButton(labels[i].Replace(" ", "") + "Button", col, labels[i], new Vector2(520, 74), ClickClip);
            return col;
        }

    }
}
