using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using NED2024.Core;

namespace NED2024.UI
{
    /// <summary>Settings screen: volume sliders, mouse sensitivity, graphics quality, resolution and fullscreen.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button backButton;
        [Tooltip("One URP asset per quality level: Low, Medium, High")]
        [SerializeField] private RenderPipelineAsset[] qualityAssets;

        private readonly List<Resolution> resolutions = new List<Resolution>();
        private bool loading;

        private void Awake()
        {
            BuildResolutionList();

            masterSlider.onValueChanged.AddListener(v => { if (!loading) GameSettings.Master = v; });
            musicSlider.onValueChanged.AddListener(v => { if (!loading) GameSettings.Music = v; });
            sfxSlider.onValueChanged.AddListener(v => { if (!loading) GameSettings.Sfx = v; });
            sensitivitySlider.onValueChanged.AddListener(v => { if (!loading) GameSettings.MouseSensitivity = v; });
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        private void OnEnable()
        {
            LoadValues();
        }

        public void SetBackAction(UnityEngine.Events.UnityAction action)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => { GameSettings.Save(); action(); });
        }

        private void BuildResolutionList()
        {
            resolutions.Clear();
            var options = new List<string>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width < 960) continue;
                bool duplicate = false;
                foreach (var existing in resolutions)
                    if (existing.width == r.width && existing.height == r.height) { duplicate = true; break; }
                if (duplicate) continue;
                resolutions.Add(r);
                options.Add(r.width + " x " + r.height);
            }
            if (resolutions.Count == 0)
            {
                resolutions.Add(Screen.currentResolution);
                options.Add(Screen.currentResolution.width + " x " + Screen.currentResolution.height);
            }
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
        }

        private void LoadValues()
        {
            loading = true;
            masterSlider.value = GameSettings.Master;
            musicSlider.value = GameSettings.Music;
            sfxSlider.value = GameSettings.Sfx;
            sensitivitySlider.value = GameSettings.MouseSensitivity;
            qualityDropdown.value = Mathf.Clamp(GameSettings.Quality, 0, qualityDropdown.options.Count - 1);
            fullscreenToggle.isOn = GameSettings.Fullscreen;

            int saved = GameSettings.ResolutionIndex;
            if (saved < 0 || saved >= resolutions.Count)
            {
                saved = resolutions.Count - 1;
                for (int i = 0; i < resolutions.Count; i++)
                    if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height) saved = i;
            }
            resolutionDropdown.value = saved;
            loading = false;
        }

        private void OnQualityChanged(int index)
        {
            if (loading) return;
            GameSettings.Quality = index;
            ApplyQuality(index, qualityAssets);
        }

        private void OnResolutionChanged(int index)
        {
            if (loading || index < 0 || index >= resolutions.Count) return;
            GameSettings.ResolutionIndex = index;
            Screen.SetResolution(resolutions[index].width, resolutions[index].height, GameSettings.Fullscreen);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (loading) return;
            GameSettings.Fullscreen = value;
            Screen.fullScreen = value;
        }

        /// <summary>Applies a quality level: Unity quality level plus the matching URP asset (shadow distance, MSAA, render scale).</summary>
        public static void ApplyQuality(int index, RenderPipelineAsset[] assets)
        {
            QualitySettings.SetQualityLevel(Mathf.Clamp(index, 0, QualitySettings.names.Length - 1), true);
            if (assets != null && assets.Length > 0)
                QualitySettings.renderPipeline = assets[Mathf.Clamp(index, 0, assets.Length - 1)];
        }

        public void ApplySavedQuality()
        {
            ApplyQuality(GameSettings.Quality, qualityAssets);
        }
    }
}
