using UnityEngine;
using NED2024.Core;
using NED2024.Interactions;

namespace NED2024.Temple
{
    /// <summary>One pedestal of the rune puzzle. Press E to wake its rune.</summary>
    public class RunePedestal : MonoBehaviour, IInteractable
    {
        [SerializeField] private int glyph;
        [SerializeField] private RunePuzzle puzzle;
        [SerializeField] private Renderer[] glowRenderers;
        [SerializeField] private Light glowLight;

        private bool lit, flash;
        private MaterialPropertyBlock block;
        private static readonly Color Idle = new Color(0.25f, 0.7f, 1f), Gold = new Color(1f, 0.8f, 0.25f), Red = new Color(1f, 0.15f, 0.1f);

        public int Glyph => glyph;

        private void Awake() { block = new MaterialPropertyBlock(); }
        private void Start() { Paint(); }

        public bool CanInteract => puzzle != null && !puzzle.Solved && !lit;
        public string Prompt => "Press E  -  touch the rune";

        public void Interact() { puzzle.Press(this); }

        public void Light(bool on) { lit = on; Paint(); }
        public void Flash(bool on) { flash = on; Paint(); }

        private void Update()
        {
            if (!lit && !flash) Paint();   // idle pulse
        }

        private void Paint()
        {
            Color c = flash ? Red : (lit ? Gold : Idle);
            float k = lit || flash ? 1f : 0.55f + 0.25f * Mathf.Sin(Time.time * 2f + glyph);
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", c * 2.4f * k);
                block.SetColor("_BaseColor", new Color(c.r, c.g, c.b, Mathf.Clamp01(0.35f + 0.65f * k)));
                r.SetPropertyBlock(block);
            }
            if (glowLight != null) { glowLight.color = c; glowLight.intensity = 2.6f * k; }
        }
    }
}
