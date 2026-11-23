using System.Collections;
using UnityEngine;
using NED2024.Core;
using NED2024.Interactions;

namespace NED2024.Temple
{
    /// <summary>
    /// A stone slab that sinks into the floor to open. Sun and Moon doors need a key and open with E; the rune gate is opened by the
    /// rune puzzle, the vault exit by taking the Moon Key, and the entrance is closed by the temple and opened by the escape.
    /// A glowing seam shows the state: red = locked, gold = can be opened, green = open.
    /// </summary>
    public class TempleDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorKind kind;
        [SerializeField] private Transform slab;
        [SerializeField] private Renderer[] glowRenderers;
        [SerializeField] private Light glowLight;
        [SerializeField] private float height = 5.8f;
        [SerializeField] private bool startOpen;

        private static readonly Color Locked = new Color(1f, 0.15f, 0.1f), Ready = new Color(1f, 0.75f, 0.2f), OpenColor = new Color(0.2f, 1f, 0.55f);

        private Vector3 closedPosition;
        private Coroutine moving;
        private Collider[] colliders;
        private MaterialPropertyBlock block;

        public DoorKind Kind => kind;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (slab == null) slab = transform;
            closedPosition = slab.localPosition;
            colliders = slab.GetComponentsInChildren<Collider>();
            block = new MaterialPropertyBlock();
            if (startOpen) SetOpen(true, true);
            else Paint();
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.ItemsChanged += OnItemsChanged;
            gm.HeartTaken += OnHeartTaken;
            OnItemsChanged();
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.ItemsChanged -= OnItemsChanged;
            gm.HeartTaken -= OnHeartTaken;
        }

        private void OnItemsChanged()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            // The vault exit only opens from the inside, when the Moon Key has been taken.
            if (kind == DoorKind.VaultExit && gm.HasMoonKey && !IsOpen) SetOpen(true);
            Paint();
        }

        private void OnHeartTaken()
        {
            if (kind == DoorKind.Entrance) StartCoroutine(OpenEntranceLater());
        }

        private IEnumerator OpenEntranceLater()
        {
            yield return new WaitForSeconds(1.5f);
            SetOpen(true);
            GameManager.Instance.ShowMessage("The entrance has opened! Run!", 4f);
        }

        // ---------------------------------------------------------------- interaction

        private bool NeedsKey => kind == DoorKind.SunDoor || kind == DoorKind.MoonDoor;

        public bool CanInteract => NeedsKey && !IsOpen;

        public string Prompt
        {
            get
            {
                var gm = GameManager.Instance;
                if (kind == DoorKind.SunDoor) return gm != null && gm.HasSunKey ? "Press E  -  use the Sun Key" : "Sealed. A golden sun symbol is carved here.";
                if (kind == DoorKind.MoonDoor) return gm != null && gm.HasMoonKey ? "Press E  -  use the Moon Key" : "Sealed. A silver moon symbol is carved here.";
                return "";
            }
        }

        public void Interact()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            bool hasKey = (kind == DoorKind.SunDoor && gm.HasSunKey) || (kind == DoorKind.MoonDoor && gm.HasMoonKey);
            if (!hasKey)
            {
                Sfx.Play("rune_fail", 0.5f);
                gm.ShowMessage(kind == DoorKind.SunDoor ? "The door is sealed. Find the Sun Key." : "The door is sealed. Find the Moon Key.", 3f);
                return;
            }
            SetOpen(true);
            gm.AddScore(GameManager.PointsPerStage / 3);
            if (kind == DoorKind.MoonDoor) gm.UsedMoonDoor();
            gm.ShowMessage(kind == DoorKind.SunDoor ? "The Sun Door grinds open." : "The Moon Door grinds open. The heart is near.", 3f);
        }

        // ---------------------------------------------------------------- movement

        public void SetOpen(bool open, bool instant = false)
        {
            if (IsOpen == open && !instant) return;
            IsOpen = open;
            if (moving != null) StopCoroutine(moving);
            Vector3 target = open ? closedPosition + Vector3.down * height : closedPosition;
            if (instant)
            {
                slab.localPosition = target;
                foreach (var c in colliders) c.enabled = !open;
                Paint();
                return;
            }
            moving = StartCoroutine(Slide(target, open));
        }

        public void Slam()
        {
            // Used for the entrance: the slab drops from above in a fraction of a second.
            if (moving != null) StopCoroutine(moving);
            IsOpen = false;
            slab.localPosition = closedPosition + Vector3.up * height;
            foreach (var c in colliders) c.enabled = true;
            moving = StartCoroutine(Fall());
        }

        private IEnumerator Fall()
        {
            Vector3 from = slab.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.45f;
                slab.localPosition = Vector3.Lerp(from, closedPosition, t * t);
                yield return null;
            }
            slab.localPosition = closedPosition;
            Sfx.PlayAt("door_slam", transform.position, 1f);
            Paint();
        }

        private IEnumerator Slide(Vector3 target, bool open)
        {
            Sfx.PlayAt("stone_slide", transform.position, 0.9f);
            if (!open) foreach (var c in colliders) c.enabled = true;
            Vector3 from = slab.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 2.6f;
                slab.localPosition = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
                if (open && t > 0.55f) foreach (var c in colliders) c.enabled = false;
                yield return null;
            }
            slab.localPosition = target;
            Paint();
        }

        private void Update()
        {
            // Pulse the seam so a door that can be used is easy to spot.
            if (glowRenderers != null && glowRenderers.Length > 0 && !IsOpen) Paint();
        }

        private void Paint()
        {
            var gm = GameManager.Instance;
            Color c = Locked;
            if (IsOpen) c = OpenColor;
            else if (gm != null && ((kind == DoorKind.SunDoor && gm.HasSunKey) || (kind == DoorKind.MoonDoor && gm.HasMoonKey))) c = Ready;
            float pulse = IsOpen ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            if (glowRenderers != null)
                foreach (var r in glowRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", c * 2.2f * pulse);
                    block.SetColor("_BaseColor", c * 0.3f);
                    r.SetPropertyBlock(block);
                }
            if (glowLight != null) { glowLight.color = c; glowLight.intensity = 2.2f * pulse; }
        }
    }
}
