using UnityEngine;
using NED2024.Core;

namespace NED2024.Environment
{
    /// <summary>Looping ambient sound (wind) that follows the music volume slider.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientLoop : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.5f;
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.spatialBlend = 0f;
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= Apply;
        }

        private void Apply()
        {
            if (source != null) source.volume = baseVolume * GameSettings.Music;
        }
    }
}
