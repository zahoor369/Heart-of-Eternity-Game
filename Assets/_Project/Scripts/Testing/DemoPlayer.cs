using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NED2024.Core;
using NED2024.Player;
using NED2024.CameraSystem;
using NED2024.Temple;

namespace NED2024.Testing
{
    /// <summary>
    /// Plays the whole game by itself for the gameplay video. Only active with the command line argument "-demo".
    /// It uses the real controls (scripted keyboard input): the explorer walks and runs along the maze routes, uses doors, reads the
    /// rune order, crosses the traps (the explorer cannot die in this mode), takes the Heart and runs for the exit.
    /// </summary>
    public class DemoPlayer : MonoBehaviour
    {
        private TempleLayout L;
        private ThirdPersonCameraController cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a != "-demo") continue;
                var go = new GameObject("DemoPlayer");
                DontDestroyOnLoad(go);
                go.AddComponent<DemoPlayer>();
                return;
            }
        }

        private static GameManager GM => GameManager.Instance;
        private static PlayerController Player => Object.FindFirstObjectByType<PlayerController>();
        private static Vector3 TW(Vector2Int t) { return TempleLayout.TileToWorld(t); }

        private void Start()
        {
            GameInput.Scripted = true;
            L = TempleLayout.Shared;
            StartCoroutine(Run());
        }

        private static TempleDoor Door(DoorKind k)
        {
            foreach (var d in Object.FindObjectsByType<TempleDoor>(FindObjectsSortMode.None)) if (d.Kind == k) return d;
            return null;
        }

        private IEnumerator Run()
        {
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "MainMenu");
            yield return new WaitForSecondsRealtime(4f);
            // type the name like a player would
            foreach (var f in Resources.FindObjectsOfTypeAll<InputField>()) if (f.gameObject.scene.IsValid() && f.name == "NameInput") { f.text = "Zahoor"; f.onEndEdit.Invoke(f.text); }
            yield return new WaitForSecondsRealtime(2.5f);
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>()) if (b.name == "NewGameButton" && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); break; }
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Game" && GM != null);
            yield return new WaitForSecondsRealtime(1.5f);
            float t = 0f;
            while (GM.State != GameState.Playing && t < 40f) { t += 0.5f; if (t > 11f) GameInput.Tap("Skip"); yield return new WaitForSecondsRealtime(0.5f); }
            GM.State.ToString();

            cam = Object.FindFirstObjectByType<ThirdPersonCameraController>();
            var hp = Player.GetComponent<Health>();
            hp.GodMode = true;

            // ---- walk into the temple
            yield return WalkWorld(new Vector3(0f, 0f, 1.5f), 4.2f, false);
            yield return new WaitForSecondsRealtime(3.5f);          // the door slams
            yield return Scan(1.5f);

            // ---- the maze to the sun vault, the key
            yield return WalkTiles(new Vector2Int(5, 1), 1.2f);
            yield return Scan(0.5f);
            yield return WalkTiles(new Vector2Int(3, 1), 1.2f, true);
            yield return WalkTiles(new Vector2Int(3, 2), 1.4f, true);
            yield return WalkTiles(new Vector2Int(2, 2), 1.4f, true);
            yield return WalkTiles(new Vector2Int(2, 3), 1.4f, true);
            yield return WalkTiles(L.SunKeyTile, 1.4f, true);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Scan(2.5f);                                  // the hidden mural

            // ---- to the Sun Door
            yield return WalkTiles(new Vector2Int(3, 1), 1.4f, true);
            yield return WalkTiles(new Vector2Int(5, 25), 1.5f);
            yield return WalkWorld(TW(TempleLayout.SunDoorTile) + new Vector3(1.8f, 0f, 0f), 0.8f, false);
            yield return Press(() => Door(DoorKind.SunDoor).IsOpen);
            yield return new WaitForSecondsRealtime(3f);
            yield return WalkTiles(new Vector2Int(3, 25), 1.2f);
            yield return WalkTiles(new Vector2Int(2, 25), 1.0f);
            yield return new WaitForSecondsRealtime(1f);

            // ---- the runes
            var peds = new Dictionary<int, RunePedestal>();
            foreach (var pd in Object.FindObjectsByType<RunePedestal>(FindObjectsSortMode.None)) peds[pd.Glyph] = pd;
            var puzzle = Object.FindFirstObjectByType<RunePuzzle>();
            yield return Scan(2.5f);
            foreach (int g in puzzle.Solution)
            {
                yield return WalkWorld(peds[g].transform.position + (Player.transform.position - peds[g].transform.position).normalized * 1.5f, 0.6f, false);
                peds[g].Interact();
                yield return new WaitForSecondsRealtime(1.6f);
            }
            yield return new WaitForSecondsRealtime(2.5f);

            // ---- the trap gallery
            yield return WalkTiles(new Vector2Int(1, 29), 1.3f);
            for (int x = 2; x <= 11; x++) yield return WalkTiles(new Vector2Int(x, 29), 1.0f, true);
            yield return WalkTiles(L.MoonKeyTile, 1.2f);
            yield return new WaitForSecondsRealtime(2f);

            // ---- out of the vault and to the Moon Door
            yield return WalkTiles(new Vector2Int(13, 25), 1.4f);
            yield return WalkTiles(new Vector2Int(11, 15), 1.4f);
            yield return WalkWorld(TW(TempleLayout.MoonDoorTile) + new Vector3(-1.8f, 0f, 0f), 0.8f, false);
            yield return Press(() => Door(DoorKind.MoonDoor).IsOpen);
            yield return new WaitForSecondsRealtime(3f);
            yield return WalkTiles(new Vector2Int(14, 15), 1.3f);

            // ---- the Heart and the escape
            yield return WalkWorld(TW(new Vector2Int(14, 15)), 0.8f, false);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Press(() => GM.Stage == MissionStage.Escape);
            yield return new WaitForSecondsRealtime(1.2f);
            var path = new List<Vector2Int>(L.EscapePath);
            foreach (var tile in path) yield return WalkWorld(TW(tile), 0.6f, true);
            yield return WalkWorld(TW(TempleLayout.EntranceTile), 0.6f, true);
            yield return WalkWorld(new Vector3(0f, 0f, -6f), 1.0f, true);
            yield return new WaitForSecondsRealtime(6f);
            GameInput.ScriptedMove = Vector2.zero;
            // finish: show the high scores and quit
            GM.ExitToMenu();
            yield return new WaitForSecondsRealtime(3f);
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>()) if (b.name == "HighScoresButton" && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); break; }
            yield return new WaitForSecondsRealtime(5f);
            Application.Quit();
        }

        /// <summary>Presses E until the thing in reach reacts (the prompt appears a moment after arriving).</summary>
        private IEnumerator Press(System.Func<bool> done)
        {
            for (int i = 0; i < 12 && !done(); i++)
            {
                GameInput.Tap("Interact");
                yield return new WaitForSecondsRealtime(0.6f);
            }
        }

        private IEnumerator Scan(float wait)
        {
            GameInput.Tap("Scan");
            yield return new WaitForSecondsRealtime(wait);
        }

        private IEnumerator WalkTiles(Vector2Int target, float arriveDistance, bool slow = false)
        {
            Vector2Int from = TempleLayout.WorldToTile(Player.transform.position);
            if (!L.IsOpen(from)) from = new Vector2Int(Mathf.Clamp(from.x, 0, TempleLayout.Size - 1), Mathf.Clamp(from.y, 0, TempleLayout.Size - 1));
            var path = L.Path(from, target);
            foreach (var tile in path) yield return WalkWorld(TW(tile), arriveDistance, !slow);
        }

        /// <summary>Walks to a world point: the camera is turned towards it and W is held. Gives up after a while so a blocked path cannot hang the demo.</summary>
        private IEnumerator WalkWorld(Vector3 target, float arrive, bool sprint)
        {
            float timeout = 0f;
            while (timeout < 14f)
            {
                timeout += Time.unscaledDeltaTime;
                if (GM.State == GameState.GameOver || GM.State == GameState.Complete) yield break;
                var p = Player.transform.position;
                Vector3 d = target - p; d.y = 0f;
                if (d.magnitude < arrive) break;
                float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float camYaw = Camera.main.transform.eulerAngles.y;
                float delta = Mathf.DeltaAngle(camYaw, wantYaw);
                cam.AddYaw(Mathf.Clamp(delta, -9f, 9f));
                GameInput.ScriptedMove = new Vector2(0f, Mathf.Abs(delta) > 60f ? 0.25f : 1f);
                GameInput.ScriptedSprint = sprint && Mathf.Abs(delta) < 25f;
                AutoStun();
                yield return null;
            }
            GameInput.ScriptedMove = Vector2.zero; GameInput.ScriptedSprint = false;
        }

        private float nextStun;

        private void AutoStun()
        {
            if (Time.time < nextStun) return;
            foreach (var s in Object.FindObjectsByType<SentinelAI>(FindObjectsSortMode.None))
                if (Vector3.Distance(s.transform.position, Player.transform.position) < 9f && s.State != SentinelState.Stunned && EchoLens.Instance.Ready)
                { GameInput.Tap("Scan"); nextStun = Time.time + 2f; return; }
        }
    }
}
