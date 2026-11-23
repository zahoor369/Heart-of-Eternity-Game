using UnityEngine;

namespace NED2024.Temple
{
    /// <summary>A carving that glows only while the Echo Lens is revealing it (the rune order mural, secret wall marks).</summary>
    public class HiddenGlyph : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color tint = new Color(0.4f, 0.95f, 1f);
        [SerializeField] private float faintGlow = 0.05f;
        [SerializeField] private Light revealLight;

        private float amount;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
        }

        public float Amount => amount;

        private void Update()
        {
            float target = EchoLens.Reveal(transform.position);
            amount = Mathf.MoveTowards(amount, target, 3f * Time.deltaTime);
            float a = Mathf.Max(faintGlow, amount);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, a));
                r.SetPropertyBlock(block);
            }
            if (revealLight != null) revealLight.intensity = amount * 2.5f;
        }
    }
}
