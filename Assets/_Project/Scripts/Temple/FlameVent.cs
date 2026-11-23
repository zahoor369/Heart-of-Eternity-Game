using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>A vent in the wall that breathes fire in bursts: a hiss and a glow as a warning, then a jet of flame.</summary>
    public class FlameVent : MonoBehaviour
    {
        [SerializeField] private ParticleSystem flames;
        [SerializeField] private Light glow;
        [SerializeField] private float cycle = 4.2f;
        [SerializeField] private float offset;
        [SerializeField] private float damagePerSecond = 22f;
        [SerializeField] private float length = 3.6f;
        [SerializeField] private float width = 1.4f;

        private int lastPhase = -1;
        private float lastHit = -10f;

        private void Update()
        {
            float t = Mathf.Repeat(Time.time + offset, cycle) / cycle;
            int phase = t < 0.55f ? 0 : (t < 0.7f ? 1 : (t < 0.92f ? 2 : 0));
            if (phase != lastPhase)
            {
                if (phase == 1) { Sfx.PlayAt("spike_warn", transform.position, 0.5f); }
                if (phase == 2) { Sfx.PlayAt("zap", transform.position, 0.35f); if (flames != null) flames.Play(); }
                if (phase == 0 && flames != null) flames.Stop();
                lastPhase = phase;
            }
            if (glow != null) glow.intensity = phase == 2 ? 6f : (phase == 1 ? 1.5f + Mathf.Sin(Time.time * 25f) : 0f);

            var gm = GameManager.Instance;
            if (phase != 2 || gm == null || gm.State != GameState.Playing || gm.PlayerTransform == null) return;
            Vector3 local = transform.InverseTransformPoint(gm.PlayerTransform.position + Vector3.up * 0.9f);
            // the jet goes along the vent's forward axis (local +Z)
            if (local.z > 0f && local.z < length && Mathf.Abs(local.x) < width && Mathf.Abs(local.y) < 1.6f && Time.time - lastHit > 0.45f)
            {
                lastHit = Time.time;
                var hp = gm.PlayerTransform.GetComponent<Health>();
                if (hp != null) hp.TakeDamage(new DamageInfo { Amount = damagePerSecond * 0.45f, Point = gm.PlayerTransform.position, Direction = transform.forward });
            }
        }
    }
}
