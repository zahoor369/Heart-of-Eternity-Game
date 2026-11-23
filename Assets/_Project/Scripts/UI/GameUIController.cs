using UnityEngine;
using UnityEngine.UI;
using NED2024.Core;

namespace NED2024.UI
{
    /// <summary>Pause menu, Game Over screen and Escape (win) screen. Reacts to GameManager state changes.</summary>
    public class GameUIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject pauseMainGroup;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject completePanel;

        [Header("Pause buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseSettingsButton;
        [SerializeField] private Button pauseMenuButton;

        [Header("Game over")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button gameOverMenuButton;
        [SerializeField] private Text gameOverReasonText;
        [SerializeField] private Text gameOverScoreText;

        [Header("Escape screen")]
        [SerializeField] private Button completeMenuButton;
        [SerializeField] private Text completeStatsText;
        [SerializeField] private Text completeScoreText;

        [SerializeField] private SettingsPanel settings;

        private void Start()
        {
            var gm = GameManager.Instance;
            resumeButton.onClick.AddListener(gm.Resume);
            pauseSettingsButton.onClick.AddListener(() => { pauseMainGroup.SetActive(false); settingsPanel.SetActive(true); });
            pauseMenuButton.onClick.AddListener(gm.ExitToMenu);
            retryButton.onClick.AddListener(gm.RetryFromCheckpoint);
            restartButton.onClick.AddListener(gm.RestartFromBeginning);
            gameOverMenuButton.onClick.AddListener(gm.ExitToMenu);
            completeMenuButton.onClick.AddListener(gm.ExitToMenu);
            settings.SetBackAction(() => { settingsPanel.SetActive(false); pauseMainGroup.SetActive(true); });

            retryButton.interactable = GameManager.HasSavedCheckpoint;
            gm.StateChanged += OnStateChanged;
            OnStateChanged(gm.State);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            pausePanel.SetActive(state == GameState.Paused);
            if (state == GameState.Paused) { pauseMainGroup.SetActive(true); settingsPanel.SetActive(false); }
            gameOverPanel.SetActive(state == GameState.GameOver);
            completePanel.SetActive(state == GameState.Complete);
            var gm = GameManager.Instance;

            if (state == GameState.GameOver)
            {
                retryButton.interactable = GameManager.HasSavedCheckpoint;
                if (gameOverReasonText != null) gameOverReasonText.text = gm.EndReason;
                if (gameOverScoreText != null)
                    gameOverScoreText.text = PlayerProfile.Name + "     Score  " + gm.Score + (gm.FinalRank > 0 ? "     (#" + gm.FinalRank + " on the high-score table)" : "");
            }
            if (state == GameState.Complete)
            {
                int minutes = Mathf.FloorToInt(gm.PlayTime / 60f);
                int seconds = Mathf.FloorToInt(gm.PlayTime % 60f);   // modulo gives the leftover seconds
                completeStatsText.text = "Time  " + minutes.ToString("00") + ":" + seconds.ToString("00") +
                                         "     Relics  " + gm.RelicsFound + "/" + gm.RelicsTotal +
                                         "     Secrets  " + gm.SecretsFound + "     Falls  " + gm.Deaths;
                if (completeScoreText != null)
                    completeScoreText.text = PlayerProfile.Name + "     SCORE  " + gm.Score +
                                             (gm.FinalRank == 1 ? "     NEW HIGH SCORE!" : (gm.FinalRank > 0 ? "     #" + gm.FinalRank + " on the table" : "")) +
                                             "\n<size=26>escape bonus " + gm.EscapeBonus + "  +  time bonus " + gm.TimeBonus + "  +  health bonus " + gm.HealthBonus +
                                             (gm.PerfectBonus > 0 ? "  +  flawless run " + gm.PerfectBonus : "") + "</size>";
            }
        }
    }
}
