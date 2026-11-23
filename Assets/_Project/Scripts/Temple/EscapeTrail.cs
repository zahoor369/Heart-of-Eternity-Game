using UnityEngine;

namespace NED2024.Temple
{
    /// <summary>Golden chevrons on the floor that light up when the Heart is taken and show the way back to the entrance.</summary>
    public class EscapeTrail : MonoBehaviour
    {
        [SerializeField] private Renderer[] pieces;     // ordered from the moon door to the entrance
        [SerializeField] private Color color = new Color(1f, 0.78f, 0.25f);

        private bool revealed;
        private float revealedAt;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            Hide();
        }

        public void Reveal() { revealed = true; revealedAt = Time.time; }

        private void Hide()
        {
            foreach (var r in pieces) if (r != null) r.enabled = false;
        }

        private void Update()
        {
            if (!revealed) return;
            float t = Time.time - revealedAt;
            for (int i = 0; i < pieces.Length; i++)
            {
                var r = pieces[i];
                if (r == null) continue;
                float appear = Mathf.Clamp01((t * 24f - i) * 0.25f);          // lights up one after the other
                r.enabled = appear > 0.01f;
                float wave = 0.45f + 0.55f * Mathf.Max(0f, Mathf.Sin(t * 5f - i * 0.45f));
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, appear * wave));
                r.SetPropertyBlock(block);
            }
        }
    }
}
