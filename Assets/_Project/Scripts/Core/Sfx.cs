using System.Collections.Generic;
using UnityEngine;

namespace NED2024.Core
{
    /// <summary>
    /// Sound effects by name. The clips live in Assets/_Project/Resources/Audio and are loaded the first time they are used,
    /// so scripts only need to know a name such as "key_chime".
    /// </summary>
    public static class Sfx
    {
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!cache.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null) Debug.LogWarning("Missing sound: " + name);
                cache[name] = clip;
            }
            return clip;
        }

        /// <summary>Plays a 2D sound (interface sounds, the player's own actions).</summary>
        public static void Play(string name, float volume = 1f)
        {
            AudioManager.Instance.PlaySfx(Get(name), volume);
        }

        /// <summary>Plays a 3D sound at a place in the temple.</summary>
        public static void PlayAt(string name, Vector3 position, float volume = 1f)
        {
            AudioManager.Instance.PlaySfxAt(Get(name), position, volume);
        }
    }
}
