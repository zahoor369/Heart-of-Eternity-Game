using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NED2024.Temple;

namespace NED2024.EditorTools
{
    /// <summary>Generates the temple's walls, floors, ceilings and trims as a few combined meshes.</summary>
    public static partial class GameBuilder
    {
        private const float H = TempleLayout.WallHeight;
        private const float T = TempleLayout.Tile;

        private class MeshAcc
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> Tris = new List<int>();

            /// <summary>Adds one quad. The corners are given in cyclic order; the winding is fixed so the front faces along the normal.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float uvDiv = 4f)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { var t = b; b = d; d = t; }
                int i = V.Count;
                foreach (var p in new[] { a, b, c, d })
                {
                    V.Add(p); N.Add(normal);
                    float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
                    if (ax >= ay && ax >= az) UV.Add(new Vector2(p.z, p.y) / uvDiv);
                    else if (ay >= az) UV.Add(new Vector2(p.x, p.z) / uvDiv);
                    else UV.Add(new Vector2(p.x, p.y) / uvDiv);
                }
                Tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            public void Box(Vector3 centre, Vector3 size, float uvDiv = 4f)
            {
                Vector3 h = size * 0.5f;
                Vector3 p000 = centre + new Vector3(-h.x, -h.y, -h.z), p100 = centre + new Vector3(h.x, -h.y, -h.z),
                        p110 = centre + new Vector3(h.x, h.y, -h.z), p010 = centre + new Vector3(-h.x, h.y, -h.z),
                        p001 = centre + new Vector3(-h.x, -h.y, h.z), p101 = centre + new Vector3(h.x, -h.y, h.z),
                        p111 = centre + new Vector3(h.x, h.y, h.z), p011 = centre + new Vector3(-h.x, h.y, h.z);
                Quad(p000, p100, p110, p010, Vector3.back, uvDiv);
                Quad(p101, p001, p011, p111, Vector3.forward, uvDiv);
                Quad(p001, p000, p010, p011, Vector3.left, uvDiv);
                Quad(p100, p101, p111, p110, Vector3.right, uvDiv);
                Quad(p010, p110, p111, p011, Vector3.up, uvDiv);
                Quad(p000, p001, p101, p100, Vector3.down, uvDiv);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(Tris, 0);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        private static Material WallMaterial(Region r)
        {
            switch (r)
            {
                case Region.Hall: return matWallHall;
                case Region.Heart: return matWallHeart;
                case Region.Rune: return matWallRune;
                case Region.Gallery: case Region.Vault: case Region.SunVault: return matWallDark;
                default: return matWallMaze;
            }
        }

        private static Material FloorMaterial(Region r)
        {
            switch (r)
            {
                case Region.Hall: return matFloorHall;
                case Region.Heart: return matFloorHeart;
                case Region.Rune: return matFloorRune;
                default: return matFloorMaze;
            }
        }

        private static Material StripMaterial(Region r)
        {
            switch (r)
            {
                case Region.Hall: case Region.Heart: return glowAmber;
                case Region.Gallery: case Region.Vault: return glowRed;
                default: return glowCyan;
            }
        }

        private static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        /// <summary>Builds the whole temple structure. Returns the parent object.</summary>
        private static Transform BuildStructure(TempleLayout L)
        {
            var root = new GameObject("Structure").transform;
            var acc = new Dictionary<Material, MeshAcc>();
            MeshAcc A(Material m) { if (!acc.TryGetValue(m, out var a)) { a = new MeshAcc(); acc[m] = a; } return a; }

            for (int x = 0; x < TempleLayout.Size; x++)
                for (int y = 0; y < TempleLayout.Size; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (!L.Open[x, y] && !L.Hidden.Contains(tile)) continue;
                    var region = L.Open[x, y] ? L.RegionOf[x, y] : Region.Maze;
                    Vector3 c = TempleLayout.TileToWorld(tile);
                    float h = T * 0.5f;

                    A(FloorMaterial(region)).Quad(c + new Vector3(-h, 0, -h), c + new Vector3(h, 0, -h), c + new Vector3(h, 0, h), c + new Vector3(-h, 0, h), Vector3.up);
                    A(matCeiling).Quad(c + new Vector3(-h, H, -h), c + new Vector3(h, H, -h), c + new Vector3(h, H, h), c + new Vector3(-h, H, h), Vector3.down);

                    foreach (var d in Dirs)
                    {
                        if (L.IsOpen(tile + d) || L.Hidden.Contains(tile + d)) continue;
                        if (tile == TempleLayout.EntranceTile && d == Vector2Int.down) continue;   // the doorway to the outside
                        Vector3 dir = new Vector3(d.x, 0, d.y);
                        Vector3 perp = new Vector3(Mathf.Abs(d.y), 0, Mathf.Abs(d.x));
                        Vector3 b0 = c + dir * h;
                        A(WallMaterial(region)).Quad(b0 - perp * h, b0 + perp * h, b0 + perp * h + Vector3.up * H, b0 - perp * h + Vector3.up * H, -dir);

                        // baseboard, cornice, glowing strip
                        Vector3 thick = new Vector3(d.x != 0 ? 1f : 0f, 0f, d.y != 0 ? 1f : 0f);
                        Vector3 along = new Vector3(d.x != 0 ? 0f : 1f, 0f, d.x != 0 ? 1f : 0f);
                        Vector3 Size(float t, float hh, float w) => thick * t + Vector3.up * hh + along * w;
                        A(matTrim).Box(b0 - dir * 0.1f + Vector3.up * 0.28f, Size(0.2f, 0.56f, T));
                        A(matTrim).Box(b0 - dir * 0.25f + Vector3.up * (H - 0.3f), Size(0.5f, 0.6f, T));
                        A(StripMaterial(region)).Box(b0 - dir * 0.215f + Vector3.up * 0.585f, Size(0.03f, 0.05f, 3.4f));
                        if (region != Region.Gallery && (x + y) % 2 == 0)
                            A(matTrim).Box(b0 - dir * 0.2f + Vector3.up * (H * 0.5f), Size(0.4f, H, 0.7f));
                    }
                }

            // Lintels above doors and openings keep the ceiling closed over a doorway.
            foreach (var d in L.Doors) AddLintel(A(matTrim), d.Tile, d.AlongX, 5.8f);
            AddLintel(A(matTrim), TempleLayout.HallExit, false, 5.2f);
            AddLintel(A(matTrim), TempleLayout.SunVaultDoorTile, true, 5.2f);
            AddLintel(A(matTrim), new Vector2Int(12, 29), true, 5.2f);

            AssetKit.EnsureFolder("Assets/_Project/Art/Meshes");
            int i = 0;
            foreach (var kv in acc)
            {
                var go = new GameObject("Mesh_" + kv.Key.name);
                go.transform.SetParent(root, false);
                var mesh = kv.Value.ToMesh("Temple_" + i++ + "_" + kv.Key.name);
                SaveMeshAsset(mesh, "Temple_" + kv.Key.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                bool glow = kv.Key.name.StartsWith("Glow_");
                if (glow) mr.shadowCastingMode = ShadowCastingMode.Off;
                if (!glow) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.isStatic = true;
            }
            return root;
        }

        private static void AddLintel(MeshAcc a, Vector2Int tile, bool alongX, float bottom)
        {
            Vector3 c = TempleLayout.TileToWorld(tile);
            float hh = H - bottom;
            Vector3 size = alongX ? new Vector3(1.1f, hh, T) : new Vector3(T, hh, 1.1f);
            a.Box(c + Vector3.up * (bottom + hh * 0.5f), size);
        }

        // ------------------------------------------------------------ outside: courtyard and the great portal

        private static void BuildExterior(Transform parent)
        {
            var root = new GameObject("Exterior").transform;
            root.SetParent(parent, false);

            // Jungle floor
            var acc = new MeshAcc();
            acc.Quad(new Vector3(-70, 0, -70), new Vector3(70, 0, -70), new Vector3(70, 0, -2), new Vector3(-70, 0, -2), Vector3.up, 6f);
            var ground = new GameObject("JungleGround");
            ground.transform.SetParent(root, false);
            var gm = acc.ToMesh("JungleGround");
            SaveMeshAsset(gm, "JungleGround");
            ground.AddComponent<MeshFilter>().sharedMesh = gm;
            ground.AddComponent<MeshRenderer>().sharedMaterial = matGround;
            ground.AddComponent<MeshCollider>().sharedMesh = gm;
            ground.isStatic = true;

            // Great portal: two pillars, a lintel and heavy side walls
            var s = new GameObject("Portal").transform; s.SetParent(root, false);
            void Block(string n, Vector3 centre, Vector3 size, Material m)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = n; b.transform.SetParent(s, false); b.transform.position = centre; b.transform.localScale = size;
                b.GetComponent<MeshRenderer>().sharedMaterial = m; b.isStatic = true;
            }
            Block("PillarL", new Vector3(-3.6f, 6f, -3.2f), new Vector3(2.6f, 12f, 2.6f), matWallHall);
            Block("PillarR", new Vector3(3.6f, 6f, -3.2f), new Vector3(2.6f, 12f, 2.6f), matWallHall);
            Block("Lintel", new Vector3(0f, 9.2f, -3.2f), new Vector3(10.6f, 3.2f, 3.2f), matWallHall);
            Block("CapL", new Vector3(-3.6f, 12.3f, -3.2f), new Vector3(3.4f, 0.8f, 3.4f), matTrim);
            Block("CapR", new Vector3(3.6f, 12.3f, -3.2f), new Vector3(3.4f, 0.8f, 3.4f), matTrim);
            Block("WallL", new Vector3(-17f, 8f, -3f), new Vector3(24f, 16f, 4f), matWallMaze);
            Block("WallR", new Vector3(17f, 8f, -3f), new Vector3(24f, 16f, 4f), matWallMaze);
            Block("Backdrop", new Vector3(0f, 14f, -1.4f), new Vector3(10f, 14f, 0.6f), matWallMaze);
            // Invisible walls keep the explorer in the courtyard
            void Barrier(Vector3 centre, Vector3 size)
            {
                var b = new GameObject("Barrier"); b.transform.SetParent(root, false); b.transform.position = centre;
                b.AddComponent<BoxCollider>().size = size;
            }
            Barrier(new Vector3(-24f, 4f, -26f), new Vector3(2f, 12f, 60f));
            Barrier(new Vector3(24f, 4f, -26f), new Vector3(2f, 12f, 60f));
            Barrier(new Vector3(0f, 4f, -52f), new Vector3(52f, 12f, 2f));
        }
    }
}
