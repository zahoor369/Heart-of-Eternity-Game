using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NED2024.Core;

namespace NED2024.UI
{
    /// <summary>Loads a scene in the background and shows a progress bar and a tip.</summary>
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text percentText;
        [SerializeField] private Text tipText;
        [SerializeField] private float minimumSeconds = 2f;
        [SerializeField] private string[] tips =
        {
            "Press Q to send out an Echo pulse. It reveals hidden carvings and stuns sentinels.",
            "Sentinels are slow. Sprint with Left Shift to outrun them, but running is loud.",
            "Spike tiles light up red just before they strike. Wait for them to sink, then cross.",
            "Some dead ends hide secret walls. Use the Echo Lens to find their glowing glyph.",
            "Life orbs restore health. Sun relics add to your score.",
            "When the Heart is taken the temple starts to seal itself. Follow the golden light."
        };

        public void Load(string sceneName)
        {
            gameObject.SetActive(true);
            if (tipText != null) tipText.text = tips[Random.Range(0, tips.Length)];
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            float elapsed = 0f;
            while (op.progress < 0.9f || elapsed < minimumSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                // The async operation stops at 0.9 until we allow activation, so rescale to 0-1.
                float real = Mathf.Clamp01(op.progress / 0.9f);
                float timed = Mathf.Clamp01(elapsed / minimumSeconds);
                float shown = Mathf.Min(real, timed);
                if (progressBar != null) progressBar.value = shown;
                if (percentText != null) percentText.text = Mathf.RoundToInt(shown * 100f) + "%";
                yield return null;
            }
            if (progressBar != null) progressBar.value = 1f;
            if (percentText != null) percentText.text = "100%";
            yield return new WaitForSecondsRealtime(0.2f);
            op.allowSceneActivation = true;
        }
    }
}
