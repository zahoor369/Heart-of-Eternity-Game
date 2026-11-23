using UnityEngine;
using UnityEngine.UI;
using NED2024.Core;

namespace NED2024.UI
{
    /// <summary>Main menu: New Game, Continue, Settings, Controls, Credits, Exit.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject scoresPanel;

        [Header("Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button controlsBackButton;
        [SerializeField] private Button creditsBackButton;
        [SerializeField] private Button scoresButton;
        [SerializeField] private Button scoresBackButton;

        [Header("Player profile")]
        [SerializeField] private InputField nameInput;
        [SerializeField] private Text scoresText;
        [SerializeField] private Text statsText;

        [Header("Other")]
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private LoadingScreen loading;
        [SerializeField] private AudioClip menuMusic;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            settings.ApplySavedQuality();
            settings.SetBackAction(() => Show(mainPanel));
            AudioManager.Instance.PlayMusic(menuMusic);

            newGameButton.onClick.AddListener(StartNewGame);
            continueButton.onClick.AddListener(ContinueGame);
            settingsButton.onClick.AddListener(() => Show(settingsPanel));
            controlsButton.onClick.AddListener(() => Show(controlsPanel));
            creditsButton.onClick.AddListener(() => Show(creditsPanel));
            controlsBackButton.onClick.AddListener(() => Show(mainPanel));
            creditsBackButton.onClick.AddListener(() => Show(mainPanel));
            exitButton.onClick.AddListener(QuitGame);
            scoresButton.onClick.AddListener(() => { RefreshScores(); Show(scoresPanel); });
            scoresBackButton.onClick.AddListener(() => Show(mainPanel));

            nameInput.text = PlayerProfile.Name;
            nameInput.characterLimit = 14;
            nameInput.onEndEdit.AddListener(_ => SaveName());

            continueButton.interactable = GameManager.HasSavedCheckpoint;
            loading.gameObject.SetActive(false);
            Show(mainPanel);
        }

        private void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            settingsPanel.SetActive(panel == settingsPanel);
            controlsPanel.SetActive(panel == controlsPanel);
            creditsPanel.SetActive(panel == creditsPanel);
            scoresPanel.SetActive(panel == scoresPanel);
        }

        private void SaveName()
        {
            PlayerProfile.Name = nameInput.text;
            nameInput.text = PlayerProfile.Name;   // shows "Survivor" again if the field was left empty
        }

        private void RefreshScores()
        {
            scoresText.text = PlayerProfile.TableText();
            statsText.text = PlayerProfile.Name + "\n" + PlayerProfile.StatsText();
        }

        private void StartNewGame()
        {
            GameManager.ClearCheckpoint();
            GameManager.ContinueRequested = false;
            StartLoading();
        }

        private void ContinueGame()
        {
            GameManager.ContinueRequested = true;
            StartLoading();
        }

        private void StartLoading()
        {
            SaveName();
            mainPanel.SetActive(false);
            loading.Load(GameManager.GameScene);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
