using UnityEngine;

namespace NED2024.Temple
{
    /// <summary>The expanding ring of light drawn on the floor by the Echo Lens.</summary>
    public class ScanRing : MonoBehaviour
    {
        private float radius, seconds, t;
        private Renderer rend;
        private MaterialPropertyBlock block;

        public void Begin(float maxRadius, float lifetime)
        {
            radius = maxRadius; seconds = lifetime;
            rend = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            t += Time.deltaTime / Mathf.Max(0.1f, seconds);
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            transform.localScale = new Vector3(radius * 2f * e, radius * 2f * e, 1f);
            if (rend != null)
            {
                rend.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(0.4f, 0.95f, 1f, 0.9f * (1f - t)));
                rend.SetPropertyBlock(block);
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
