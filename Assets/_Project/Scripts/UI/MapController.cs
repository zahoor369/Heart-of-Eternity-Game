using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NED2024.Core;
using NED2024.Temple;

namespace NED2024.UI
{
    /// <summary>
    /// Minimap (corner, follows the explorer) and full map (M key). The temple starts unexplored: tiles appear on the map as the
    /// explorer walks through them. The objective marker is only shown once its place has been seen, or while the Echo Lens is active.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        private const int PixelsPerTile = 24;

        [Header("Map data")]
        [SerializeField] private Texture2D sourceMap;               // the finished map painted by the builder (readable)
        [SerializeField] private Vector4 worldBounds = new Vector4(-62f, -2f, 62f, 122f);   // minX, minZ, maxX, maxZ
        [SerializeField] private float miniRadius = 24f;            // metres from the player to the minimap edge

        [Header("Minimap")]
        [SerializeField] private RawImage miniMap;
        [SerializeField] private RectTransform miniPlayer;
        [SerializeField] private RectTransform miniObjective;
        [SerializeField] private RectTransform miniDots;

        [Header("Full map")]
        [SerializeField] private GameObject bigPanel;
        [SerializeField] private RawImage bigMapImage;
        [SerializeField] private RectTransform bigMapRect;
        [SerializeField] private RectTransform bigPlayer;
        [SerializeField] private RectTransform bigObjective;
        [SerializeField] private RectTransform bigDots;

        [Header("Text")]
        [SerializeField] private Text distanceText;
        [SerializeField] private Sprite dotSprite;

        private TempleLayout layout;
        private Texture2D shown;
        private Color32[] sourcePixels, shownPixels;
        private bool[,] explored;
        private bool dirty;
        private Transform player;
        private SentinelAI[] sentinels = new SentinelAI[0];
        private float nextScan, nextReveal;
        private readonly List<Image> miniDotPool = new List<Image>();
        private readonly List<Image> bigDotPool = new List<Image>();

        public bool BigMapOpen => bigPanel != null && bigPanel.activeSelf;

        public bool IsExplored(Vector3 world)
        {
            var t = TempleLayout.WorldToTile(world);
            return t.x >= 0 && t.y >= 0 && t.x < TempleLayout.Size && t.y < TempleLayout.Size && explored[t.x, t.y];
        }

        private void Start()
        {
            layout = TempleLayout.Shared;
            explored = new bool[TempleLayout.Size, TempleLayout.Size];
            int size = TempleLayout.Size * PixelsPerTile;
            shown = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            sourcePixels = sourceMap.GetPixels32();
            shownPixels = new Color32[sourcePixels.Length];
            var dark = new Color32(8, 12, 16, 255);
            for (int i = 0; i < shownPixels.Length; i++) shownPixels[i] = dark;
            shown.SetPixels32(shownPixels); shown.Apply();
            if (miniMap != null) miniMap.texture = shown;
            if (bigMapImage != null) bigMapImage.texture = shown;

            var gm = GameManager.Instance;
            if (gm != null) gm.StateChanged += OnStateChanged;
            if (bigPanel != null) bigPanel.SetActive(false);
            GameManager.CloseOverlay = TryCloseBigMap;
        }

        private bool TryCloseBigMap()
        {
            if (!BigMapOpen) return false;
            bigPanel.SetActive(false);
            return true;
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.StateChanged -= OnStateChanged;
            if (shown != null) Destroy(shown);
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.Playing && bigPanel != null) bigPanel.SetActive(false);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (player == null) player = gm.PlayerTransform;
            if (player == null || shown == null) return;

            if (gm.State == GameState.Playing && GameInput.MapDown && bigPanel != null) bigPanel.SetActive(!bigPanel.activeSelf);

            if (Time.time >= nextReveal) { nextReveal = Time.time + 0.2f; Reveal(); }
            if (Time.time >= nextScan) { nextScan = Time.time + 0.5f; sentinels = FindObjectsByType<SentinelAI>(FindObjectsSortMode.None); }
            if (dirty) { shown.SetPixels32(shownPixels); shown.Apply(); dirty = false; }

            UpdateMini();
            if (BigMapOpen) UpdateBig();

            var tracker = ObjectiveTracker.Instance;
            if (distanceText != null)
            {
                if (tracker != null && tracker.Current != null && ObjectiveVisible(tracker.Current.position))
                {
                    float d = Vector3.Distance(new Vector3(player.position.x, 0, player.position.z), new Vector3(tracker.Current.position.x, 0, tracker.Current.position.z));
                    distanceText.text = Mathf.RoundToInt(d) + " m";
                }
                else distanceText.text = "?";
            }
        }

        private bool ObjectiveVisible(Vector3 world)
        {
            return IsExplored(world) || (EchoLens.Instance != null && EchoLens.Instance.Active);
        }

        // ---------------------------------------------------------------- fog of war

        private void Reveal()
        {
            var here = TempleLayout.WorldToTile(player.position);
            if (!layout.IsOpen(here)) return;
            var dist = layout.Distances(here);
            foreach (var kv in dist)
            {
                if (kv.Value > 3) continue;
                var t = kv.Key;
                // the tile and the walls around it
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        RevealTile(t.x + dx, t.y + dy);
            }
        }

        private void RevealTile(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= TempleLayout.Size || ty >= TempleLayout.Size || explored[tx, ty]) return;
            explored[tx, ty] = true;
            int size = TempleLayout.Size * PixelsPerTile;
            for (int y = 0; y < PixelsPerTile; y++)
            {
                int row = (ty * PixelsPerTile + y) * size + tx * PixelsPerTile;
                for (int x = 0; x < PixelsPerTile; x++) shownPixels[row + x] = sourcePixels[row + x];
            }
            dirty = true;
        }

        // ---------------------------------------------------------------- drawing

        private Vector2 WorldToUV(Vector3 w)
        {
            return new Vector2((w.x - worldBounds.x) / (worldBounds.z - worldBounds.x), (w.z - worldBounds.y) / (worldBounds.w - worldBounds.y));
        }

        private void UpdateMini()
        {
            if (miniMap == null) return;
            float worldW = worldBounds.z - worldBounds.x;
            float worldH = worldBounds.w - worldBounds.y;
            float span = miniRadius * 2f;
            miniMap.uvRect = new Rect((player.position.x - miniRadius - worldBounds.x) / worldW,
                                      (player.position.z - miniRadius - worldBounds.y) / worldH,
                                      span / worldW, span / worldH);
            if (miniPlayer != null) miniPlayer.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);

            float half = miniMap.rectTransform.rect.width * 0.5f;
            var tracker = ObjectiveTracker.Instance;
            if (miniObjective != null)
            {
                bool show = tracker != null && tracker.Current != null && ObjectiveVisible(tracker.Current.position);
                miniObjective.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 d = tracker.Current.position - player.position;
                    Vector2 p = new Vector2(d.x, d.z) / miniRadius * half;
                    if (p.magnitude > half * 0.88f) p = p.normalized * half * 0.88f;
                    miniObjective.anchoredPosition = p;
                    miniObjective.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(Time.time * 5f));
                }
            }

            int used = 0;
            foreach (var s in sentinels)
            {
                if (s == null || !SentinelVisible(s)) continue;
                Vector3 d = s.transform.position - player.position;
                if (new Vector2(d.x, d.z).magnitude > miniRadius * 0.95f) continue;
                PlaceDot(miniDotPool, miniDots, used++, new Vector2(d.x, d.z) / miniRadius * half, 14f, Hunting(s));
            }
            HideRemaining(miniDotPool, used);
        }

        private void UpdateBig()
        {
            if (bigMapRect == null) return;
            Rect r = bigMapRect.rect;
            if (bigPlayer != null)
            {
                Vector2 uv = WorldToUV(player.position);
                bigPlayer.anchoredPosition = new Vector2((uv.x - 0.5f) * r.width, (uv.y - 0.5f) * r.height);
                bigPlayer.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            }
            var tracker = ObjectiveTracker.Instance;
            if (bigObjective != null)
            {
                bool show = tracker != null && tracker.Current != null && ObjectiveVisible(tracker.Current.position);
                bigObjective.gameObject.SetActive(show);
                if (show)
                {
                    Vector2 uv = WorldToUV(tracker.Current.position);
                    bigObjective.anchoredPosition = new Vector2((uv.x - 0.5f) * r.width, (uv.y - 0.5f) * r.height);
                    bigObjective.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(Time.time * 5f));
                }
            }
            int used = 0;
            foreach (var s in sentinels)
            {
                if (s == null || !SentinelVisible(s)) continue;
                Vector2 uv = WorldToUV(s.transform.position);
                PlaceDot(bigDotPool, bigDots, used++, new Vector2((uv.x - 0.5f) * r.width, (uv.y - 0.5f) * r.height), 12f, Hunting(s));
            }
            HideRemaining(bigDotPool, used);
        }

        private bool SentinelVisible(SentinelAI s)
        {
            return Hunting(s) || (EchoLens.Instance != null && EchoLens.Instance.Active) || IsExplored(s.transform.position) && Vector3.Distance(s.transform.position, player.position) < 16f;
        }

        private static bool Hunting(SentinelAI s) { return s.State == SentinelState.Chase || s.State == SentinelState.Attack || s.State == SentinelState.Alert; }

        private void PlaceDot(List<Image> pool, RectTransform parent, int index, Vector2 pos, float size, bool hunting)
        {
            if (parent == null) return;
            while (pool.Count <= index)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.sprite = dotSprite; img.raycastTarget = false;
                pool.Add(img);
            }
            var dot = pool[index];
            dot.gameObject.SetActive(true);
            dot.rectTransform.anchoredPosition = pos;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.color = hunting ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(1f, 0.7f, 0.2f, 0.7f);
        }

        private static void HideRemaining(List<Image> pool, int used)
        {
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
    }
}
