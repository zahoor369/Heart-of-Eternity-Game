using UnityEngine;
using NED2024.Core;
using NED2024.Interactions;

namespace NED2024.Temple
{
    public enum CollectibleKind { SunKey, MoonKey, Relic, LifeOrb }

    /// <summary>Something the explorer picks up by walking into it: keys, Sun relics (score) and life orbs (health).</summary>
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private CollectibleKind kind;
        [SerializeField] private float healAmount = 35f;
        [SerializeField] private Transform visual;
        [SerializeField] private float spinSpeed = 70f;
        [SerializeField] private float bobHeight = 0.12f;

        private bool collected;
        private Vector3 basePosition;
        private float phase;

        public CollectibleKind Kind => kind;
        public Collectible SetKind(CollectibleKind k) { kind = k; return this; }

        private void Start()
        {
            if (visual == null) visual = transform;
            basePosition = visual.localPosition;
            phase = Random.value * 6.28f;
        }

        private void Update()
        {
            if (visual == null) return;
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * bobHeight);
        }

        private void OnTriggerEnter(Collider other) { TryCollect(other); }
        private void OnTriggerStay(Collider other) { TryCollect(other); }

        private void TryCollect(Collider other)
        {
            if (collected || !other.CompareTag("Player")) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            switch (kind)
            {
                case CollectibleKind.LifeOrb:
                    var hp = other.GetComponent<Health>();
                    if (hp == null || hp.Current >= hp.Max)
                    {
                        gm.ShowMessage("Health is already full", 1.2f);
                        return;
                    }
                    hp.Heal(healAmount);
                    gm.AddScore(GameManager.PointsPerOrb);
                    Sfx.Play("heal", 0.8f);
                    gm.ShowMessage("A life orb restores your strength  +" + Mathf.RoundToInt(healAmount), 2f);
                    break;
                case CollectibleKind.Relic:
                    gm.CollectRelic();
                    Sfx.Play("relic_chime", 0.9f);
                    gm.ShowMessage("Sun relic found  (" + gm.RelicsFound + "/" + gm.RelicsTotal + ")", 3f);
                    break;
                case CollectibleKind.SunKey:
                    gm.CollectSunKey();
                    Sfx.Play("key_chime", 1f);
                    gm.ShowMessage("You took the Sun Key. A door somewhere in the temple bears the same symbol.", 4.5f);
                    break;
                case CollectibleKind.MoonKey:
                    gm.CollectMoonKey();
                    Sfx.Play("key_chime", 1f);
                    gm.ShowMessage("You took the Moon Key. The vault door behind you unlocks.", 4.5f);
                    break;
            }
            collected = true;
            Destroy(gameObject);
        }
    }
}
