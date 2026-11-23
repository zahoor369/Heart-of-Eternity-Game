using System;
using UnityEngine;
using NED2024.Core;

namespace NED2024.Interactions
{
    /// <summary>Finds the nearest usable object around the player and uses it when E is pressed.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float radius = 2.2f;
        [SerializeField] private float scanInterval = 0.1f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private readonly Collider[] hits = new Collider[16];
        private IInteractable current;
        private float nextScan;

        /// <summary>Raised with the prompt text, or null when nothing is in range.</summary>
        public event Action<string> PromptChanged;

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing)
            {
                SetCurrent(null);
                return;
            }

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + scanInterval;
                Scan();
            }

            if (current != null && GameInput.InteractDown && current.CanInteract)
            {
                current.Interact();
            }
        }

        private void Scan()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, hits, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var interactable = hits[i].GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract) continue;
                float d = (hits[i].transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = interactable; }
            }
            SetCurrent(best);
        }

        private void SetCurrent(IInteractable next)
        {
            if (ReferenceEquals(next, current)) return;
            current = next;
            PromptChanged?.Invoke(current != null ? current.Prompt : null);
        }
    }
}
