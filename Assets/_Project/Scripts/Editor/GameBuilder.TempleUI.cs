using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using NED2024.Core;
using NED2024.Interactions;
using NED2024.Temple;
using NED2024.UI;

namespace NED2024.EditorTools
{
    public static partial class GameBuilder
    {
        private static Font NumberFont => AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/Rajdhani-Bold.ttf");
        private static Color Teal => new Color(0.3f, 0.9f, 1f);
        private static Color GoldC => UIKit.Gold;

        private static Sprite IconSprite(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(GenTex + name + ".png"); }

        private static Text NumLabel(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = UIKit.Label(name, parent, text, size, color, align, false, FontStyle.Bold);
            if (NumberFont != null) t.font = NumberFont;
            return t;
        }

        // ------------------------------------------------------------ map texture (painted from the layout)

        private static Texture2D BuildMapTexture()
        {
            AssetKit.EnsureFolder(AssetKit.TexDir);
            const int ppt = 24;
            int size = TempleLayout.Size * ppt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bg = new Color(0.03f, 0.05f, 0.07f);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            Color RegionColor(Region r)
            {
                switch (r)
                {
                    case Region.Hall: return new Color(0.62f, 0.5f, 0.28f);
                    case Region.Rune: return new Color(0.28f, 0.48f, 0.62f);
                    case Region.Gallery: return new Color(0.6f, 0.28f, 0.24f);
                    case Region.Vault: case Region.SunVault: return new Color(0.6f, 0.46f, 0.2f);
                    case Region.Heart: return new Color(0.3f, 0.65f, 0.72f);
                    default: return new Color(0.34f, 0.32f, 0.26f);
                }
            }
            void Fill(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(size, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(size, x1); x++) px[y * size + x] = c;
            }

            for (int tx = 0; tx < TempleLayout.Size; tx++)
                for (int ty = 0; ty < TempleLayout.Size; ty++)
                {
                    if (!layout.Open[tx, ty]) continue;
                    Fill(tx * ppt, ty * ppt, (tx + 1) * ppt, (ty + 1) * ppt, RegionColor(layout.RegionOf[tx, ty]));
                }
            var wall = new Color(0.9f, 0.86f, 0.72f);
            for (int tx = 0; tx < TempleLayout.Size; tx++)
                for (int ty = 0; ty < TempleLayout.Size; ty++)
                {
                    if (!layout.Open[tx, ty]) continue;
                    int x0 = tx * ppt, y0 = ty * ppt, x1 = x0 + ppt, y1 = y0 + ppt;
                    if (!layout.IsOpen(tx + 1, ty)) Fill(x1 - 3, y0, x1, y1, wall);
                    if (!layout.IsOpen(tx - 1, ty)) Fill(x0, y0, x0 + 3, y1, wall);
                    if (!layout.IsOpen(tx, ty + 1)) Fill(x0, y1 - 3, x1, y1, wall);
                    if (!layout.IsOpen(tx, ty - 1) && !(tx == TempleLayout.EntranceTile.x && ty == 0)) Fill(x0, y0, x1, y0 + 3, wall);
                }
            // doors
            foreach (var d in layout.Doors)
            {
                Color c = d.Kind == DoorKind.SunDoor ? new Color(1f, 0.75f, 0.2f) : (d.Kind == DoorKind.MoonDoor ? new Color(0.7f, 0.85f, 1f) : (d.Kind == DoorKind.Entrance ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.3f)));
                int cx = d.Tile.x * ppt + ppt / 2, cy = d.Tile.y * ppt + ppt / 2;
                Fill(cx - 8, cy - 8, cx + 8, cy + 8, c);
            }
            tex.SetPixels(px);
            tex.Apply();
            string path = AssetKit.TexDir + "/temple_map.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false; ti.maxTextureSize = 2048; ti.sRGBTexture = true; ti.isReadable = true; ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite arrowSprite, diamondSprite, circleSprite;

        private static void BuildMapUi(RectTransform hudRoot, Text distanceText)
        {
            var mapTex = BuildMapTexture();
            circleSprite = MakeIconSprite("icon_circle", (x, y) => (1f - Mathf.Sqrt(x * x + y * y)) * 6f);
            arrowSprite = MakeIconSprite("icon_arrow", (x, y) => Mathf.Min((1f - Mathf.Abs(x) * (1.15f - (y + 1f) * 0.5f) * 1.6f) * 8f, (y + 0.95f) * 8f, (0.95f - y) * 8f));
            diamondSprite = MakeIconSprite("icon_diamond", (x, y) => (1f - (Mathf.Abs(x) + Mathf.Abs(y))) * 7f);

            var holder = UIKit.Rect("MapUI", hudRoot);
            UIKit.Stretch(holder);
            var mc = holder.gameObject.AddComponent<MapController>();

            // ---- minimap
            var frame = UIKit.Box("MinimapFrame", holder, new Color(0.02f, 0.04f, 0.06f, 0.85f));
            UIKit.Anchor(frame.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -250), new Vector2(300, 300));
            var maskImg = UIKit.Box("Mask", frame.transform, Color.white);
            UIKit.Stretch(maskImg.rectTransform, 6);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var raw = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(maskImg.transform, false);
            raw.texture = mapTex; raw.raycastTarget = false;
            UIKit.Stretch(raw.rectTransform);
            var dots = UIKit.Rect("Dots", maskImg.transform); UIKit.Stretch(dots);
            dots.anchorMin = dots.anchorMax = new Vector2(0.5f, 0.5f); dots.sizeDelta = Vector2.zero;
            var obj = MapIcon("Objective", maskImg.transform, diamondSprite, GoldC, 26f);
            var player = MapIcon("Player", maskImg.transform, arrowSprite, Teal, 28f);
            var north = UIKit.Label("North", frame.transform, "N", 20, UIKit.Bone, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Anchor(north.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(30, 26));
            var mapHint = UIKit.Label("Hint", frame.transform, "M  full map", 18, new Color(0.7f, 0.75f, 0.78f), TextAnchor.MiddleCenter);
            UIKit.Anchor(mapHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(200, 26));

            // ---- full map
            var big = UIKit.Box("BigMapPanel", holder, new Color(0f, 0f, 0f, 0.86f), false);
            UIKit.Stretch(big.rectTransform);
            big.raycastTarget = true;
            var title = UIKit.Label("Title", big.transform, "TEMPLE MAP", 60, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(900, 80));
            float mapH = 900f, mapW = mapH;
            var bigFrame = UIKit.Box("Frame", big.transform, new Color(0.5f, 0.6f, 0.65f, 1f));
            UIKit.Anchor(bigFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(mapW + 8, mapH + 8));
            var bigRaw = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            bigRaw.transform.SetParent(bigFrame.transform, false);
            bigRaw.texture = mapTex; bigRaw.raycastTarget = false;
            UIKit.Stretch(bigRaw.rectTransform, 4);
            var bigRect = bigRaw.rectTransform;
            var bigDots = UIKit.Rect("Dots", bigRect); bigDots.anchorMin = bigDots.anchorMax = new Vector2(0.5f, 0.5f); bigDots.sizeDelta = Vector2.zero;
            var bigObj = MapIcon("Objective", bigRect, diamondSprite, GoldC, 30f);
            var bigPlayer = MapIcon("Player", bigRect, arrowSprite, Teal, 32f);
            var legend = UIKit.Label("Legend", big.transform, "The map fills in as you explore.     Arrow = you     Diamond = objective     Red dot = sentinel hunting you     M or Esc = close", 22, new Color(0.8f, 0.85f, 0.88f), TextAnchor.MiddleCenter);
            UIKit.Anchor(legend.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(1800, 40));

            Wire(mc, "sourceMap", mapTex);
            Wire(mc, "miniMap", raw);
            Wire(mc, "miniPlayer", player);
            Wire(mc, "miniObjective", obj);
            Wire(mc, "miniDots", dots);
            Wire(mc, "bigPanel", big.gameObject);
            Wire(mc, "bigMapImage", bigRaw);
            Wire(mc, "bigMapRect", bigRect);
            Wire(mc, "bigPlayer", bigPlayer);
            Wire(mc, "bigObjective", bigObj);
            Wire(mc, "bigDots", bigDots);
            Wire(mc, "distanceText", distanceText);
            Wire(mc, "dotSprite", circleSprite);
            big.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ in-game UI

        private static void BuildGameUi(GameObject player, PlayerInteractor interactor, GameObject camerasRoot, Camera introCam, GameManager gm)
        {
            UIKit.Init();
            var hudCanvas = UIKit.CreateCanvas("HUD Canvas", 10);
            var hudRoot = UIKit.Rect("HUD", hudCanvas.transform);
            UIKit.Stretch(hudRoot);
            var hud = hudRoot.gameObject.AddComponent<HUDController>();
            hudRoot.gameObject.AddComponent<CanvasGroup>();

            var vignette = UIKit.Box("DamageVignette", hudRoot, new Color(1, 1, 1, 0), false);
            vignette.sprite = AssetKit.MakeVignetteSprite();
            vignette.color = new Color(1, 1, 1, 0);
            UIKit.Stretch(vignette.rectTransform);

            var panelColor = new Color(0.02f, 0.04f, 0.06f, 0.72f);

            // Health (bottom left)
            var hp = UIKit.Box("HealthPanel", hudRoot, panelColor);
            UIKit.Anchor(hp.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(520, 120));
            var hpLabel = UIKit.Label("Label", hp.transform, "VITALITY", 22, new Color(0.7f, 0.82f, 0.85f));
            UIKit.Anchor(hpLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -12), new Vector2(240, 30));
            var hpText = NumLabel("Value", hp.transform, "100", 40, Color.white, TextAnchor.MiddleRight);
            UIKit.Anchor(hpText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -6), new Vector2(200, 52));
            var barBg = UIKit.Box("BarBackground", hp.transform, new Color(0.04f, 0.1f, 0.12f, 0.95f));
            UIKit.Anchor(barBg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(-48, 34));
            var fill = UIKit.Box("Fill", barBg.transform, new Color(0.35f, 0.95f, 0.75f), false);
            fill.sprite = UIKit.Panel;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1f;
            UIKit.Stretch(fill.rectTransform, 4);

            // Score, relics and keys (top right)
            var sp = UIKit.Box("ScorePanel", hudRoot, panelColor);
            UIKit.Anchor(sp.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(560, 190));
            var scoreLabel = UIKit.Label("Label", sp.transform, "SCORE", 22, new Color(0.7f, 0.82f, 0.85f));
            UIKit.Anchor(scoreLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -10), new Vector2(200, 30));
            var score = NumLabel("Score", sp.transform, "0", 54, GoldC, TextAnchor.MiddleLeft);
            UIKit.Anchor(score.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -38), new Vector2(300, 70));
            var relicIcon = UIKit.Box("RelicIcon", sp.transform, GoldC, false); relicIcon.sprite = IconSprite("icon_relic"); relicIcon.preserveAspect = true;
            UIKit.Anchor(relicIcon.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 14), new Vector2(48, 48));
            var relicText = NumLabel("Relics", sp.transform, "0 / 5", 30, Color.white);
            UIKit.Anchor(relicText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 14), new Vector2(150, 48));
            var sunKey = UIKit.Box("SunKeyIcon", sp.transform, new Color(1f, 0.75f, 0.25f), false); sunKey.sprite = IconSprite("icon_key"); sunKey.preserveAspect = true;
            UIKit.Anchor(sunKey.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-96, 14), new Vector2(60, 48));
            var moonKey = UIKit.Box("MoonKeyIcon", sp.transform, new Color(0.7f, 0.88f, 1f), false); moonKey.sprite = IconSprite("icon_key"); moonKey.preserveAspect = true;
            UIKit.Anchor(moonKey.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 14), new Vector2(60, 48));

            // Objective (top left)
            var ob = UIKit.Box("ObjectivePanel", hudRoot, panelColor);
            UIKit.Anchor(ob.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(820, 110));
            var obLabel = UIKit.Label("Label", ob.transform, "OBJECTIVE", 22, Teal, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIKit.Anchor(obLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -10), new Vector2(300, 30));
            var obText = UIKit.Label("Text", ob.transform, "Explore the maze", 32, UIKit.Bone, TextAnchor.UpperLeft);
            UIKit.Anchor(obText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            obText.rectTransform.offsetMin = new Vector2(24, 8); obText.rectTransform.offsetMax = new Vector2(-24, -44);
            var distText = NumLabel("Distance", ob.transform, "?", 24, GoldC, TextAnchor.MiddleRight);
            UIKit.Anchor(distText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -8), new Vector2(160, 34));

            // Escape countdown (top centre)
            var esc = UIKit.Box("EscapePanel", hudRoot, new Color(0.1f, 0.03f, 0.02f, 0.78f));
            UIKit.Anchor(esc.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(760, 100));
            var escText = NumLabel("Text", esc.transform, "THE TEMPLE SEALS IN 02:30", 36, GoldC, TextAnchor.MiddleCenter);
            UIKit.Anchor(escText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(0, 52));
            var escBg = UIKit.Box("BarBg", esc.transform, new Color(0.12f, 0.06f, 0.04f, 0.95f));
            UIKit.Anchor(escBg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(-40, 20));
            var escFill = UIKit.Box("Fill", escBg.transform, new Color(1f, 0.6f, 0.2f), false);
            escFill.sprite = UIKit.Panel; escFill.type = Image.Type.Filled; escFill.fillMethod = Image.FillMethod.Horizontal; escFill.fillAmount = 1f;
            UIKit.Stretch(escFill.rectTransform, 3);

            // Message and prompt
            var message = UIKit.Label("Message", hudRoot, "", 32, Color.white, TextAnchor.UpperCenter);
            UIKit.Anchor(message.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(900, 170));
            var prompt = UIKit.Label("Prompt", hudRoot, "", 36, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Anchor(prompt.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(1100, 60));

            // Echo lens (bottom right): a ring that fills as the lens recharges
            var lensFrame = UIKit.Box("LensPanel", hudRoot, panelColor);
            UIKit.Anchor(lensFrame.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(330, 120));
            var lensIcon = UIKit.Box("Icon", lensFrame.transform, new Color(0.4f, 0.95f, 1f, 0.5f), false); lensIcon.sprite = IconSprite("icon_scan"); lensIcon.preserveAspect = true;
            UIKit.Anchor(lensIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(84, 84));
            var scanFill = UIKit.Box("Fill", lensFrame.transform, new Color(0.4f, 0.95f, 1f, 1f), false);
            scanFill.sprite = IconSprite("icon_scan"); scanFill.preserveAspect = true;
            scanFill.type = Image.Type.Filled; scanFill.fillMethod = Image.FillMethod.Radial360; scanFill.fillAmount = 1f;
            UIKit.Anchor(scanFill.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(84, 84));
            var scanText = NumLabel("Key", lensFrame.transform, "Q", 38, Color.white, TextAnchor.MiddleCenter);
            UIKit.Anchor(scanText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(112, 14), new Vector2(60, 50));
            var lensLabel = UIKit.Label("Label", lensFrame.transform, "ECHO LENS", 22, new Color(0.7f, 0.85f, 0.9f), TextAnchor.MiddleLeft);
            UIKit.Anchor(lensLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(112, -22), new Vector2(200, 30));

            // Direction arrow (shown while the lens is active)
            var arrowHolder = UIKit.Rect("NavArrow", hudRoot);
            UIKit.Anchor(arrowHolder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(150, 150));
            var arrowImg = UIKit.Box("Arrow", arrowHolder, new Color(0.4f, 0.95f, 1f, 0.9f), false); arrowImg.sprite = IconSprite("icon_nav"); arrowImg.preserveAspect = true;
            UIKit.Anchor(arrowImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            var ring = UIKit.Box("Ring", arrowHolder, new Color(0.4f, 0.95f, 1f, 0.25f), false); ring.sprite = circleSprite != null ? circleSprite : MakeIconSprite("icon_circle", (x, y) => (1f - Mathf.Sqrt(x * x + y * y)) * 6f);
            UIKit.Anchor(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            ring.transform.SetAsFirstSibling();

            BuildMapUi(hudRoot, distText);

            Wire(hud, "playerHealth", player.GetComponent<Health>());
            Wire(hud, "interactor", interactor);
            Wire(hud, "healthFill", fill);
            Wire(hud, "healthText", hpText);
            Wire(hud, "damageVignette", vignette);
            Wire(hud, "scoreText", score);
            Wire(hud, "relicText", relicText);
            Wire(hud, "sunKeyIcon", sunKey);
            Wire(hud, "moonKeyIcon", moonKey);
            Wire(hud, "scanFill", scanFill);
            Wire(hud, "scanText", scanText);
            Wire(hud, "navArrow", arrowHolder);
            Wire(hud, "objectiveText", obText);
            Wire(hud, "promptText", prompt);
            Wire(hud, "messageText", message);
            Wire(hud, "escapeText", escText);
            Wire(hud, "escapeBar", escFill);
            Wire(hud, "escapePanel", esc.gameObject);

            // ---- Screens: pause, game over, escape
            var screens = UIKit.CreateCanvas("Screens Canvas", 20);
            var uiCtrl = screens.gameObject.AddComponent<GameUIController>();

            var pause = FullscreenPanel("PausePanel", screens.transform, new Color(0, 0.02f, 0.04f, 0.78f));
            var pauseGroup = UIKit.Rect("PauseMain", pause.transform);
            UIKit.Stretch(pauseGroup);
            var pTitle = UIKit.Label("Title", pauseGroup, "PAUSED", 100, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(pTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1000, 160));
            ButtonColumn(pauseGroup, new Vector2(0, -20), out var pBtns, "Resume", "Settings", "Main Menu");
            var pSettings = BuildSettingsPanel(pause.transform);
            pSettings.gameObject.SetActive(false);

            var over = FullscreenPanel("GameOverPanel", screens.transform, new Color(0.07f, 0.02f, 0.02f, 0.86f));
            var oTitle = UIKit.Label("Title", over.transform, "LOST IN THE TEMPLE", 100, new Color(0.95f, 0.45f, 0.3f), TextAnchor.MiddleCenter, true);
            UIKit.Anchor(oTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1700, 170));
            var oReason = UIKit.Label("Reason", over.transform, "", 34, UIKit.Bone, TextAnchor.MiddleCenter);
            UIKit.Anchor(oReason.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1500, 50));
            var oScore = UIKit.Label("Score", over.transform, "Score 0", 44, new Color(0.95f, 0.78f, 0.4f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Anchor(oScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(1500, 70));
            ButtonColumn(over.transform, new Vector2(0, -80), out var oBtns, "Retry From Checkpoint", "Restart Mission", "Main Menu");

            var win = FullscreenPanel("CompletePanel", screens.transform, new Color(0.02f, 0.06f, 0.08f, 0.88f));
            var wTitle = UIKit.Label("Title", win.transform, "YOU ESCAPED", 100, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter, true);
            UIKit.Anchor(wTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1700, 150));
            var wStory = UIKit.Label("Story", win.transform, "The temple seals itself behind you, forever.\nThe Heart of Eternity glows in your hands.", 34, UIKit.Bone, TextAnchor.MiddleCenter);
            UIKit.Anchor(wStory.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1300, 110));
            var wStats = UIKit.Label("Stats", win.transform, "Time 00:00", 38, new Color(0.9f, 0.8f, 0.5f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Anchor(wStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1500, 60));
            var wScore = UIKit.Label("Score", win.transform, "SCORE 0", 46, new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Anchor(wScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -75), new Vector2(1800, 120));
            var wBtn = UIKit.MenuButton("MenuButton", win.transform, "Main Menu", new Vector2(420, 80), ClickClip);
            UIKit.Anchor(wBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(420, 80));

            Wire(uiCtrl, "pausePanel", pause.gameObject);
            Wire(uiCtrl, "pauseMainGroup", pauseGroup.gameObject);
            Wire(uiCtrl, "settingsPanel", pSettings.gameObject);
            Wire(uiCtrl, "gameOverPanel", over.gameObject);
            Wire(uiCtrl, "completePanel", win.gameObject);
            Wire(uiCtrl, "resumeButton", pBtns[0]);
            Wire(uiCtrl, "pauseSettingsButton", pBtns[1]);
            Wire(uiCtrl, "pauseMenuButton", pBtns[2]);
            Wire(uiCtrl, "retryButton", oBtns[0]);
            Wire(uiCtrl, "restartButton", oBtns[1]);
            Wire(uiCtrl, "gameOverMenuButton", oBtns[2]);
            Wire(uiCtrl, "gameOverReasonText", oReason);
            Wire(uiCtrl, "gameOverScoreText", oScore);
            Wire(uiCtrl, "completeMenuButton", wBtn);
            Wire(uiCtrl, "completeStatsText", wStats);
            Wire(uiCtrl, "completeScoreText", wScore);
            Wire(uiCtrl, "settings", pSettings);
            pause.gameObject.SetActive(false);
            over.gameObject.SetActive(false);
            win.gameObject.SetActive(false);

            // ---- Intro cinematic
            var introCanvas = UIKit.CreateCanvas("Intro Canvas", 30);
            var overlay = FullscreenPanel("IntroOverlay", introCanvas.transform, Color.black);
            var cg = overlay.gameObject.AddComponent<CanvasGroup>();
            var iTitle = UIKit.Label("Title", overlay.transform, "HEART OF ETERNITY", 110, new Color(0.95f, 0.85f, 0.6f), TextAnchor.MiddleCenter, true);
            UIKit.Anchor(iTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800, 300));
            var iLine = UIKit.Label("Line", overlay.transform, "", 44, UIKit.Bone, TextAnchor.MiddleCenter);
            UIKit.Anchor(iLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 200));
            var skip = UIKit.Label("Skip", overlay.transform, "SPACE / CLICK to continue", 22, new Color(0.5f, 0.55f, 0.58f), TextAnchor.MiddleCenter);
            UIKit.Anchor(skip.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(800, 40));
            var intro = overlay.gameObject.AddComponent<IntroSequence>();
            Wire(intro, "overlay", cg);
            Wire(intro, "titleText", iTitle);
            Wire(intro, "lineText", iLine);
            Wire(intro, "introCameraRig", introCam.transform);
            Wire(intro, "introCamera", introCam);
            Wire(intro, "playerCameras", camerasRoot);
            Wire(intro, "stingClip", AssetKit.Clip("rumble"));
        }

        // ------------------------------------------------------------ main menu scene

        private static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            UIKit.Init();
            BuildPropMaterials();
            layout = TempleLayout.Shared;

            // Backdrop: the great portal at night with torches, ferns and the explorer
            var back = new GameObject("Backdrop").transform;
            var acc = new MeshAcc();
            acc.Quad(new Vector3(-60, 0, -40), new Vector3(60, 0, -40), new Vector3(60, 0, 40), new Vector3(-60, 0, 40), Vector3.up, 6f);
            var ground = new GameObject("Ground"); ground.transform.SetParent(back, false);
            var gm = acc.ToMesh("MenuGround"); SaveMeshAsset(gm, "MenuGround");
            ground.AddComponent<MeshFilter>().sharedMesh = gm; ground.AddComponent<MeshRenderer>().sharedMaterial = matGround;
            void Block(string n, Vector3 centre, Vector3 size, Material m)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = n; b.transform.SetParent(back, false); b.transform.position = centre; b.transform.localScale = size; b.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            Block("PillarL", new Vector3(-4.5f, 6f, 14f), new Vector3(3f, 12f, 3f), matWallHall);
            Block("PillarR", new Vector3(4.5f, 6f, 14f), new Vector3(3f, 12f, 3f), matWallHall);
            Block("Lintel", new Vector3(0f, 10.2f, 14f), new Vector3(12.5f, 3.6f, 3.4f), matWallHall);
            Block("Wall", new Vector3(0f, 8f, 17f), new Vector3(60f, 16f, 4f), matWallMaze);
            // The Heart of Eternity floats in the doorway as a teaser
            var heartGo = new GameObject("MenuHeart").transform; heartGo.SetParent(back, false); heartGo.position = new Vector3(0f, 3.3f, 12.6f);
            heartGo.gameObject.AddComponent<Float>();
            MeshObject("Gem", heartGo, MakeCrystal("Crystal_Heart", 1.7f, 0.55f, 8, 0.5f), AssetKit.Emissive("Glow_Heart", new Color(0.15f, 0.7f, 1f), 1.7f), Vector3.zero, Vector3.one * 1.6f).AddComponent<Spin>();
            for (int i = 0; i < 2; i++)
            {
                var rr = MeshObject("Orbit" + i, heartGo, MakeTorus("Torus_Orbit" + i, 0.85f + i * 0.2f, 0.022f, 48, 8), glowAmber, Vector3.zero, Vector3.one * 1.6f);
                rr.transform.localRotation = Quaternion.Euler(60f * (i + 1), 25f * i, 0f);
                Wire(rr.AddComponent<Spin>(), "degreesPerSecond", new Vector3(0f, i == 0 ? 55f : -40f, 0f));
            }
            FlatQuad("Halo", back, heartGo.position, Quaternion.identity, 9f, ParticleMaterial("FX_HeartHalo", new Color(0.4f, 0.9f, 1f, 0.5f), true, SoftDot())).AddComponent<Billboard>();
            MakeLight("HeartLight", heartGo.position, new Color(0.5f, 0.92f, 1f), 9f, 22f, back);
            MakeLight("PortalLight", new Vector3(0f, 3.5f, 11f), new Color(1f, 0.7f, 0.35f), 9f, 22f, back);
            MakeLight("HeroLight", new Vector3(-1f, 3.2f, 0.5f), new Color(0.75f, 0.85f, 1f), 5f, 10f, back);
            foreach (float x in new[] { -7f, 7f })
            {
                Prim(PrimitiveType.Cylinder, "TorchPost", back, new Vector3(x, 1.2f, 6f), new Vector3(0.2f, 1.2f, 0.2f), matMetal);
                var flame = MakeLoopParticles("Flame", back, fxFlame, new Color(1f, 0.72f, 0.3f, 0.95f), new Color(1f, 0.2f, 0.05f, 0f), 0.8f, 26f, 1f, 0.8f, 0.15f, -0.2f, true, 10f);
                flame.transform.position = new Vector3(x, 2.5f, 6f); flame.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                MakeLight("TorchLight", new Vector3(x, 3f, 6f), new Color(1f, 0.62f, 0.28f), 6f, 16f, back, true);
            }
            var rng = new System.Random(9);
            for (int i = 0; i < 20; i++)
            {
                var p = new Vector3(-18f + (float)rng.NextDouble() * 36f, 0f, 2f + (float)rng.NextDouble() * 10f);
                if (Mathf.Abs(p.x) < 3f && p.z > 7f) continue;
                PlaceModel("fern_02", p, (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble(), back, ModelMat("fern_02", new Color(0.7f, 0.95f, 0.7f)), false);
            }
            foreach (var bp in new[] { new Vector3(-9f, 0f, 9f), new Vector3(10f, 0f, 8f), new Vector3(-14f, 0f, 4f) })
                PlaceModel("boulder_01", bp, 30f, 2.4f, back, ModelMat("boulder_01", new Color(0.7f, 0.78f, 0.65f)), false);
            var hero = BuildHeroModel("MenuExplorer", back, ExplorerLook, AssetKit.BuildExplorerController());
            hero.transform.position = new Vector3(1.6f, 0f, 3.0f);
            hero.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            AddExplorerGear(hero.GetComponent<Animator>());

            // Lighting
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(AssetKit.MatDir + "/TempleSky.mat");
            if (skyMat == null) { skyMat = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(skyMat, AssetKit.MatDir + "/TempleSky.mat"); }
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.12f, 0.15f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.016f; RenderSettings.fogColor = new Color(0.04f, 0.07f, 0.08f);
            var moon = new GameObject("Moonlight");
            moon.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var ml = moon.AddComponent<Light>();
            ml.type = LightType.Directional; ml.color = new Color(0.5f, 0.72f, 1f); ml.intensity = 0.9f; ml.shadows = LightShadows.Soft;
            RenderSettings.sun = ml;
            var vol = new GameObject("PostProcessing").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = postProfile;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-5.2f, 1.9f, -2.0f);
            camGo.transform.rotation = Quaternion.Euler(-2f, 4f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<NED2024.Environment.CameraSway>();

            var wind = new GameObject("Ambience");
            var src = wind.AddComponent<AudioSource>();
            src.clip = AssetKit.Clip("ambience"); src.playOnAwake = true; src.loop = true; src.volume = 0.6f;

            // ---- UI
            var canvas = UIKit.CreateCanvas("Menu Canvas", 10);
            var menu = canvas.gameObject.AddComponent<MainMenuController>();

            var shade = UIKit.Box("LeftShade", canvas.transform, new Color(0, 0.02f, 0.04f, 0.6f), false);
            UIKit.Anchor(shade.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            shade.rectTransform.offsetMin = Vector2.zero; shade.rectTransform.offsetMax = Vector2.zero;

            var main = UIKit.Rect("MainPanel", canvas.transform);
            UIKit.Stretch(main);
            var title = UIKit.Label("Title", main, "HEART OF\nETERNITY", 112, new Color(0.96f, 0.86f, 0.62f), TextAnchor.UpperLeft, true);
            UIKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -60), new Vector2(1100, 290));
            var sub = UIKit.Label("Subtitle", main, "THE LOST TEMPLE ESCAPE", 40, Teal, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIKit.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -330), new Vector2(900, 60));
            var colRoot = UIKit.Rect("ButtonColumn", main);
            UIKit.Anchor(colRoot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, -150), new Vector2(520, 7 * 90f));
            var v = colRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = false; v.childControlHeight = false; v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            string[] labels = { "New Game", "Continue", "High Scores", "Settings", "Controls", "Credits", "Exit Game" };
            var btns = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++) btns[i] = UIKit.MenuButton(labels[i].Replace(" ", "") + "Button", colRoot, labels[i], new Vector2(460, 70), ClickClip);

            var footer = UIKit.Label("Footer", main, "Muhammad Zahoor Ul Haq   |   Roll No. CT-24148   |   NED University of Engineering & Technology, Karachi   |   Game Programming", 22, new Color(0.65f, 0.7f, 0.72f), TextAnchor.MiddleLeft);
            UIKit.Anchor(footer.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 24), new Vector2(1700, 36));

            // Player profile: name field (saved between runs)
            var pLabel = UIKit.Label("PlayerLabel", main, "EXPLORER NAME", 26, Teal, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIKit.Anchor(pLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(640, 120), new Vector2(400, 36));
            var nameGo = DefaultControls.CreateInputField(UIKit.StandardResources());
            nameGo.name = "NameInput";
            nameGo.transform.SetParent(main, false);
            UIKit.Anchor(nameGo.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(640, 62), new Vector2(420, 60));
            var nameInput = nameGo.GetComponent<InputField>();
            nameInput.textComponent.font = UIKit.BodyFont; nameInput.textComponent.fontSize = 32; nameInput.textComponent.color = Color.black;
            nameInput.placeholder.GetComponent<Text>().font = UIKit.BodyFont; nameInput.placeholder.GetComponent<Text>().fontSize = 30;
            nameInput.placeholder.GetComponent<Text>().text = "Type your name";
            var nameHint = UIKit.Label("NameHint", main, "Your name and best scores are saved.", 22, new Color(0.65f, 0.7f, 0.72f), TextAnchor.MiddleLeft);
            UIKit.Anchor(nameHint.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(640, 8), new Vector2(520, 34));

            // High scores
            var scores = FullscreenPanel("ScoresPanel", canvas.transform, new Color(0.02f, 0.03f, 0.05f, 0.95f));
            var sTitle = UIKit.Label("Title", scores.transform, "HIGH SCORES", 80, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(sTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1200, 110));
            var sStats = UIKit.Label("Stats", scores.transform, "", 34, GoldC, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Anchor(sStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 110));
            var sBody = UIKit.Label("Table", scores.transform, "", 38, UIKit.Bone, TextAnchor.UpperCenter);
            UIKit.Anchor(sBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1500, 460));
            var sBack = UIKit.MenuButton("BackButton", scores.transform, "Back", new Vector2(320, 70), ClickClip);
            UIKit.Anchor(sBack.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(320, 70));
            scores.gameObject.SetActive(false);

            var settings = BuildSettingsPanel(canvas.transform);
            settings.gameObject.SetActive(false);

            // Controls
            var controls = FullscreenPanel("ControlsPanel", canvas.transform, new Color(0.02f, 0.03f, 0.05f, 0.95f));
            var cTitle = UIKit.Label("Title", controls.transform, "CONTROLS", 80, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(cTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1000, 110));
            string[] keys = { "W A S D", "Mouse", "Left Shift", "Space", "E", "Q", "F", "M", "Esc" };
            string[] acts = { "Move", "Look / orbit the camera", "Sprint (louder: sentinels can hear it)", "Jump", "Use: doors, rune pedestals, secret walls, the Heart", "Echo Lens: reveals hidden carvings, points to the goal, stuns sentinels", "Lantern on / off", "Open / close the temple map", "Pause" };
            var cKeys = UIKit.Label("Keys", controls.transform, string.Join("\n", keys), 36, GoldC, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIKit.Anchor(cKeys.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-800, 0), new Vector2(380, 560));
            var cActs = UIKit.Label("Actions", controls.transform, string.Join("\n", acts), 36, UIKit.Bone, TextAnchor.UpperLeft);
            UIKit.Anchor(cActs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-400, 0), new Vector2(1300, 560));
            var cGoal = UIKit.Label("Goal", controls.transform, "Goal: find the Sun Key, wake the four runes in the right order, cross the Trap Gallery for the Moon Key, take the Heart of Eternity and run back to the entrance before the temple seals itself.", 28, new Color(0.75f, 0.85f, 0.88f), TextAnchor.MiddleCenter);
            UIKit.Anchor(cGoal.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1500, 100));
            var cBack = UIKit.MenuButton("BackButton", controls.transform, "Back", new Vector2(320, 70), ClickClip);
            UIKit.Anchor(cBack.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(320, 70));
            controls.gameObject.SetActive(false);

            // Credits
            var credits = FullscreenPanel("CreditsPanel", canvas.transform, new Color(0.02f, 0.03f, 0.05f, 0.95f));
            var crTitle = UIKit.Label("Title", credits.transform, "CREDITS", 80, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(crTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(1000, 110));
            string cr =
                "Game design and programming:  Muhammad Zahoor Ul Haq   (Roll No. CT-24148)\n" +
                "NED University of Engineering & Technology, Karachi - Game Programming\n\n" +
                "Stone, moss and paving textures, statues, braziers, plants and rocks: Poly Haven (CC0)\n" +
                "Explorer character: custom Blender model, animations from the Quaternius Universal Animation Library (CC0)\n" +
                "Fonts: Cinzel and Rajdhani (SIL Open Font License)\n" +
                "Sound effects and music: synthesised for this game\n\n" +
                "Built with Unity 6, URP, Cinemachine and AI Navigation.\n" +
                "Full details are in Documentation/ASSET_CREDITS.md";
            var crBody = UIKit.Label("Body", credits.transform, cr, 32, UIKit.Bone, TextAnchor.UpperCenter);
            UIKit.Anchor(crBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1700, 620));
            var crBack = UIKit.MenuButton("BackButton", credits.transform, "Back", new Vector2(320, 70), ClickClip);
            UIKit.Anchor(crBack.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(320, 70));
            credits.gameObject.SetActive(false);

            // Loading screen
            var load = FullscreenPanel("LoadingScreen", canvas.transform, new Color(0.01f, 0.015f, 0.02f, 1f));
            var lTitle = UIKit.Label("Title", load.transform, "ENTERING THE TEMPLE", 80, UIKit.Bone, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(lTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1500, 140));
            var bar = DefaultControls.CreateSlider(UIKit.StandardResources());
            bar.name = "ProgressBar";
            bar.transform.SetParent(load.transform, false);
            var barRt = bar.GetComponent<RectTransform>();
            UIKit.Anchor(barRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 28));
            var slider = bar.GetComponent<Slider>();
            slider.interactable = false; slider.value = 0f;
            Object.DestroyImmediate(bar.transform.Find("Handle Slide Area").gameObject);
            slider.handleRect = null;
            bar.transform.Find("Background").GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 1f);
            bar.transform.Find("Fill Area/Fill").GetComponent<Image>().color = GoldC;
            var pct = UIKit.Label("Percent", load.transform, "0%", 36, UIKit.Bone, TextAnchor.MiddleCenter);
            UIKit.Anchor(pct.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(300, 50));
            var tip = UIKit.Label("Tip", load.transform, "", 30, new Color(0.75f, 0.82f, 0.85f), TextAnchor.MiddleCenter, false, FontStyle.Italic);
            UIKit.Anchor(tip.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1500, 100));
            var loading = load.gameObject.AddComponent<LoadingScreen>();
            Wire(loading, "progressBar", slider);
            Wire(loading, "percentText", pct);
            Wire(loading, "tipText", tip);
            load.gameObject.SetActive(false);

            Wire(menu, "mainPanel", main.gameObject);
            Wire(menu, "settingsPanel", settings.gameObject);
            Wire(menu, "controlsPanel", controls.gameObject);
            Wire(menu, "creditsPanel", credits.gameObject);
            Wire(menu, "scoresPanel", scores.gameObject);
            Wire(menu, "newGameButton", btns[0]);
            Wire(menu, "continueButton", btns[1]);
            Wire(menu, "scoresButton", btns[2]);
            Wire(menu, "settingsButton", btns[3]);
            Wire(menu, "controlsButton", btns[4]);
            Wire(menu, "creditsButton", btns[5]);
            Wire(menu, "exitButton", btns[6]);
            Wire(menu, "controlsBackButton", cBack);
            Wire(menu, "creditsBackButton", crBack);
            Wire(menu, "scoresBackButton", sBack);
            Wire(menu, "nameInput", nameInput);
            Wire(menu, "scoresText", sBody);
            Wire(menu, "statsText", sStats);
            Wire(menu, "settings", settings);
            Wire(menu, "loading", loading);
            Wire(menu, "menuMusic", AssetKit.Clip("music_menu"));

            UIKit.CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log("Menu scene built: " + MenuScenePath);
        }
    }
}
