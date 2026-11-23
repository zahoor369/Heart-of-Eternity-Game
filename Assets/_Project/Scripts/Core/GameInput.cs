using System.Collections.Generic;
using UnityEngine;

namespace NED2024.Core
{
    /// <summary>
    /// One place that reads the keyboard and mouse (legacy Input Manager).
    /// In normal play it simply forwards to UnityEngine.Input. The automated playtest switches
    /// <see cref="Scripted"/> on and feeds values in, so the real gameplay code can be tested without a human.
    /// </summary>
    public static class GameInput
    {
        public static bool Scripted;

        // Values supplied by the automated playtest
        public static Vector2 ScriptedMove;
        public static Vector2 ScriptedLook;
        public static bool ScriptedAim, ScriptedFire, ScriptedSprint;
        private static readonly Dictionary<string, int> taps = new Dictionary<string, int>();

        /// <summary>Presses a button for exactly one frame (the next one).</summary>
        public static void Tap(string name) { taps[name] = Time.frameCount + 1; }
        private static bool Tapped(string name) { return taps.TryGetValue(name, out int f) && f == Time.frameCount; }

        public static float Horizontal => Scripted ? ScriptedMove.x : Input.GetAxisRaw("Horizontal");
        public static float Vertical => Scripted ? ScriptedMove.y : Input.GetAxisRaw("Vertical");
        public static float LookX => Scripted ? ScriptedLook.x : Input.GetAxis("Mouse X");
        public static float LookY => Scripted ? ScriptedLook.y : Input.GetAxis("Mouse Y");
        public static bool Aim => Scripted ? ScriptedAim : Input.GetMouseButton(1);
        public static bool Fire => Scripted ? ScriptedFire : Input.GetMouseButton(0);
        public static bool FireDown => Scripted ? Tapped("Fire") : Input.GetMouseButtonDown(0);
        public static bool Sprint => Scripted ? ScriptedSprint : Input.GetKey(KeyCode.LeftShift);
        public static bool JumpDown => Scripted ? Tapped("Jump") : Input.GetKeyDown(KeyCode.Space);
        public static bool ReloadDown => Scripted ? Tapped("Reload") : Input.GetKeyDown(KeyCode.R);
        public static bool InteractDown => Scripted ? Tapped("Interact") : Input.GetKeyDown(KeyCode.E);
        public static bool TorchDown => Scripted ? Tapped("Torch") : Input.GetKeyDown(KeyCode.F);
        public static bool ScanDown => Scripted ? Tapped("Scan") : Input.GetKeyDown(KeyCode.Q);
        public static bool MapDown => Scripted ? Tapped("Map") : Input.GetKeyDown(KeyCode.M);
        public static bool PauseDown => Scripted ? Tapped("Pause") : Input.GetKeyDown(KeyCode.Escape);
        public static bool SkipDown => Scripted ? Tapped("Skip") : (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));
        public static float Wheel => Scripted ? 0f : Input.GetAxis("Mouse ScrollWheel");

        /// <summary>Weapon slot pressed this frame (0, 1, 2) or -1.</summary>
        public static int SlotDown
        {
            get
            {
                if (Scripted)
                {
                    for (int i = 0; i < 3; i++) if (Tapped("Slot" + i)) return i;
                    return -1;
                }
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                return -1;
            }
        }
    }
}
