using System;
using System.Collections.Generic;
using UnityEngine;
using NED2024.Temple;

namespace NED2024.Core
{
    /// <summary>
    /// Saves and restores progress at each checkpoint: position, health, keys, relics, score, which items were collected, which doors
    /// are open, the rune puzzle and the secret walls. Without this a Retry would bring every collected relic back.
    /// Items are identified by their position in the Collectibles container of the scene.
    /// </summary>
    public class ProgressSaver : MonoBehaviour
    {
        private const string Key = "progress_json";
        private const string DeathsKey = "progress_deaths";

        [SerializeField] private Transform collectRoot;
        [SerializeField] private Health playerHealth;
        [SerializeField] private RunePuzzle puzzle;
        [SerializeField] private HeartOfEternity heart;

        private GameObject[] collectibles = new GameObject[0];
        private TempleDoor[] doors = new TempleDoor[0];
        private SecretWall[] secrets = new SecretWall[0];

        [Serializable]
        private class SaveData
        {
            public float health;
            public bool sun, moon, puzzleSolved, heartTaken, hasPosition;
            public int relics, secretsFound, deaths, score;
            public float px, py, pz, yaw;
            public int[] collectedGone;
            public int[] openDoors;
            public int[] openSecrets;
        }

        public static void Clear() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.DeleteKey(DeathsKey); }

        private void Awake()
        {
            var list = new List<GameObject>();
            if (collectRoot != null) foreach (Transform c in collectRoot) list.Add(c.gameObject);
            collectibles = list.ToArray();
            doors = FindObjectsByType<TempleDoor>(FindObjectsSortMode.None);
            Array.Sort(doors, (a, b) => ((int)a.Kind).CompareTo((int)b.Kind));
            secrets = FindObjectsByType<SecretWall>(FindObjectsSortMode.None);
            Array.Sort(secrets, (a, b) => string.CompareOrdinal(a.name, b.name));
        }

        public void Save()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var data = new SaveData();
            data.health = playerHealth != null && !playerHealth.IsDead ? playerHealth.Current : 100f;
            data.sun = gm.HasSunKey; data.moon = gm.HasMoonKey;
            data.relics = gm.RelicsFound; data.secretsFound = gm.SecretsFound; data.deaths = gm.Deaths; data.score = gm.Score;
            data.hasPosition = gm.HasCheckpointPosition;
            data.px = gm.CheckpointPosition.x; data.py = gm.CheckpointPosition.y; data.pz = gm.CheckpointPosition.z; data.yaw = gm.CheckpointYaw;
            data.puzzleSolved = puzzle != null && puzzle.Solved;
            data.heartTaken = heart != null && heart.Taken;

            var gone = new List<int>();
            for (int i = 0; i < collectibles.Length; i++) if (collectibles[i] == null) gone.Add(i);
            data.collectedGone = gone.ToArray();

            var open = new List<int>();
            foreach (var d in doors) if (d != null && d.IsOpen) open.Add((int)d.Kind);
            data.openDoors = open.ToArray();

            var sec = new List<int>();
            for (int i = 0; i < secrets.Length; i++) if (secrets[i] != null && secrets[i].IsOpen) sec.Add(i);
            data.openSecrets = sec.ToArray();

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>The number of deaths is kept separately because a death happens after the last checkpoint was saved.</summary>
        public void NoteDeath(int deaths) { PlayerPrefs.SetInt(DeathsKey, deaths); PlayerPrefs.Save(); }

        public void Load(GameManager gm)
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return;
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return;

            int deaths = Mathf.Max(data.deaths, PlayerPrefs.GetInt(DeathsKey, 0));
            gm.RestoreProgress(data.sun, data.moon, data.relics, data.secretsFound, deaths, data.score);
            if (playerHealth != null && data.health > 0f) playerHealth.SetCurrent(Mathf.Max(data.health, 40f));

            if (data.collectedGone != null)
                foreach (int i in data.collectedGone) if (i >= 0 && i < collectibles.Length && collectibles[i] != null) Destroy(collectibles[i]);

            // The entrance sealed itself on the first visit, so it is closed on every later run.
            foreach (var d in doors)
            {
                if (d == null) continue;
                bool shouldOpen = data.openDoors != null && Array.IndexOf(data.openDoors, (int)d.Kind) >= 0;
                if (d.Kind == DoorKind.Entrance && !shouldOpen) d.SetOpen(false, true);
                else if (shouldOpen && !d.IsOpen) d.SetOpen(true, true);
            }
            if (data.openSecrets != null)
                foreach (int i in data.openSecrets) if (i >= 0 && i < secrets.Length && secrets[i] != null) secrets[i].Open(true);
            if (data.puzzleSolved && puzzle != null) puzzle.RestoreSolved();
            if (data.heartTaken && heart != null) heart.Take(true);

            if (data.hasPosition)
            {
                gm.RestoreCheckpoint(new Vector3(data.px, data.py, data.pz), data.yaw);
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.SetPositionAndRotation(new Vector3(data.px, data.py, data.pz), Quaternion.Euler(0f, data.yaw, 0f));
                    if (cc != null) cc.enabled = true;
                }
            }
        }
    }
}
