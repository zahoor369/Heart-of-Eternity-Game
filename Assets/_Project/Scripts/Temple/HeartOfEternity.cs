using System.Collections;
using UnityEngine;
using NED2024.Core;
using NED2024.Interactions;

namespace NED2024.Temple
{
    /// <summary>The Heart of Eternity. Taking it starts the collapse: the entrance opens, the boulder is released and the escape timer starts.</summary>
    public class HeartOfEternity : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject crystal;
        [SerializeField] private Light heartLight;
        [SerializeField] private RollingBoulder boulder;
        [SerializeField] private EscapeTrail trail;

        public bool Taken { get; private set; }

        public bool CanInteract => !Taken;
        public string Prompt => "Press E  -  take the Heart of Eternity";

        public void Interact()
        {
            if (Taken) return;
            Take(false);
        }

        public void Take(bool instant)
        {
            Taken = true;
            if (crystal != null) crystal.SetActive(false);
            if (heartLight != null) heartLight.enabled = false;
            if (instant) return;
            var gm = GameManager.Instance;
            Sfx.Play("heart_pickup", 1f);
            gm.TakeHeartOfEternity();
            gm.ShowMessage("The temple shakes! The walls are closing in. Follow the golden light!", 5f);
            NED2024.CameraSystem.ThirdPersonCameraController.Shake(1.1f, 6f);
            Sfx.Play("rumble", 0.9f);
            if (trail != null) trail.Reveal();
            if (boulder != null) boulder.Begin(9f);
        }
    }
}
