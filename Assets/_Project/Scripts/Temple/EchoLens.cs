using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>
    /// The explorer's Echo Lens: a wrist device that sends out a pulse of light (Q). For a few seconds it reveals hidden glyphs
    /// and secret walls, points towards the current objective, and stuns the temple sentinels close by.
    /// </summary>
    public class EchoLens : MonoBehaviour
    {
        public static EchoLens Instance { get; private set; }

        [SerializeField] private float cooldown = 14f;
        [SerializeField] private float duration = 6f;
        [SerializeField] private float revealRadius = 32f;
        [SerializeField] private float stunRadius = 15f;
        [SerializeField] private float stunSeconds = 4f;
        [SerializeField] private GameObject ringPrefab;

        private float readyAt;
        private float startedAt = -100f;
        private Vector3 origin;

        public bool Ready => Time.time >= readyAt;
        public bool Active => Time.time - startedAt < duration;
        public float CooldownFraction => Mathf.Clamp01((readyAt - Time.time) / cooldown);   // 1 right after use, 0 when ready
        public float Cooldown => cooldown;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            if (GameInput.ScanDown) Pulse();
        }

        public bool Pulse()
        {
            if (!Ready) { Sfx.Play("rune_fail", 0.25f); return false; }
            readyAt = Time.time + cooldown;
            startedAt = Time.time;
            origin = transform.position;
            Sfx.Play("scan", 0.9f);
            if (ringPrefab != null)
            {
                var ring = Instantiate(ringPrefab, origin + Vector3.up * 0.12f, Quaternion.identity);
                ring.AddComponent<ScanRing>().Begin(revealRadius * 0.9f, 1.1f);
            }
            // Stun nearby sentinels
            int stunned = 0;
            foreach (var s in FindObjectsByType<SentinelAI>(FindObjectsSortMode.None))
                if (Vector3.Distance(s.transform.position, origin) <= stunRadius && s.Stun(stunSeconds)) stunned++;
            if (stunned > 0 && GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(GameManager.PointsPerStun * stunned);
                GameManager.Instance.ShowMessage("Echo pulse: " + stunned + (stunned == 1 ? " sentinel" : " sentinels") + " stunned", 2f);
            }
            return true;
        }

        /// <summary>How strongly a hidden thing at this position is revealed (0 to 1). A wave of light travels outwards from the pulse.</summary>
        public static float Reveal(Vector3 position)
        {
            var lens = Instance;
            if (lens == null || !lens.Active) return 0f;
            float t = Time.time - lens.startedAt;
            float d = Vector3.Distance(position, lens.origin);
            if (d > lens.revealRadius) return 0f;
            float wave = lens.revealRadius * Mathf.Clamp01(t / 0.9f);
            if (d > wave) return 0f;
            float fade = Mathf.Clamp01((lens.duration - t) / 1.2f);
            return fade;
        }
    }
}
