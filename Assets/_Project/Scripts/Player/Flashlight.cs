using UnityEngine;
using NED2024.Core;

namespace NED2024.Player
{
    /// <summary>Toggleable torch on the player. F key.</summary>
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] private Light torch;
        [SerializeField] private AudioClip toggleClip;
        [SerializeField] private KeyCode key = KeyCode.F;

        private bool hinted;

        private void Start()
        {
            // The temple is dark, so the lantern starts on.
            if (torch != null) torch.enabled = true;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            if (!hinted)
            {
                hinted = true;
                gm.ShowMessage("WASD move   Shift sprint   E use   Q Echo Lens   M map", 6f);
            }
            if (GameInput.TorchDown && torch != null)
            {
                torch.enabled = !torch.enabled;
                AudioManager.Instance.PlaySfx(toggleClip, 0.5f);
            }
        }
    }
}
