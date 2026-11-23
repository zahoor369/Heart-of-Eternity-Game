using UnityEngine;

namespace NED2024.Core
{
    /// <summary>Plays music and sound effects and applies the volume sliders from the settings menu.</summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        private AudioSource musicSource;
        private AudioSource uiSource;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f;

            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (instance == this) GameSettings.Changed -= ApplyVolumes;
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = GameSettings.Master;
            if (musicSource != null) musicSource.volume = GameSettings.Music;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || musicSource.clip == clip) return;
            musicSource.clip = clip;
            musicSource.Play();
        }

        /// <summary>2D sound, used for the UI and for the player's own weapon.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            uiSource.PlayOneShot(clip, volume * GameSettings.Sfx);
        }

        /// <summary>3D positional sound so enemies and impacts are heard from the right direction.</summary>
        public void PlaySfxAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume * GameSettings.Sfx);
        }
    }
}
