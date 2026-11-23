using System.Collections;
using UnityEngine;
using NED2024.Core;
using NED2024.Interactions;

namespace NED2024.Temple
{
    /// <summary>A hidden wall segment at the end of a dead end. Its glyph shows only under the Echo Lens; pressing E sinks the wall.</summary>
    public class SecretWall : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform slab;
        [SerializeField] private float height = 5.8f;
        [SerializeField] private Transform revealPoint;

        private Collider[] colliders;
        private Vector3 closedPosition;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (slab == null) slab = transform;
            closedPosition = slab.localPosition;
            colliders = slab.GetComponentsInChildren<Collider>();
        }

        private Vector3 Where => revealPoint != null ? revealPoint.position : transform.position;

        public bool CanInteract => !IsOpen && EchoLens.Reveal(Where) > 0.35f;
        public string Prompt => "Press E  -  push the glowing glyph";

        public void Interact()
        {
            if (IsOpen) return;
            Open(false);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.FoundSecret();
                gm.ShowMessage("A hidden passage! (+" + GameManager.PointsPerSecret + ")", 3f);
            }
        }

        public void Open(bool instant)
        {
            IsOpen = true;
            if (instant) { slab.localPosition = closedPosition + Vector3.down * height; foreach (var c in colliders) c.enabled = false; return; }
            StartCoroutine(Slide());
        }

        private IEnumerator Slide()
        {
            Sfx.PlayAt("stone_slide", transform.position, 0.8f);
            Vector3 from = slab.localPosition, to = closedPosition + Vector3.down * height;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 2.2f;
                slab.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                if (t > 0.5f) foreach (var c in colliders) c.enabled = false;
                yield return null;
            }
        }
    }
}
