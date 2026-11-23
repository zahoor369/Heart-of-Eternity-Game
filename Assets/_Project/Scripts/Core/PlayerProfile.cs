using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NED2024.Core
{
    /// <summary>
    /// Player profile: the player's name, lifetime statistics and a top-5 high-score table.
    /// Everything is stored as JSON in PlayerPrefs so it is still there the next time the game is started.
    /// </summary>
    public static class PlayerProfile
    {
        private const string NameKey = "player_name";
        private const string ScoresKey = "high_scores_json";
        private const string StatsKey = "profile_stats_json";
        public const int TableSize = 5;
        public const string DefaultName = "Survivor";

        [Serializable]
        public class Entry
        {
            public string name;
            public int score;
            public string outcome;   // ESCAPED or TRAPPED
            public int kills;
            public int seconds;
        }

        [Serializable] private class Table { public List<Entry> entries = new List<Entry>(); }
        [Serializable] private class Stats { public int games; public int wins; public int kills; public int best; }

        public static string Name
        {
            get { string n = PlayerPrefs.GetString(NameKey, DefaultName); return string.IsNullOrWhiteSpace(n) ? DefaultName : n; }
            set
            {
                string n = string.IsNullOrWhiteSpace(value) ? DefaultName : value.Trim();
                if (n.Length > 14) n = n.Substring(0, 14);
                PlayerPrefs.SetString(NameKey, n);
                PlayerPrefs.Save();
            }
        }

        public static int Games => LoadStats().games;
        public static int Wins => LoadStats().wins;
        public static int TotalKills => LoadStats().kills;
        public static int BestScore => LoadStats().best;

        private static Table LoadTable()
        {
            string json = PlayerPrefs.GetString(ScoresKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var t = JsonUtility.FromJson<Table>(json);
                if (t != null && t.entries != null) return t;
            }
            return new Table();
        }

        private static Stats LoadStats()
        {
            string json = PlayerPrefs.GetString(StatsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var s = JsonUtility.FromJson<Stats>(json);
                if (s != null) return s;
            }
            return new Stats();
        }

        public static IReadOnlyList<Entry> Top() { return LoadTable().entries; }

        /// <summary>Adds a finished run to the statistics and the high-score table. Returns the rank (1 = best), or 0 if it did not make the table.</summary>
        public static int Submit(int score, string outcome, int kills, int seconds)
        {
            var stats = LoadStats();
            stats.games++;
            if (outcome == "ESCAPED") stats.wins++;
            stats.kills += kills;
            if (score > stats.best) stats.best = score;
            PlayerPrefs.SetString(StatsKey, JsonUtility.ToJson(stats));

            var table = LoadTable();
            table.entries.Add(new Entry { name = Name, score = score, outcome = outcome, kills = kills, seconds = seconds });
            table.entries.Sort((a, b) => b.score.CompareTo(a.score));   // best first
            int rank = 0;
            for (int i = 0; i < table.entries.Count && i < TableSize; i++)
                if (table.entries[i].score == score && table.entries[i].name == Name && table.entries[i].seconds == seconds && table.entries[i].outcome == outcome) { rank = i + 1; break; }
            if (table.entries.Count > TableSize) table.entries.RemoveRange(TableSize, table.entries.Count - TableSize);
            PlayerPrefs.SetString(ScoresKey, JsonUtility.ToJson(table));
            PlayerPrefs.Save();
            return rank;
        }

        public static string TableText()
        {
            var top = Top();
            if (top.Count == 0) return "No runs yet.\nFinish a mission to get on the board.";
            var sb = new StringBuilder();
            for (int i = 0; i < top.Count; i++)
            {
                var e = top[i];
                sb.AppendLine((i + 1) + ".   " + e.name + "   -   " + e.score + " points   (" + e.outcome + ", " + e.kills + " relics, " + FormatTime(e.seconds) + ")");
            }
            return sb.ToString();
        }

        public static string StatsText()
        {
            var s = LoadStats();
            return "Games played " + s.games + "     Escapes " + s.wins + "     Relics found " + s.kills + "     Best score " + s.best;
        }

        public static string FormatTime(int seconds) { return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"); }

        /// <summary>Used by the automated test only.</summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(ScoresKey); PlayerPrefs.DeleteKey(StatsKey); PlayerPrefs.DeleteKey(NameKey);
        }
    }
}
