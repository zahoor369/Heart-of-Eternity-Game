using System;
using UnityEngine;

namespace NED2024.Core
{
    /// <summary>Player settings stored in PlayerPrefs. Static so any script can read the current values.</summary>
    public static class GameSettings
    {
        private const string MasterKey = "vol_master";
        private const string MusicKey = "vol_music";
        private const string SfxKey = "vol_sfx";
        private const string SensKey = "mouse_sens";
        private const string QualityKey = "quality";
        private const string FullscreenKey = "fullscreen";
        private const string ResolutionKey = "resolution";

        public static event Action Changed;

        public static float Master { get => PlayerPrefs.GetFloat(MasterKey, 0.9f); set { PlayerPrefs.SetFloat(MasterKey, value); Changed?.Invoke(); } }
        public static float Music { get => PlayerPrefs.GetFloat(MusicKey, 0.6f); set { PlayerPrefs.SetFloat(MusicKey, value); Changed?.Invoke(); } }
        public static float Sfx { get => PlayerPrefs.GetFloat(SfxKey, 0.9f); set { PlayerPrefs.SetFloat(SfxKey, value); Changed?.Invoke(); } }
        public static float MouseSensitivity { get => PlayerPrefs.GetFloat(SensKey, 1f); set { PlayerPrefs.SetFloat(SensKey, value); Changed?.Invoke(); } }
        public static int Quality { get => PlayerPrefs.GetInt(QualityKey, 1); set { PlayerPrefs.SetInt(QualityKey, value); Changed?.Invoke(); } }
        public static bool Fullscreen { get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1; set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); Changed?.Invoke(); } }
        public static int ResolutionIndex { get => PlayerPrefs.GetInt(ResolutionKey, -1); set { PlayerPrefs.SetInt(ResolutionKey, value); Changed?.Invoke(); } }

        public static void Save() => PlayerPrefs.Save();
    }
}
