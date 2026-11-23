using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NED2024.Core;
using NED2024.Interactions;
using NED2024.Temple;

namespace NED2024.UI
{
    /// <summary>In-game HUD: health, score, keys, relics, Echo Lens cooldown, objective, prompts, messages, escape timer and a direction arrow.</summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerInteractor interactor;

        [Header("Health")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Image damageVignette;

        [Header("Items and score")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text relicText;
        [SerializeField] private Image sunKeyIcon;
        [SerializeField] private Image moonKeyIcon;

        [Header("Echo Lens")]
        [SerializeField] private Image scanFill;
        [SerializeField] private Text scanText;
        [SerializeField] private RectTransform navArrow;

        [Header("Mission")]
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text promptText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text escapeText;
        [SerializeField] private Image escapeBar;
        [SerializeField] private GameObject escapePanel;

        private Coroutine messageRoutine;
        private CanvasGroup group;
        private float vignetteAlpha;
        private float nextBeat;
        private float lastTick;

        private void OnEnable()
        {
            if (playerHealth != null) { playerHealth.Changed += OnHealthChanged; playerHealth.Damaged += OnDamaged; }
            if (interactor != null) interactor.PromptChanged += OnPromptChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) { playerHealth.Changed -= OnHealthChanged; playerHealth.Damaged -= OnDamaged; }
            if (interactor != null) interactor.PromptChanged -= OnPromptChanged;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ObjectiveChanged -= OnObjective;
                gm.MessageRequested -= OnMessage;
                gm.ItemsChanged -= RefreshItems;
                gm.ScoreChanged -= OnScore;
                gm.StateChanged -= OnState;
            }
        }

        private void Awake() { group = GetComponent<CanvasGroup>(); }

        private void OnState(GameState state) { if (group != null) group.alpha = (state == GameState.GameOver || state == GameState.Complete) ? 0f : 1f; }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ObjectiveChanged += OnObjective;
                gm.MessageRequested += OnMessage;
                gm.ItemsChanged += RefreshItems;
                gm.ScoreChanged += OnScore;
                gm.StateChanged += OnState;
                OnObjective(gm.Stage, GameManager.ObjectiveText(gm.Stage));
                RefreshItems();
                OnScore(gm.Score);
            }
            if (playerHealth != null) OnHealthChanged(playerHealth.Current, playerHealth.Max);
            if (promptText != null) promptText.text = "";
            if (messageText != null) messageText.text = "";
            if (escapePanel != null) escapePanel.SetActive(false);
            if (navArrow != null) navArrow.gameObject.SetActive(false);
            if (damageVignette != null) SetAlpha(damageVignette, 0f);
        }

        private static void SetAlpha(Image img, float a) { var c = img.color; c.a = a; img.color = c; }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Damage / low health vignette
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, 1.2f * Time.deltaTime);
            float low = playerHealth != null ? Mathf.Clamp01((35f - playerHealth.Current) / 35f) : 0f;
            if (damageVignette != null)
                SetAlpha(damageVignette, Mathf.Max(vignetteAlpha, low * (0.35f + 0.15f * Mathf.Sin(Time.time * 6f))));
            if (low > 0.2f && playerHealth != null && !playerHealth.IsDead && Time.time >= nextBeat && gm.State == GameState.Playing)
            {
                nextBeat = Time.time + Mathf.Lerp(1.4f, 0.7f, low);
                Sfx.Play("heartbeat", 0.6f);
            }

            // Echo lens cooldown ring
            var lens = EchoLens.Instance;
            if (lens != null && scanFill != null)
            {
                scanFill.fillAmount = 1f - lens.CooldownFraction;
                scanFill.color = lens.Ready ? new Color(0.4f, 0.95f, 1f, 1f) : new Color(0.4f, 0.5f, 0.6f, 0.8f);
                if (scanText != null) scanText.text = lens.Ready ? "Q" : Mathf.CeilToInt(lens.CooldownFraction * lens.Cooldown).ToString();
            }

            // Direction arrow while the lens is active
            if (navArrow != null)
            {
                var tracker = ObjectiveTracker.Instance;
                bool show = lens != null && lens.Active && tracker != null && tracker.Current != null && gm.PlayerTransform != null && Camera.main != null;
                navArrow.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 to = tracker.Current.position - gm.PlayerTransform.position; to.y = 0f;
                    float angle = Vector3.SignedAngle(Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f) * Vector3.forward, to, Vector3.up);
                    navArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
                }
            }

            // Escape countdown
            if (escapePanel != null)
            {
                bool escaping = gm.EscapeActive;
                if (escapePanel.activeSelf != escaping) escapePanel.SetActive(escaping);
                if (escaping)
                {
                    float left = Mathf.Max(0f, gm.EscapeTimeLeft);
                    escapeText.text = "THE TEMPLE SEALS IN  " + Mathf.FloorToInt(left / 60f).ToString("00") + ":" + Mathf.FloorToInt(left % 60f).ToString("00");
                    if (escapeBar != null) escapeBar.fillAmount = gm.EscapeTimeTotal > 0f ? left / gm.EscapeTimeTotal : 0f;
                    escapeText.color = left < 30f ? Color.Lerp(new Color(1f, 0.25f, 0.2f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 8f)) : new Color(1f, 0.82f, 0.35f);
                    if (left < 20f && Mathf.FloorToInt(left) != Mathf.FloorToInt(lastTick)) Sfx.Play("tick", 0.5f);
                    lastTick = left;
                }
            }
        }

        // ---------------------------------------------------------------- events

        private void OnHealthChanged(float current, float max)
        {
            if (healthFill != null) healthFill.fillAmount = max > 0f ? current / max : 0f;
            if (healthText != null) healthText.text = Mathf.CeilToInt(current).ToString();
            if (healthFill != null) healthFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.35f, 0.95f, 0.75f), current / max);
        }

        private void OnDamaged(DamageInfo info)
        {
            vignetteAlpha = Mathf.Clamp01(0.45f + info.Amount / 60f);
        }

        private void OnScore(int score) { if (scoreText != null) scoreText.text = score.ToString("N0"); }

        private void RefreshItems()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (relicText != null) relicText.text = gm.RelicsFound + " / " + gm.RelicsTotal;
            if (sunKeyIcon != null) SetAlpha(sunKeyIcon, gm.HasSunKey ? 1f : 0.18f);
            if (moonKeyIcon != null) SetAlpha(moonKeyIcon, gm.HasMoonKey ? 1f : 0.18f);
        }

        private void OnObjective(MissionStage stage, string text) { if (objectiveText != null) objectiveText.text = text; }

        private void OnPromptChanged(string prompt) { if (promptText != null) promptText.text = prompt ?? ""; }

        private void OnMessage(string text, float seconds)
        {
            if (messageText == null) return;
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(ShowMessage(text, seconds));
        }

        private IEnumerator ShowMessage(string text, float seconds)
        {
            messageText.text = text;
            var c = messageText.color; c.a = 1f; messageText.color = c;
            yield return new WaitForSeconds(seconds);
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime * 2f;
                c.a = Mathf.Max(0f, t); messageText.color = c;
                yield return null;
            }
            messageText.text = "";
        }
    }
}
