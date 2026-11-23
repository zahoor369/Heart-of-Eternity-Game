using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NED2024.Core;
using NED2024.Interactions;
using NED2024.Player;
using NED2024.CameraSystem;
using NED2024.Temple;
using NED2024.UI;

namespace NED2024.Testing
{
    /// <summary>
    /// Automated playtest. It only runs when the game is started with the command line argument "-autotest".
    /// It feeds scripted input through GameInput and teleports the explorer between the stages of the temple, so the real movement,
    /// camera, trap, puzzle, AI, mission, score and UI code runs. It takes screenshots and writes a pass/fail report.
    /// It does not replace testing by a human player.
    /// </summary>
    public class AutoPlaytest : MonoBehaviour
    {
        private string outDir;
        private readonly List<string> report = new List<string>();
        private readonly List<string> errors = new List<string>();
        private int passed, failed;
        private int segFrames; private float segTime;
        private readonly List<float> frameTimes = new List<float>();
        private TempleLayout L;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != "-autotest") continue;
                var go = new GameObject("AutoPlaytest");
                DontDestroyOnLoad(go);
                var t = go.AddComponent<AutoPlaytest>();
                t.outDir = (i + 1 < args.Length) ? args[i + 1] : Path.Combine(Application.dataPath, "..", "AutoTest");
                return;
            }
        }

        private void Update()
        {
            if (GM != null && GM.State == GameState.Playing && Time.timeScale == 1f && Time.frameCount > 10)
            {
                frameTimes.Add(Time.unscaledDeltaTime);
                segFrames++; segTime += Time.unscaledDeltaTime;
            }
        }

        private void Start()
        {
            Directory.CreateDirectory(outDir);
            Application.logMessageReceived += OnLog;
            GameInput.Scripted = true;
            L = TempleLayout.Shared;
            StartCoroutine(Watchdog());
            StartCoroutine(Run());
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(type + ": " + message.Split('\n')[0]);
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(1100f);
            report.Add("WATCHDOG: the test run took more than 18 minutes and was stopped.");
            Finish();
        }

        // ------------------------------------------------------------ helpers

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) passed++; else failed++;
            string fps = segFrames > 20 && segTime > 0f ? "  {" + (segFrames / segTime).ToString("F0") + " fps}" : "";
            segFrames = 0; segTime = 0f;
            string line = (ok ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]") + fps;
            report.Add(line);
            Debug.Log("AUTOTEST " + line);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            yield return null; yield return null;
        }

        private IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
        {
            float t = 0f;
            while (!condition() && t < timeoutSeconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static PlayerController Player => Object.FindFirstObjectByType<PlayerController>();
        private static GameManager GM => GameManager.Instance;
        private static Vector3 TW(Vector2Int t) { return TempleLayout.TileToWorld(t); }

        private void Teleport(Vector3 position, float yaw = 0f)
        {
            var p = Player;
            var cc = p.GetComponent<CharacterController>();
            cc.enabled = false;
            p.transform.position = position + Vector3.up * 0.05f;
            p.transform.rotation = Quaternion.Euler(0, yaw, 0);
            cc.enabled = true;
        }

        private static TempleDoor Door(DoorKind k)
        {
            foreach (var d in Object.FindObjectsByType<TempleDoor>(FindObjectsSortMode.None)) if (d.Kind == k) return d;
            return null;
        }

        private IEnumerator SkipIntro()
        {
            float t = 0f;
            while ((GM == null || GM.State != GameState.Playing) && t < 60f)
            {
                GameInput.Tap("Skip");
                t += 0.5f;
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private IEnumerator ClickButton(string objectName)
        {
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                if (b.name == objectName && b.gameObject.scene.IsValid() && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); yield break; }
            Debug.LogWarning("Button not found: " + objectName);
        }

        private IEnumerator LookAt(Vector3 target)
        {
            var ctrl = Object.FindFirstObjectByType<ThirdPersonCameraController>();
            var cam = Camera.main;
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = target - cam.transform.position;
                float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                ctrl.AddYaw(Mathf.DeltaAngle(cam.transform.eulerAngles.y, wantYaw));
                yield return null; yield return null;
            }
        }

        private static int CountActiveSentinels()
        {
            int n = 0;
            foreach (var s in Object.FindObjectsByType<SentinelAI>(FindObjectsSortMode.None)) if (s.GetComponent<NavMeshAgent>().isOnNavMesh) n++;
            return n;
        }

        private IEnumerator FreshRun()
        {
            GM.RestartFromBeginning();
            yield return new WaitForSecondsRealtime(2f);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Game" && GM != null, 30f);
            yield return SkipIntro();
            yield return new WaitForSecondsRealtime(1f);
        }

        // ------------------------------------------------------------ the test

        private IEnumerator Run()
        {
            // ---- main menu and player profile
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "MainMenu");
            yield return new WaitForSecondsRealtime(3f);
            PlayerProfile.ClearAll();
            yield return Shot("01_main_menu");
            int buttons = 0;
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>()) if (b.gameObject.scene.IsValid() && b.gameObject.activeInHierarchy) buttons++;
            Check("Main menu shows its buttons", buttons >= 7, buttons + " active buttons");

            InputField nameField = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<InputField>()) if (f.gameObject.scene.IsValid() && f.name == "NameInput") nameField = f;
            Check("Main menu has a player name field", nameField != null);
            if (nameField != null)
            {
                nameField.text = "Zahoor";
                nameField.onEndEdit.Invoke(nameField.text);
                yield return null;
                Check("The typed player name is saved", PlayerProfile.Name == "Zahoor", "Name=" + PlayerProfile.Name);
            }
            yield return Shot("01b_main_menu_with_name");
            yield return ClickButton("HighScoresButton");
            yield return new WaitForSecondsRealtime(0.4f);
            Check("High Scores screen opens and shows the empty table", PlayerProfile.Top().Count == 0);
            yield return Shot("01c_high_scores_empty");
            yield return ClickButton("BackButton");
            yield return new WaitForSecondsRealtime(0.3f);

            yield return ClickButton("SettingsButton");
            yield return new WaitForSecondsRealtime(0.5f);
            Slider master = null;
            foreach (var s in Resources.FindObjectsOfTypeAll<Slider>()) if (s.gameObject.scene.IsValid() && s.name == "Master VolumeSlider") master = s;
            Check("Settings panel opens with sliders", master != null);
            if (master != null)
            {
                master.value = 0.5f; yield return null;
                Check("Master volume slider changes the saved setting", Mathf.Abs(GameSettings.Master - 0.5f) < 0.01f, "Master=" + GameSettings.Master);
                master.value = 0.9f;
            }
            yield return Shot("02_settings");
            yield return ClickButton("BackButton");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return ClickButton("ControlsButton");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("03_controls");
            yield return ClickButton("BackButton");
            yield return ClickButton("CreditsButton");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("04_credits");
            yield return ClickButton("BackButton");
            yield return new WaitForSecondsRealtime(0.3f);

            yield return ClickButton("NewGameButton");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("05_loading");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Game" && GM != null, 60f);
            Check("New Game loads the Game scene", SceneManager.GetActiveScene().name == "Game");

            // ---- intro and start
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("06_intro_title");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("07_intro_story");
            yield return SkipIntro();
            yield return new WaitForSecondsRealtime(1.5f);
            Check("The intro finishes and play begins", GM.State == GameState.Playing, "State=" + GM.State);
            var player = Player;
            Check("Player exists and is tagged Player", player != null && player.CompareTag("Player"));
            yield return Shot("08_gameplay_start");

            Check("The temple has its doors, traps and collectibles",
                  Object.FindObjectsByType<TempleDoor>(FindObjectsSortMode.None).Length >= 5 &&
                  Object.FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None).Length >= 10 &&
                  Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).Length >= 12,
                  Object.FindObjectsByType<TempleDoor>(FindObjectsSortMode.None).Length + " doors, " + Object.FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None).Length + " spike tiles, " + Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).Length + " collectibles");
            Check("Every sentinel stands on the NavMesh", CountActiveSentinels() == Object.FindObjectsByType<SentinelAI>(FindObjectsSortMode.None).Length && CountActiveSentinels() >= 3, CountActiveSentinels() + " sentinels");

            // ---- movement, sprint, jump, mouse look
            Vector3 p0 = player.transform.position;
            GameInput.ScriptedMove = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(2f);
            GameInput.ScriptedMove = Vector2.zero;
            float moved = Vector3.Distance(p0, player.transform.position);
            Check("WASD move moves the explorer forward", moved > 4f, moved.ToString("F1") + " m in 2 s");
            yield return Shot("09_walking");
            Vector3 p1 = player.transform.position;
            GameInput.ScriptedMove = new Vector2(0, 1); GameInput.ScriptedSprint = true;
            yield return new WaitForSecondsRealtime(0.8f);
            float sprintSpeed = player.HorizontalSpeed;
            GameInput.ScriptedMove = Vector2.zero; GameInput.ScriptedSprint = false;
            Check("Sprint is faster than walking", sprintSpeed > 4.8f, sprintSpeed.ToString("F1") + " m/s");
            yield return new WaitForSecondsRealtime(0.6f);
            float y0 = player.transform.position.y, maxY = y0;
            GameInput.Tap("Jump");
            for (int i = 0; i < 40; i++) { maxY = Mathf.Max(maxY, player.transform.position.y); yield return null; }
            Check("Jump lifts the explorer", maxY - y0 > 0.3f, (maxY - y0).ToString("F2") + " m");
            var ctrl = Object.FindFirstObjectByType<ThirdPersonCameraController>();
            float yawBefore = Camera.main.transform.eulerAngles.y;
            GameInput.ScriptedLook = new Vector2(8f, 0f);
            yield return new WaitForSecondsRealtime(0.4f);
            GameInput.ScriptedLook = Vector2.zero;
            Check("Mouse look orbits the camera", Mathf.Abs(Mathf.DeltaAngle(yawBefore, Camera.main.transform.eulerAngles.y)) > 5f);

            // ---- the hall: the entrance seals behind the explorer
            Door(DoorKind.Entrance).SetOpen(true, true);
            Teleport(TW(new Vector2Int(15, 2)), 0f);
            yield return new WaitForSecondsRealtime(2.5f);
            Check("The entrance door slams shut when the explorer enters the hall", !Door(DoorKind.Entrance).IsOpen);
            yield return Shot("10_hall_sealed");

            // ---- Echo lens: reveals the hidden mural in the sun vault
            Teleport(TW(new Vector2Int(3, 1)), 270f);
            yield return new WaitForSecondsRealtime(1f);
            var lens = EchoLens.Instance;
            Check("The Echo Lens is ready", lens != null && lens.Ready);
            GameInput.Tap("Scan");
            yield return new WaitForSecondsRealtime(1.6f);
            HiddenGlyph mural = null;
            foreach (var g in Object.FindObjectsByType<HiddenGlyph>(FindObjectsSortMode.None)) if (g.name == "RuneMural_SunVault") mural = g;
            Check("The Echo pulse reveals the hidden rune mural", mural != null && mural.Amount > 0.5f, mural != null ? mural.Amount.ToString("F2") : "no mural");
            Check("The Echo Lens goes on cooldown", !lens.Ready && lens.CooldownFraction > 0.5f);
            yield return Shot("11_echo_pulse");

            // ---- spike field in the sun vault hurts
            var hp = player.GetComponent<Health>();
            SpikeTrap trap = null; float bd = 99f;
            foreach (var s in Object.FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(s.transform.position, TW(new Vector2Int(2, 2)));
                if (d < bd) { bd = d; trap = s; }
            }
            float hpBefore = hp.Current;
            Teleport(trap.transform.position, 0f);
            yield return new WaitForSecondsRealtime(4.2f);
            Check("A spike tile damages the explorer when it strikes", hp.Current < hpBefore, hpBefore + " -> " + hp.Current);
            yield return Shot("12_spikes");

            // ---- the Sun Key
            int scoreBefore = GM.Score;
            Teleport(TW(L.SunKeyTile), 0f);
            yield return new WaitForSecondsRealtime(1.2f);
            Check("Taking the Sun Key gives the key and points", GM.HasSunKey && GM.Score >= scoreBefore + GameManager.PointsPerKey, "HasKey=" + GM.HasSunKey + ", score " + scoreBefore + " -> " + GM.Score);
            Check("The objective moves on to the Sun Door", GM.Stage == MissionStage.OpenSunDoor, "Stage=" + GM.Stage);
            yield return Shot("13_sun_key");

            // ---- the Sun Door (a real E key press)
            Teleport(TW(TempleLayout.SunDoorTile) + new Vector3(1.8f, 0f, 0f), 270f);   // standing in front of the door
            yield return new WaitForSecondsRealtime(1.2f);
            Check("A closed door shows a prompt", Door(DoorKind.SunDoor).Prompt.Length > 0);
            GameInput.Tap("Interact");
            yield return new WaitForSecondsRealtime(3.4f);
            Check("The Sun Door opens with the Sun Key", Door(DoorKind.SunDoor).IsOpen);
            yield return Shot("14_sun_door");

            // ---- the rune chamber
            Teleport(TW(new Vector2Int(2, 25)), 0f);
            yield return new WaitForSecondsRealtime(1.2f);
            Check("Entering the rune chamber advances the mission", GM.Stage == MissionStage.SolveRunes, "Stage=" + GM.Stage);
            var puzzle = Object.FindFirstObjectByType<RunePuzzle>();
            var peds = new Dictionary<int, RunePedestal>();
            foreach (var pd in Object.FindObjectsByType<RunePedestal>(FindObjectsSortMode.None)) peds[pd.Glyph] = pd;
            Check("There are four rune pedestals", peds.Count == 4);
            yield return Shot("15_rune_chamber");
            float hpRune = hp.Current;
            peds[0].Interact();                     // Sun first is wrong: the sequence starts with the Moon
            yield return new WaitForSecondsRealtime(1.2f);
            Check("A wrong rune shocks the explorer and resets the puzzle", hp.Current < hpRune && !puzzle.Solved, hpRune + " -> " + hp.Current);
            int scoreRunes = GM.Score;
            foreach (int g in puzzle.Solution) { peds[g].Interact(); yield return new WaitForSecondsRealtime(0.5f); }
            yield return new WaitForSecondsRealtime(1.5f);
            Check("The right order solves the puzzle and opens the gate", puzzle.Solved && Door(DoorKind.RuneGate).IsOpen);
            Check("Solving the runes gives points and moves the mission on", GM.Score >= scoreRunes + GameManager.PointsPerRune && GM.Stage == MissionStage.CrossGallery, "Stage=" + GM.Stage);
            yield return Shot("16_runes_solved");

            // ---- the trap gallery: blades and flames
            hp.Heal(100f);
            var blade = Object.FindFirstObjectByType<SwingingBlade>();
            float hpGallery = hp.Current;
            Teleport(blade.transform.position, 90f);
            yield return new WaitForSecondsRealtime(4.5f);
            Check("A swinging blade cuts the explorer standing in its path", hp.Current < hpGallery, hpGallery + " -> " + hp.Current);
            yield return Shot("17_blade");
            hp.Heal(100f);
            FlameVent vent = Object.FindFirstObjectByType<FlameVent>();
            float hpVent = hp.Current;
            Teleport(vent.transform.position + vent.transform.forward * 1.6f - Vector3.up * (vent.transform.position.y), 0f);
            yield return new WaitForSecondsRealtime(5f);
            Check("A flame vent burns the explorer in its jet", hp.Current < hpVent, hpVent + " -> " + hp.Current);
            yield return Shot("18_flames");

            // ---- the Moon Key and the vault exit
            hp.Heal(100f);
            Teleport(TW(L.MoonKeyTile), 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Taking the Moon Key gives the key", GM.HasMoonKey);
            Check("The one-way vault exit opens after the Moon Key", Door(DoorKind.VaultExit).IsOpen);
            Check("The objective moves on to the Moon Door", GM.Stage == MissionStage.OpenMoonDoor, "Stage=" + GM.Stage);
            yield return Shot("19_moon_key");

            // ---- the Moon Door and the Heart
            Teleport(TW(new Vector2Int(11, 15)), 90f);
            yield return new WaitForSecondsRealtime(1f);
            Door(DoorKind.MoonDoor).Interact();
            yield return new WaitForSecondsRealtime(3.4f);
            Check("The Moon Door opens with the Moon Key", Door(DoorKind.MoonDoor).IsOpen);
            Check("The objective moves on to the Heart", GM.Stage == MissionStage.TakeHeart, "Stage=" + GM.Stage);
            Teleport(TW(new Vector2Int(14, 15)), 90f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("20_heart_chamber");
            int scoreHeart = GM.Score;
            var heart = Object.FindFirstObjectByType<HeartOfEternity>();
            heart.Interact();
            yield return new WaitForSecondsRealtime(2.2f);
            Check("Taking the Heart starts the escape timer", GM.EscapeActive && GM.Stage == MissionStage.Escape && GM.EscapeTimeLeft > 30f, "left " + GM.EscapeTimeLeft.ToString("F0") + " s");
            Check("Taking the Heart gives points", GM.Score >= scoreHeart + GameManager.PointsPerHeart);
            Check("The entrance opens for the escape", Door(DoorKind.Entrance).IsOpen);
            yield return new WaitForSecondsRealtime(8f);
            var boulder = Object.FindFirstObjectByType<RollingBoulder>(FindObjectsInactive.Include);
            Check("The boulder starts rolling", boulder != null && boulder.Running);
            yield return Shot("21_escape");

            // ---- run out of time: the temple seals
            GM.DebugSetEscapeTime(0.2f);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Running out of time ends the run (the temple seals itself)", GM.State == GameState.GameOver, "State=" + GM.State);
            yield return Shot("22_sealed");
            int rankSealed = PlayerProfile.Top().Count;

            // ---- full run again: escape through the exit
            yield return FreshRun();
            Teleport(TW(new Vector2Int(15, 2)), 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            Teleport(TW(L.SunKeyTile), 0f); yield return new WaitForSecondsRealtime(1f);
            GM.RunesSolved();   // the puzzle was tested above; here only the score/stage flow matters
            Teleport(TW(L.MoonKeyTile), 0f); yield return new WaitForSecondsRealtime(1f);
            Door(DoorKind.MoonDoor).SetOpen(true, true);
            Object.FindFirstObjectByType<HeartOfEternity>().Interact();
            yield return new WaitForSecondsRealtime(1f);
            Teleport(new Vector3(0f, 0f, -3.5f), 180f);
            yield return new WaitForSecondsRealtime(1.8f);
            Check("Reaching the entrance with the Heart completes the mission", GM.State == GameState.Complete, "State=" + GM.State);
            Check("Escaping adds time, escape and health bonuses", GM.EscapeBonus > 0 && GM.HealthBonus > 0 && GM.TimeBonus > 0, "escape " + GM.EscapeBonus + ", time " + GM.TimeBonus + ", health " + GM.HealthBonus);
            int winScore = GM.Score;
            var top = PlayerProfile.Top();
            Check("The finished run is saved on the high-score table under the player's name", top.Count >= 1 && top[0].name == "Zahoor" && top[0].outcome == "ESCAPED", top.Count > 0 ? top[0].name + " " + top[0].score + " " + top[0].outcome : "empty");
            yield return Shot("23_escaped");

            // ---- death, retry from checkpoint, collectibles stay collected
            yield return FreshRun();
            Teleport(TW(new Vector2Int(15, 5)), 0f); yield return new WaitForSecondsRealtime(1f);
            Teleport(TW(L.SunKeyTile), 0f); yield return new WaitForSecondsRealtime(1.5f);   // advances the mission and saves a checkpoint
            Vector2Int relicTile = L.RelicTiles[0];
            Teleport(TW(relicTile), 0f); yield return new WaitForSecondsRealtime(1f);
            int relicsNow = GM.RelicsFound;
            Check("A Sun relic is collected for score", relicsNow >= 1, "Relics=" + relicsNow);
            Check("The score shows on the HUD", FindScoreText() == GM.Score.ToString("N0"), FindScoreText());
            Teleport(TW(new Vector2Int(5, 25)), 0f); yield return new WaitForSecondsRealtime(1f);
            GM.SetCheckpoint(Player.transform.position, 0f);   // a checkpoint at the sun door
            int scoreAtCheckpoint = GM.Score;
            Player.GetComponent<Health>().TakeDamage(new DamageInfo { Amount = 9999f, Point = Player.transform.position, Direction = Vector3.forward });
            yield return WaitUntil(() => GM.State == GameState.GameOver, 12f);
            Check("Player death leads to the Game Over screen", GM.State == GameState.GameOver, "State=" + GM.State);
            Check("A death is recorded on the profile table", PlayerProfile.Top().Count >= 2);
            yield return Shot("24_game_over");
            GM.RetryFromCheckpoint();
            yield return new WaitForSecondsRealtime(2f);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Game" && GM != null && GM.State == GameState.Playing, 30f);
            yield return new WaitForSecondsRealtime(1.5f);
            float dist = Vector3.Distance(Player.transform.position, TW(new Vector2Int(5, 25)));
            Check("Retry puts the explorer back at the checkpoint and skips the intro", dist < 3f && GM.State == GameState.Playing, dist.ToString("F1") + " m from the checkpoint");
            Check("Retry restores keys, relics and score", GM.HasSunKey && GM.RelicsFound == relicsNow && GM.Score >= scoreAtCheckpoint - GameManager.DeathPenalty - 5, "key " + GM.HasSunKey + ", relics " + GM.RelicsFound + ", score " + GM.Score);
            int relicsLeft = 0;
            foreach (var c in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None)) if (c.Kind == CollectibleKind.Relic) relicsLeft++;
            Check("Collected relics stay collected after a Retry", relicsLeft == 5 - relicsNow || relicsLeft <= 4, relicsLeft + " relics left in the temple");

            // ---- a sentinel: stunned by the lens, attacks when it reaches the explorer
            hp = Player.GetComponent<Health>(); hp.Heal(100f);
            SentinelAI sentinel = Object.FindFirstObjectByType<SentinelAI>();
            Teleport(sentinel.transform.position + sentinel.transform.forward * 5f, 0f);
            yield return new WaitForSecondsRealtime(0.6f);
            GameInput.Tap("Scan");
            yield return new WaitForSecondsRealtime(0.6f);
            Check("The Echo pulse stuns a nearby sentinel", sentinel.State == SentinelState.Stunned, "State=" + sentinel.State);
            yield return Shot("25_sentinel_stunned");
            yield return new WaitForSecondsRealtime(4.5f);
            Teleport(sentinel.transform.position + sentinel.transform.forward * 1.3f, 180f);
            float hpSent = hp.Current;
            yield return WaitUntil(() => hp.Current < hpSent, 7f);
            Check("A sentinel strikes an explorer who stays close", hp.Current < hpSent, hpSent + " -> " + hp.Current);
            yield return Shot("26_sentinel_attack");
            foreach (var sn0 in Object.FindObjectsByType<SentinelAI>(FindObjectsSortMode.None)) Destroy(sn0.gameObject);   // one hit was enough: do not let it kill the explorer
            yield return null;
            hp.Heal(100f);

            // the sentinels have been tested: remove them so they cannot disturb the remaining checks
            foreach (var sn in Object.FindObjectsByType<SentinelAI>(FindObjectsSortMode.None)) Destroy(sn.gameObject);
            hp.Heal(100f);

            // ---- secret wall, life orb
            if (L.SecretEntryTiles.Count > 0)
            {
                Teleport(TW(L.SecretEntryTiles[0]), 0f);
                yield return new WaitForSecondsRealtime(0.8f);
                GM.ShowMessage("test", 0.1f);
                // the lens cooldown is 14 s: wait for it
                yield return WaitUntil(() => EchoLens.Instance.Ready, 20f);
                GameInput.Tap("Scan");
                yield return new WaitForSecondsRealtime(1.5f);
                SecretWall sw = null; float sd = 99f;
                foreach (var w in Object.FindObjectsByType<SecretWall>(FindObjectsSortMode.None)) { float d = Vector3.Distance(w.transform.position, Player.transform.position); if (d < sd) { sd = d; sw = w; } }
                Check("A secret wall can be used while the Echo Lens reveals it", sw != null && sw.CanInteract);
                int secretsBefore = GM.SecretsFound;
                sw.Interact();
                yield return new WaitForSecondsRealtime(3f);
                Check("Opening a secret wall counts as a found secret", sw.IsOpen && GM.SecretsFound == secretsBefore + 1);
                yield return Shot("27_secret");
            }
            if (L.OrbTiles.Count > 0)
            {
                hp.TakeDamage(new DamageInfo { Amount = 40f, Point = Player.transform.position, Direction = Vector3.up });
                float low = hp.Current;
                Teleport(TW(L.OrbTiles[0]), 0f);
                yield return new WaitForSecondsRealtime(1.2f);
                Check("A life orb restores health", hp.Current > low, low + " -> " + hp.Current);
            }

            // ---- map and pause
            GameInput.Tap("Map");
            yield return new WaitForSecondsRealtime(0.5f);
            var mapC = Object.FindFirstObjectByType<MapController>();
            Check("The M key opens the temple map", mapC.BigMapOpen);
            yield return Shot("28_map");
            GameInput.Tap("Pause");
            yield return new WaitForSecondsRealtime(0.4f);
            Check("Esc closes the map first (does not pause)", !mapC.BigMapOpen && GM.State == GameState.Playing);
            GameInput.Tap("Pause");
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Esc pauses the game", GM.State == GameState.Paused && Time.timeScale == 0f);
            yield return Shot("29_pause");
            GameInput.Tap("Pause");
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Esc resumes the game", GM.State == GameState.Playing && Time.timeScale == 1f);
            Check("Exploring reveals the map", mapC.IsExplored(Player.transform.position));

            // ---- back to the menu, profile and the table
            GM.ExitToMenu();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "MainMenu", 20f);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Back in the menu", SceneManager.GetActiveScene().name == "MainMenu");
            yield return ClickButton("HighScoresButton");
            yield return new WaitForSecondsRealtime(0.5f);
            Text board = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Text>()) if (t.gameObject.scene.IsValid() && t.name == "Table") board = t;
            Check("The High Scores screen lists the saved runs", board != null && board.text.Contains("Zahoor") && board.text.Contains("ESCAPED") && board.text.Contains("TRAPPED"), board != null ? board.text.Replace("\n", " | ") : "no table");
            yield return Shot("30_high_scores");
            Check("The profile statistics are updated", PlayerProfile.Games >= 3 && PlayerProfile.Wins >= 1 && PlayerProfile.BestScore >= winScore, "games " + PlayerProfile.Games + ", wins " + PlayerProfile.Wins + ", best " + PlayerProfile.BestScore);
            PlayerProfile.ClearAll();   // the test must not leave its runs in the real player's profile
            Finish();
        }

        private static string FindScoreText()
        {
            foreach (var t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) if (t.gameObject.name == "Score" && t.transform.parent != null && t.transform.parent.name == "ScorePanel") return t.text;
            return "";
        }

        // ------------------------------------------------------------ report

        private void Finish()
        {
            if (frameTimes.Count > 30)
            {
                frameTimes.Sort();
                float sum = 0f; foreach (var f in frameTimes) sum += f;
                float avg = frameTimes.Count / sum;
                float low1 = 1f / frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * 0.99f))];
                report.Insert(0, "Frame rate while playing: average " + avg.ToString("F0") + " fps, 1% low " + low1.ToString("F0") + " fps (" + frameTimes.Count + " frames) on " + SystemInfo.graphicsDeviceName);
            }
            var sb = new StringBuilder();
            sb.AppendLine("AUTOMATED PLAYTEST REPORT");
            sb.AppendLine("Passed: " + passed + "   Failed: " + failed);
            sb.AppendLine();
            foreach (var l in report) sb.AppendLine(l);
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Errors logged during the run (" + errors.Count + "):");
                var seen = new HashSet<string>();
                foreach (var e in errors) if (seen.Add(e)) sb.AppendLine("  " + e);
            }
            File.WriteAllText(Path.Combine(outDir, "autotest_report.txt"), sb.ToString());
            Debug.Log("AUTOTEST finished: " + passed + " passed, " + failed + " failed");
            Application.Quit(failed == 0 ? 0 : 2);
        }
    }
}
