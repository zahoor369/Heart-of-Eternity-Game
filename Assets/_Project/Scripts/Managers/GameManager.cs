using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NED2024.Core
{
    public enum GameState { Intro, Playing, Paused, GameOver, Complete }

    /// <summary>The steps of the escape. Each step has its own objective text and map marker.</summary>
    public enum MissionStage
    {
        FindSunKey = 0,     // explore the maze and take the Sun Key from its vault
        OpenSunDoor = 1,    // carry it to the Sun Door
        SolveRunes = 2,     // wake the four rune pedestals in the right order
        CrossGallery = 3,   // cross the Trap Gallery and take the Moon Key
        OpenMoonDoor = 4,   // use it on the Moon Door
        TakeHeart = 5,      // take the Heart of Eternity
        Escape = 6,         // run back to the entrance before the temple seals itself
        Done = 7
    }

    /// <summary>
    /// Owns the game state, mission progress, score, the escape countdown, pause, game over and completion.
    /// One instance lives in the Game scene. Other scripts listen to its events instead of polling it.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const string CheckpointKey = "checkpoint_stage";
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        // Scoring (points)
        public const int PointsPerRelic = 150, PointsPerKey = 200, PointsPerRune = 300, PointsPerStage = 150, PointsPerOrb = 10,
                         PointsPerSecret = 100, PointsPerStun = 25, PointsPerHeart = 500, TimeBonusBudget = 1200, PointsPerHealth = 4,
                         PointsPerEscapeSecond = 5, PerfectRunBonus = 500, DeathPenalty = 100;

        /// <summary>Set by the main menu so the Game scene knows whether to load the saved checkpoint.</summary>
        public static bool ContinueRequested;

        public static GameManager Instance { get; private set; }

        [SerializeField] private ProgressSaver progress;
        [SerializeField] private int relicsTotal = 5;
        [SerializeField] private float escapeSecondsSetting = 150f;

        /// <summary>An overlay (the full map) can register here so Esc closes it instead of pausing the game.</summary>
        public static Func<bool> CloseOverlay;

        public GameState State { get; private set; } = GameState.Intro;
        public MissionStage Stage { get; private set; } = MissionStage.FindSunKey;
        public bool HasSunKey { get; private set; }
        public bool HasMoonKey { get; private set; }
        public int RelicsFound { get; private set; }
        public int RelicsTotal { get; set; } = 5;
        public int SecretsFound { get; private set; }
        public int Deaths { get; private set; }
        public float PlayTime { get; private set; }
        public Transform PlayerTransform { get; private set; }

        public int Score { get; private set; }
        public int TimeBonus { get; private set; }
        public int HealthBonus { get; private set; }
        public int EscapeBonus { get; private set; }
        public int PerfectBonus { get; private set; }
        /// <summary>Position on the high-score table after the run ended (0 = not on the table).</summary>
        public int FinalRank { get; private set; }
        public string EndReason { get; private set; } = "";

        // Escape countdown
        public bool EscapeActive { get; private set; }
        public float EscapeTimeLeft { get; private set; }
        public float EscapeTimeTotal { get; private set; }
        public float EscapeSeconds { get; set; } = 150f;

        // Where the player respawns after dying (set by checkpoints)
        public Vector3 CheckpointPosition { get; private set; }
        public float CheckpointYaw { get; private set; }
        public bool HasCheckpointPosition { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<MissionStage, string> ObjectiveChanged;
        public event Action<string, float> MessageRequested;
        public event Action ItemsChanged;
        public event Action<int> ScoreChanged;
        public event Action<float> EscapeTick;
        public event Action HeartTaken;

        public static bool HasSavedCheckpoint => PlayerPrefs.GetInt(CheckpointKey, 0) > 0;

        /// <summary>True when this run was started with Continue / Retry, so the intro is skipped.</summary>
        public bool ResumedFromCheckpoint { get; private set; }

        private void Awake()
        {
            Instance = this;
            RelicsTotal = relicsTotal;
            EscapeSeconds = escapeSecondsSetting;
            Time.timeScale = 1f;
            ResumedFromCheckpoint = ContinueRequested && HasSavedCheckpoint;
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; CloseOverlay = null; }
            Time.timeScale = 1f;
        }

        private void Start()
        {
            AudioManager.Instance.PlayMusic(Sfx.Get("music_game"));
            if (ResumedFromCheckpoint)
            {
                int saved = PlayerPrefs.GetInt(CheckpointKey, 0);
                Stage = (MissionStage)Mathf.Clamp(saved, 0, (int)MissionStage.TakeHeart);
                if (progress != null) progress.Load(this);
            }
            ContinueRequested = false;
            RaiseObjective();
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                PlayTime += Time.deltaTime;
                if (EscapeActive)
                {
                    EscapeTimeLeft -= Time.deltaTime;
                    EscapeTick?.Invoke(EscapeTimeLeft);
                    if (EscapeTimeLeft <= 0f) { EscapeTimeLeft = 0f; EscapeActive = false; FailRun("The temple sealed itself forever."); }
                }
                if (GameInput.PauseDown)
                {
                    if (CloseOverlay == null || !CloseOverlay()) Pause();
                }
            }
            else if (State == GameState.Paused && GameInput.PauseDown)
            {
                Resume();
            }
        }

        public void RegisterPlayer(Transform player) { PlayerTransform = player; }

        public void SetCheckpoint(Vector3 position, float yaw)
        {
            if (Stage >= MissionStage.Escape) return;   // the run-back is never saved: a retry restarts it from the heart chamber
            CheckpointPosition = position; CheckpointYaw = yaw; HasCheckpointPosition = true;
            if (progress != null) progress.Save();
            PlayerPrefs.Save();
        }

        public void RestoreCheckpoint(Vector3 position, float yaw)
        {
            CheckpointPosition = position; CheckpointYaw = yaw; HasCheckpointPosition = true;
        }

        public void BeginPlay() { SetState(GameState.Playing); }

        public void SetState(GameState newState)
        {
            State = newState;
            bool gameplay = newState == GameState.Playing;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
            Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;
            StateChanged?.Invoke(newState);
        }

        public void Pause() { if (State == GameState.Playing) SetState(GameState.Paused); }
        public void Resume() { if (State == GameState.Paused) SetState(GameState.Playing); }

        public void ShowMessage(string text, float seconds = 3f) { MessageRequested?.Invoke(text, seconds); }

        // ------------------------------------------------------------------ items and score

        public void AddScore(int points)
        {
            if (points <= 0 || State == GameState.GameOver || State == GameState.Complete) return;
            Score += points;
            ScoreChanged?.Invoke(Score);
        }

        public void CollectSunKey()
        {
            HasSunKey = true;
            AddScore(PointsPerKey);
            ItemsChanged?.Invoke();
            if (Stage == MissionStage.FindSunKey) AdvanceTo(MissionStage.OpenSunDoor);
        }

        public void CollectMoonKey()
        {
            HasMoonKey = true;
            AddScore(PointsPerKey);
            ItemsChanged?.Invoke();
            if (Stage <= MissionStage.CrossGallery) AdvanceTo(MissionStage.OpenMoonDoor);
        }

        public void CollectRelic()
        {
            RelicsFound++;
            AddScore(PointsPerRelic);
            ItemsChanged?.Invoke();
        }

        public void FoundSecret()
        {
            SecretsFound++;
            AddScore(PointsPerSecret);
            ItemsChanged?.Invoke();
        }

        public void RunesSolved()
        {
            AddScore(PointsPerRune);
            AdvanceTo(MissionStage.CrossGallery);
        }

        public void EnteredRuneChamber() { if (Stage == MissionStage.OpenSunDoor) AdvanceTo(MissionStage.SolveRunes); }
        public void UsedMoonDoor() { if (Stage == MissionStage.OpenMoonDoor) AdvanceTo(MissionStage.TakeHeart); }

        /// <summary>Restores the counters when progress is loaded after a Retry.</summary>
        public void RestoreProgress(bool sun, bool moon, int relics, int secrets, int deaths, int score)
        {
            HasSunKey = sun; HasMoonKey = moon; RelicsFound = relics; SecretsFound = secrets; Deaths = deaths; Score = score;
            ItemsChanged?.Invoke();
            ScoreChanged?.Invoke(Score);
        }

        /// <summary>The heart is lifted from its pedestal: the temple starts to seal itself and the escape begins.</summary>
        public void TakeHeartOfEternity()
        {
            if (Stage >= MissionStage.Escape) return;
            AddScore(PointsPerHeart);
            AdvanceTo(MissionStage.Escape);
            EscapeActive = true;
            EscapeTimeTotal = EscapeSeconds;
            EscapeTimeLeft = EscapeSeconds;
            HeartTaken?.Invoke();
        }

        /// <summary>Moves the mission forward. Only moves forward, never back.</summary>
        public void AdvanceTo(MissionStage next)
        {
            if (next <= Stage) return;
            Stage = next;
            AddScore(PointsPerStage);
            if (next <= MissionStage.TakeHeart)
            {
                PlayerPrefs.SetInt(CheckpointKey, (int)next + 1);   // +1 so "stage 0" still counts as a saved game
                PlayerPrefs.Save();
                if (progress != null) progress.Save();
            }
            Sfx.Play("rune_ok", 0.5f);
            RaiseObjective();
        }

        public static string ObjectiveText(MissionStage stage)
        {
            switch (stage)
            {
                case MissionStage.FindSunKey: return "Explore the maze and find the Sun Key";
                case MissionStage.OpenSunDoor: return "Take the Sun Key to the Sun Door";
                case MissionStage.SolveRunes: return "Wake the four runes in the right order";
                case MissionStage.CrossGallery: return "Cross the Trap Gallery and take the Moon Key";
                case MissionStage.OpenMoonDoor: return "Open the Moon Door at the heart of the temple";
                case MissionStage.TakeHeart: return "Take the Heart of Eternity";
                case MissionStage.Escape: return "ESCAPE! Follow the golden light to the entrance";
                default: return "You escaped the temple";
            }
        }

        private void RaiseObjective() { ObjectiveChanged?.Invoke(Stage, ObjectiveText(Stage)); }

        // ------------------------------------------------------------------ end of the run

        /// <summary>Test hook: sets the escape timer (used by the automated playtest to check the time-out ending).</summary>
        public void DebugSetEscapeTime(float seconds) { EscapeTimeLeft = seconds; }

        public void PlayerDied()
        {
            Deaths++;
            Score = Mathf.Max(0, Score - DeathPenalty);
            ScoreChanged?.Invoke(Score);
            if (progress != null) progress.NoteDeath(Deaths);
        }

        public void FailRun(string reason)
        {
            if (State == GameState.GameOver || State == GameState.Complete) return;
            EndReason = reason;
            EscapeActive = false;
            Sfx.Play("lose");
            FinalRank = PlayerProfile.Submit(Score, "TRAPPED", RelicsFound, Mathf.RoundToInt(PlayTime));
            SetState(GameState.GameOver);
        }

        public void CompleteMission()
        {
            if (State == GameState.Complete || State == GameState.GameOver) return;
            Stage = MissionStage.Done;
            EscapeBonus = Mathf.RoundToInt(Mathf.Max(0f, EscapeTimeLeft)) * PointsPerEscapeSecond;
            TimeBonus = Mathf.Max(0, TimeBonusBudget - Mathf.RoundToInt(PlayTime));
            var hp = PlayerTransform != null ? PlayerTransform.GetComponent<Health>() : null;
            HealthBonus = hp != null ? Mathf.RoundToInt(hp.Current) * PointsPerHealth : 0;
            PerfectBonus = Deaths == 0 ? PerfectRunBonus : 0;
            AddScore(EscapeBonus + TimeBonus + HealthBonus + PerfectBonus);
            EscapeActive = false;
            EndReason = "You escaped with the Heart of Eternity.";
            PlayerPrefs.DeleteKey(CheckpointKey);
            ProgressSaver.Clear();
            Sfx.Play("win");
            FinalRank = PlayerProfile.Submit(Score, "ESCAPED", RelicsFound, Mathf.RoundToInt(PlayTime));
            SetState(GameState.Complete);
        }

        public void RetryFromCheckpoint()
        {
            ContinueRequested = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameScene);
        }

        public void RestartFromBeginning()
        {
            ClearCheckpoint();
            ContinueRequested = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameScene);
        }

        public void ExitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }

        public static void ClearCheckpoint() { PlayerPrefs.DeleteKey(CheckpointKey); ProgressSaver.Clear(); }
    }
}
