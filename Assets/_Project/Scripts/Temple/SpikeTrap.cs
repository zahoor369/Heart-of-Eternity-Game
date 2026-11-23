using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>
    /// A floor tile of spikes that cycles: hidden, a red warning glow, spikes up, spikes down. Two groups run half a cycle apart,
    /// so there is always a safe tile to step on if the timing is right.
    /// </summary>
    public class SpikeTrap : MonoBehaviour
    {
        [SerializeField] private Transform spikes;
        [SerializeField] private Renderer warningGlow;
        [SerializeField] private float cycle = 3f;
        [SerializeField] private float offset;
        [SerializeField] private float damage = 22f;
        [SerializeField] private Vector3 halfSize = new Vector3(1.9f, 1f, 1.9f);
        [SerializeField] private float raisedHeight = 0.85f;

        private float lastHit = -10f;
        private int lastPhase = -1;
        private MaterialPropertyBlock block;

        private void Awake() { block = new MaterialPropertyBlock(); }

        /// <summary>0 hidden, 1 warning, 2 up, 3 retracting.</summary>
        private int Phase(float t)
        {
            if (t < 0.40f) return 0;
            if (t < 0.60f) return 1;
            if (t < 0.87f) return 2;
            return 3;
        }

        private void Update()
        {
            float t = Mathf.Repeat(Time.time + offset, cycle) / cycle;
            int phase = Phase(t);
            float height = phase == 2 ? raisedHeight : (phase == 3 ? raisedHeight * (1f - (t - 0.87f) / 0.13f) : (phase == 1 ? 0.12f * Mathf.Sin(t * 120f) + 0.12f : 0f));
            if (spikes != null) spikes.localPosition = new Vector3(0f, height - raisedHeight - 0.02f, 0f);

            if (warningGlow != null)
            {
                float a = phase == 1 ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : (phase == 2 ? 0.7f : 0f);
                warningGlow.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(1f, 0.15f, 0.08f, a));
                warningGlow.SetPropertyBlock(block);
            }

            if (phase != lastPhase)
            {
                if (phase == 1 && Vector3.Distance(transform.position, Listener()) < 25f) Sfx.PlayAt("spike_warn", transform.position, 0.5f);
                if (phase == 2 && Vector3.Distance(transform.position, Listener()) < 25f) Sfx.PlayAt("spike", transform.position, 0.8f);
                lastPhase = phase;
            }

            var gm = GameManager.Instance;
            if (phase != 2 || gm == null || gm.State != GameState.Playing || gm.PlayerTransform == null) return;
            Vector3 local = transform.InverseTransformPoint(gm.PlayerTransform.position);
            if (Mathf.Abs(local.x) < halfSize.x && Mathf.Abs(local.z) < halfSize.z && local.y < 1.1f && Time.time - lastHit > 0.9f)
            {
                lastHit = Time.time;
                var hp = gm.PlayerTransform.GetComponent<Health>();
                if (hp != null) hp.TakeDamage(new DamageInfo { Amount = damage, Point = gm.PlayerTransform.position, Direction = Vector3.up });
            }
        }

        private static Vector3 Listener()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.PlayerTransform != null ? gm.PlayerTransform.position : Vector3.zero;
        }
    }
}
