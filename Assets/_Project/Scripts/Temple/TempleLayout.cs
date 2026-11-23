using System.Collections.Generic;
using UnityEngine;

namespace NED2024.Temple
{
    public enum Region { Solid, Maze, Hall, Rune, Gallery, Vault, Heart, SunVault }

    public enum DoorKind { Entrance, SunDoor, RuneGate, VaultExit, MoonDoor, ExitDoor }

    public class DoorSpec
    {
        public DoorKind Kind;
        public Vector2Int Tile;
        public bool AlongX;   // true when the player walks through it along the X axis (the slab is thin in X)
    }

    /// <summary>
    /// The plan of the temple. A 31 x 31 grid of 4 m tiles. Odd/odd tiles are maze cells; the tiles between them are walls that
    /// are carved away to make passages. Special rooms (entrance hall, vaults, rune chamber, trap gallery, heart chamber) are
    /// reserved first and the maze fills the rest. A fixed seed makes the temple identical on every run.
    /// </summary>
    public class TempleLayout
    {
        public const int Size = 31;
        public const int Cells = 15;
        public const float Tile = 4f;
        public const float WallHeight = 7f;

        public readonly bool[,] Open = new bool[Size, Size];
        public readonly Region[,] RegionOf = new Region[Size, Size];
        public readonly List<DoorSpec> Doors = new List<DoorSpec>();

        // Key places (tile coordinates)
        public static readonly Vector2Int EntranceTile = new Vector2Int(15, 0);
        public static readonly Vector2Int HallExit = new Vector2Int(15, 4);
        public static readonly Vector2Int SunVaultDoorTile = new Vector2Int(4, 1);
        public static readonly Vector2Int SunDoorTile = new Vector2Int(4, 25);
        public static readonly Vector2Int RuneGateTile = new Vector2Int(1, 28);
        public static readonly Vector2Int VaultExitTile = new Vector2Int(13, 26);
        public static readonly Vector2Int MoonDoorTile = new Vector2Int(12, 15);

        public Vector2Int SunKeyTile = new Vector2Int(1, 3);       // far corner of the sun vault
        public Vector2Int MoonKeyTile = new Vector2Int(15, 29);    // end of the vault after the gallery
        public Vector2Int HeartTile = new Vector2Int(15, 15);
        public Vector2Int RuneCentreTile = new Vector2Int(2, 25);

        public readonly List<Vector2Int> RelicTiles = new List<Vector2Int>();
        public readonly List<Vector2Int> SecretTiles = new List<Vector2Int>();      // alcoves behind hidden walls (the tile of the alcove)
        /// <summary>Tiles that exist physically (the secret passage and its alcove) but are not part of the walkable graph until the wall opens.</summary>
        public readonly HashSet<Vector2Int> Hidden = new HashSet<Vector2Int>();
        public readonly List<Vector2Int> SecretWallTiles = new List<Vector2Int>();
        public readonly List<Vector2Int> SecretEntryTiles = new List<Vector2Int>(); // the dead end in front of each alcove
        public readonly List<Vector2Int> OrbTiles = new List<Vector2Int>();
        public readonly List<List<Vector2Int>> SentinelRoutes = new List<List<Vector2Int>>();
        public readonly List<Vector2Int> EscapePath = new List<Vector2Int>();       // Moon Door -> entrance
        public readonly List<Vector2Int> CheckpointTiles = new List<Vector2Int>();

        private System.Random rng;

        public static Vector3 TileToWorld(Vector2Int t, float y = 0f) { return new Vector3((t.x - Size / 2) * Tile, y, t.y * Tile); }
        public static Vector2Int WorldToTile(Vector3 p) { return new Vector2Int(Mathf.RoundToInt(p.x / Tile) + Size / 2, Mathf.RoundToInt(p.z / Tile)); }

        public bool IsOpen(int x, int y) { return x >= 0 && y >= 0 && x < Size && y < Size && Open[x, y]; }
        public bool IsOpen(Vector2Int t) { return IsOpen(t.x, t.y); }

        private static TempleLayout shared;
        /// <summary>The temple used by the whole game (the same plan every run).</summary>
        public static TempleLayout Shared { get { if (shared == null) shared = Generate(2050); return shared; } }

        public static TempleLayout Generate(int seed = 2050)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var layout = new TempleLayout();
                if (layout.Build(seed + attempt)) return layout;
            }
            throw new System.Exception("Could not build a valid temple layout");
        }

        // ------------------------------------------------------------------ construction

        private bool Build(int seed)
        {
            rng = new System.Random(seed);
            var reserved = new bool[Cells, Cells];

            // Rooms in cell coordinates (inclusive)
            Room(reserved, 5, 0, 9, 1, Region.Hall);
            Room(reserved, 6, 6, 8, 8, Region.Heart);
            Room(reserved, 0, 11, 1, 13, Region.Rune);
            Room(reserved, 0, 14, 5, 14, Region.Gallery);
            Room(reserved, 6, 13, 7, 14, Region.Vault);
            Room(reserved, 0, 0, 1, 1, Region.SunVault);

            if (!CarveMaze(reserved)) return false;
            Braid(reserved, 0.16);

            // Openings between rooms and the maze
            OpenTile(HallExit, Region.Hall);
            OpenTile(SunVaultDoorTile, Region.SunVault);
            OpenTile(SunDoorTile, Region.Rune);
            OpenTile(RuneGateTile, Region.Rune);
            OpenTile(new Vector2Int(12, 29), Region.Gallery);   // gallery opens into the moon vault
            OpenTile(VaultExitTile, Region.Vault);
            OpenTile(MoonDoorTile, Region.Heart);
            OpenTile(EntranceTile, Region.Hall);

            Doors.Add(new DoorSpec { Kind = DoorKind.Entrance, Tile = EntranceTile, AlongX = false });
            Doors.Add(new DoorSpec { Kind = DoorKind.SunDoor, Tile = SunDoorTile, AlongX = true });
            Doors.Add(new DoorSpec { Kind = DoorKind.RuneGate, Tile = RuneGateTile, AlongX = false });
            Doors.Add(new DoorSpec { Kind = DoorKind.VaultExit, Tile = VaultExitTile, AlongX = false });
            Doors.Add(new DoorSpec { Kind = DoorKind.MoonDoor, Tile = MoonDoorTile, AlongX = true });
            // The exit door is the entrance door (the same slab closes behind the explorer and opens at the end).

            // Everything must be reachable (doors count as open)
            var dist = Distances(new Vector2Int(15, 2));
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (Open[x, y] && !dist.ContainsKey(new Vector2Int(x, y))) return false;

            PlaceFeatures();
            if (EscapePath.Count < 8) return false;
            return true;
        }

        private void Room(bool[,] reserved, int cx0, int cy0, int cx1, int cy1, Region region)
        {
            for (int cx = cx0; cx <= cx1; cx++) for (int cy = cy0; cy <= cy1; cy++) reserved[cx, cy] = true;
            for (int x = 2 * cx0 + 1; x <= 2 * cx1 + 1; x++)
                for (int y = 2 * cy0 + 1; y <= 2 * cy1 + 1; y++) { Open[x, y] = true; RegionOf[x, y] = region; }
        }

        private void OpenTile(Vector2Int t, Region r) { Open[t.x, t.y] = true; RegionOf[t.x, t.y] = r; }

        private bool CarveMaze(bool[,] reserved)
        {
            var visited = new bool[Cells, Cells];
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(7, 2);
            visited[start.x, start.y] = true;
            Mark(start.x, start.y);
            stack.Push(start);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var options = new List<Vector2Int>();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= Cells || n.y >= Cells || reserved[n.x, n.y] || visited[n.x, n.y]) continue;
                    options.Add(d);
                }
                if (options.Count == 0) { stack.Pop(); continue; }
                var dir = options[rng.Next(options.Count)];
                var next = c + dir;
                MarkTile(2 * c.x + 1 + dir.x, 2 * c.y + 1 + dir.y);   // the wall tile between the two cells
                visited[next.x, next.y] = true;
                Mark(next.x, next.y);
                stack.Push(next);
            }
            for (int x = 0; x < Cells; x++) for (int y = 0; y < Cells; y++) if (!reserved[x, y] && !visited[x, y]) return false;
            return true;
        }

        private void Mark(int cx, int cy) { Open[2 * cx + 1, 2 * cy + 1] = true; RegionOf[2 * cx + 1, 2 * cy + 1] = Region.Maze; }
        private void MarkTile(int x, int y) { Open[x, y] = true; RegionOf[x, y] = Region.Maze; }

        /// <summary>Removes some walls between maze cells so the maze has loops (shortcuts and alternative routes).</summary>
        private void Braid(bool[,] reserved, double chance)
        {
            for (int x = 1; x < Size - 1; x++)
                for (int y = 1; y < Size - 1; y++)
                {
                    if (Open[x, y]) continue;
                    bool wallBetweenX = x % 2 == 0 && y % 2 == 1;
                    bool wallBetweenY = x % 2 == 1 && y % 2 == 0;
                    if (!wallBetweenX && !wallBetweenY) continue;
                    var a = wallBetweenX ? new Vector2Int(x - 1, y) : new Vector2Int(x, y - 1);
                    var b = wallBetweenX ? new Vector2Int(x + 1, y) : new Vector2Int(x, y + 1);
                    if (!Open[a.x, a.y] || !Open[b.x, b.y] || RegionOf[a.x, a.y] != Region.Maze || RegionOf[b.x, b.y] != Region.Maze) continue;
                    if (rng.NextDouble() < chance) MarkTile(x, y);
                }
        }

        // ------------------------------------------------------------------ path finding

        private static readonly Vector2Int[] Dirs4 = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        public Dictionary<Vector2Int, int> Distances(Vector2Int from)
        {
            var dist = new Dictionary<Vector2Int, int> { { from, 0 } };
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in Dirs4)
                {
                    var n = c + d;
                    if (!IsOpen(n) || dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        public List<Vector2Int> Path(Vector2Int from, Vector2Int to)
        {
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(from); prev[from] = from;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) break;
                foreach (var d in Dirs4)
                {
                    var n = c + d;
                    if (!IsOpen(n) || prev.ContainsKey(n)) continue;
                    prev[n] = c; q.Enqueue(n);
                }
            }
            var path = new List<Vector2Int>();
            if (!prev.ContainsKey(to)) return path;
            for (var c = to; c != from; c = prev[c]) path.Add(c);
            path.Add(from);
            path.Reverse();
            return path;
        }

        private int OpenNeighbours(Vector2Int t)
        {
            int n = 0;
            foreach (var d in Dirs4) if (IsOpen(t + d)) n++;
            return n;
        }

        // ------------------------------------------------------------------ features

        private void PlaceFeatures()
        {
            var fromHall = Distances(new Vector2Int(15, 5));
            var used = new HashSet<Vector2Int>();

            // Dead ends in the maze, farthest from the hall first: relics and secret alcoves go there.
            var deadEnds = new List<Vector2Int>();
            for (int x = 1; x < Size - 1; x++)
                for (int y = 1; y < Size - 1; y++)
                {
                    var t = new Vector2Int(x, y);
                    if (!Open[x, y] || RegionOf[x, y] != Region.Maze || OpenNeighbours(t) != 1) continue;
                    if (fromHall.ContainsKey(t)) deadEnds.Add(t);
                }
            deadEnds.Sort((a, b) => fromHall[b].CompareTo(fromHall[a]));

            // Secret alcoves: dead ends whose continuation tile is solid and fully surrounded by solid tiles.
            foreach (var de in deadEnds)
            {
                if (SecretTiles.Count >= 2) break;
                Vector2Int open = Vector2Int.zero;
                foreach (var d in Dirs4) if (IsOpen(de + d)) open = d;
                var wall = de - open;                // the wall behind the dead end
                var alcove = de - open * 2;          // the tile beyond the wall
                if (alcove.x < 2 || alcove.y < 2 || alcove.x > Size - 3 || alcove.y > Size - 3) continue;
                if (Open[wall.x, wall.y] || Open[alcove.x, alcove.y]) continue;
                // the alcove and its wall must not touch any other open tile
                bool clean = true;
                foreach (var d in Dirs4)
                {
                    if (d != open && IsOpen(alcove + d)) clean = false;      // d == open would point back at the wall
                    if (d != open && d != -open && IsOpen(wall + d)) clean = false;
                }
                if (!clean) continue;
                SecretEntryTiles.Add(de);
                SecretWallTiles.Add(wall);
                SecretTiles.Add(alcove);
                Hidden.Add(wall); Hidden.Add(alcove);
                used.Add(de);
            }

            foreach (var de in deadEnds)
            {
                if (RelicTiles.Count >= 5) break;
                if (used.Contains(de)) continue;
                bool far = true;
                foreach (var r in RelicTiles) if (Mathf.Abs(r.x - de.x) + Mathf.Abs(r.y - de.y) < 8) far = false;
                if (!far) continue;
                RelicTiles.Add(de); used.Add(de);
            }
            // If the maze has too few good dead ends use the farthest open tiles.
            if (RelicTiles.Count < 5)
            {
                var all = new List<Vector2Int>(fromHall.Keys);
                all.Sort((a, b) => fromHall[b].CompareTo(fromHall[a]));
                foreach (var t in all)
                {
                    if (RelicTiles.Count >= 5) break;
                    if (RegionOf[t.x, t.y] != Region.Maze || used.Contains(t)) continue;
                    RelicTiles.Add(t); used.Add(t);
                }
            }

            // Life orbs on junctions spread over the maze
            var junctions = new List<Vector2Int>();
            for (int x = 1; x < Size - 1; x++)
                for (int y = 1; y < Size - 1; y++)
                {
                    var t = new Vector2Int(x, y);
                    if (Open[x, y] && RegionOf[x, y] == Region.Maze && OpenNeighbours(t) >= 3 && fromHall.ContainsKey(t)) junctions.Add(t);
                }
            Shuffle(junctions);
            foreach (var j in junctions)
            {
                if (OrbTiles.Count >= 7) break;
                bool far = true;
                foreach (var o in OrbTiles) if (Mathf.Abs(o.x - j.x) + Mathf.Abs(o.y - j.y) < 7) far = false;
                if (far && !used.Contains(j)) { OrbTiles.Add(j); used.Add(j); }
            }

            // The way out: from the moon door to the entrance
            var path = Path(new Vector2Int(11, 15), new Vector2Int(15, 1));
            EscapePath.AddRange(path);

            // Sentinel patrols: four routes in different parts of the maze
            var homes = new List<Vector2Int>();
            var cand = new List<Vector2Int>(fromHall.Keys);
            Shuffle(cand);
            foreach (var t in cand)
            {
                if (homes.Count >= 5) break;
                if (RegionOf[t.x, t.y] != Region.Maze || fromHall[t] < 8) continue;
                bool far = true;
                foreach (var h in homes) if (Mathf.Abs(h.x - t.x) + Mathf.Abs(h.y - t.y) < 12) far = false;
                if (far) homes.Add(t);
            }
            foreach (var h in homes)
            {
                var route = new List<Vector2Int> { h };
                var around = Distances(h);
                var ring = new List<Vector2Int>();
                foreach (var kv in around) if (kv.Value >= 5 && kv.Value <= 9 && RegionOf[kv.Key.x, kv.Key.y] == Region.Maze) ring.Add(kv.Key);
                Shuffle(ring);
                for (int i = 0; i < ring.Count && route.Count < 4; i++)
                {
                    bool apart = true;
                    foreach (var r in route) if (Mathf.Abs(r.x - ring[i].x) + Mathf.Abs(r.y - ring[i].y) < 4) apart = false;
                    if (apart) route.Add(ring[i]);
                }
                if (route.Count >= 3) SentinelRoutes.Add(route);
            }

            // Checkpoints
            CheckpointTiles.Add(new Vector2Int(15, 5));      // start
            CheckpointTiles.Add(new Vector2Int(5, 25));      // in front of the sun door
            CheckpointTiles.Add(new Vector2Int(1, 29));      // gallery start
            CheckpointTiles.Add(new Vector2Int(13, 25));     // outside the vault exit
            CheckpointTiles.Add(new Vector2Int(14, 15));     // heart chamber
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
