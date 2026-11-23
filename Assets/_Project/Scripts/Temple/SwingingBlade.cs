using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    /// <summary>A pendulum axe. The pivot is on the ceiling; the blade swings across the corridor and cuts anything in its path.</summary>
    public class SwingingBlade : MonoBehaviour
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform bladeCentre;
        [SerializeField] private float amplitude = 62f;       // degrees each way
        [SerializeField] private float period = 3.2f;
        [SerializeField] private float phase;
        [SerializeField] private float damage = 28f;
        [SerializeField] private float hitRadius = 1.35f;

        private float lastHit = -10f;
        private float lastAngle;
        private bool wasNearCentre;

        private void Update()
        {
            float angle = amplitude * Mathf.Sin(2f * Mathf.PI * (Time.time / period) + phase);
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);

            // whoosh when the blade passes the middle
            bool nearCentre = Mathf.Abs(angle) < 8f;
            var gm = GameManager.Instance;
            if (nearCentre && !wasNearCentre && gm != null && gm.PlayerTransform != null && Vector3.Distance(gm.PlayerTransform.position, transform.position) < 18f)
                Sfx.PlayAt("blade", bladeCentre.position, 0.7f);
            wasNearCentre = nearCentre;
            lastAngle = angle;

            if (gm == null || gm.State != GameState.Playing || gm.PlayerTransform == null) return;
            Vector3 p = gm.PlayerTransform.position + Vector3.up * 1f;
            if (Vector3.Distance(p, bladeCentre.position) < hitRadius && Time.time - lastHit > 1.0f)
            {
                lastHit = Time.time;
                var hp = gm.PlayerTransform.GetComponent<Health>();
                if (hp != null) hp.TakeDamage(new DamageInfo { Amount = damage, Point = p, Direction = pivot.right * Mathf.Sign(Mathf.Cos(2f * Mathf.PI * (Time.time / period) + phase)) });
            }
        }
    }
}
