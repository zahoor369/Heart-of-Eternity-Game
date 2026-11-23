using UnityEngine;
using NED2024.Core;

namespace NED2024.Temple
{
    public enum ZoneKind { HallSeal, RuneChamber, ExitGate, Checkpoint, Hint }

    /// <summary>An invisible trigger volume that makes something happen when the explorer walks into it.</summary>
    public class ZoneTrigger : MonoBehaviour
    {
        [SerializeField] private ZoneKind kind;
        [SerializeField] private string message = "";
        [SerializeField] private float messageSeconds = 4f;
        [SerializeField] private TempleDoor door;

        private bool used;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            switch (kind)
            {
                case ZoneKind.HallSeal:
                    if (used) return; used = true;
                    if (door != null) door.Slam();
                    NED2024.CameraSystem.ThirdPersonCameraController.Shake(0.9f, 2.5f);
                    Sfx.Play("rumble", 0.7f);
                    gm.ShowMessage(message, messageSeconds);
                    break;
                case ZoneKind.RuneChamber:
                    if (used) return; used = true;
                    gm.EnteredRuneChamber();
                    if (!string.IsNullOrEmpty(message)) gm.ShowMessage(message, messageSeconds);
                    break;
                case ZoneKind.ExitGate:
                    if (gm.Stage == MissionStage.Escape) gm.CompleteMission();
                    break;
                case ZoneKind.Checkpoint:
                    if (used) return; used = true;
                    gm.SetCheckpoint(transform.position, other.transform.eulerAngles.y);
                    gm.ShowMessage("Checkpoint saved", 1.6f);
                    break;
                case ZoneKind.Hint:
                    if (used) return; used = true;
                    gm.ShowMessage(message, messageSeconds);
                    break;
            }
        }

        /// <summary>Lets a saved game mark a one-time zone as already used.</summary>
        public void MarkUsed() { used = true; }
        public ZoneKind Kind => kind;
    }
}
