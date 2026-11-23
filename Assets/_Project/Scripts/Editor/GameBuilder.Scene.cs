using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NED2024.Core;
using NED2024.Interactions;
using NED2024.Player;
using NED2024.CameraSystem;
using NED2024.Temple;
using NED2024.UI;

namespace NED2024.EditorTools
{
    public static partial class GameBuilder
    {
        private static TempleLayout layout;
        private static Transform levelRoot, collectRoot, trapRoot, decorRoot, triggerRoot, doorRoot, lightRoot;
        private static readonly Dictionary<DoorKind, TempleDoor> doorMap = new Dictionary<DoorKind, TempleDoor>();
        private static RunePuzzle runePuzzle;
        private static HeartOfEternity heartObj;
        private static EscapeTrail trailObj;
        private static RollingBoulder boulderObj;
        private static ObjectiveTracker tracker;
        private static System.Random decorRng;

        private static Vector3 TW(Vector2Int t, float y = 0f) { return TempleLayout.TileToWorld(t, y); }
        private static Vector3 TW(int x, int y, float h = 0f) { return TempleLayout.TileToWorld(new Vector2Int(x, y), h); }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            layout = TempleLayout.Shared;
            decorRng = new System.Random(77);
            doorMap.Clear();
            BuildPropMaterials();

            levelRoot = new GameObject("Level").transform;
            collectRoot = new GameObject("Collectibles").transform;
            trapRoot = new GameObject("Traps").transform;
            decorRoot = new GameObject("Decor").transform;
            triggerRoot = new GameObject("Triggers").transform;
            doorRoot = new GameObject("Doors").transform;
            lightRoot = new GameObject("Torches").transform;
            var sentinelRoot = new GameObject("Sentinels").transform;

            BuildLighting();
            BuildExterior(levelRoot);
            var structure = BuildStructure(layout);
            structure.SetParent(levelRoot, false);

            foreach (var d in layout.Doors) doorMap[d.Kind] = BuildDoor(d, doorRoot);
            BuildDecor();
            BuildVaultsAndPuzzle();
            BuildGalleryTraps();
            BuildCollectibles();
            BuildSecrets();
            BuildHeartChamber();
            BuildEscapeRoute();
            BuildTriggers();
            BuildTorches();
            AddNavMesh();

            foreach (var route in layout.SentinelRoutes)
            {
                var pts = new List<Vector3>();
                foreach (var t in route) pts.Add(TW(t));
                SpawnSentinel(pts[0], pts, sentinelRoot);
            }

            var player = BuildPlayer(out ThirdPersonCameraController camCtrl, out PlayerInteractor interactor, out GameObject camerasRoot, out Camera introCam);
            var gm = BuildManagers(player);
            BuildGameUi(player, interactor, camerasRoot, introCam, gm);

            UIKit.CreateEventSystem();
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log("Game scene built: " + GameScenePath);
        }

        // ------------------------------------------------------------ lighting

        private static void BuildLighting()
        {
            var root = new GameObject("Lighting").transform;

            var skyMat = new Material(Shader.Find("Skybox/Procedural"));
            skyMat.SetFloat("_SunSize", 0.015f);
            skyMat.SetFloat("_AtmosphereThickness", 0.55f);
            skyMat.SetFloat("_Exposure", 0.5f);
            skyMat.SetColor("_SkyTint", new Color(0.12f, 0.28f, 0.38f));
            skyMat.SetColor("_GroundColor", new Color(0.03f, 0.06f, 0.06f));
            AssetKit.EnsureFolder(AssetKit.MatDir);
            AssetDatabase.DeleteAsset(AssetKit.MatDir + "/TempleSky.mat");
            AssetDatabase.CreateAsset(skyMat, AssetKit.MatDir + "/TempleSky.mat");
            RenderSettings.skybox = skyMat;

            // Dark teal ambient so every corridor is readable; torches and glows do the rest.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.1f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = new Color(0.035f, 0.06f, 0.07f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.35f;

            var moon = new GameObject("Moonlight");
            moon.transform.SetParent(root, false);
            moon.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
            var l = moon.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.5f, 0.72f, 1f);
            l.intensity = 0.8f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.95f;
            RenderSettings.sun = l;

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.transform.SetParent(root, false);
            volume.isGlobal = true;
            volume.sharedProfile = postProfile;

            // Reflection probes (shiny gold, wet stone)
            AddReflectionProbe(root, "Probe_Hall", TW(15, 2, 3f), new Vector3(40, 8, 20));
            AddReflectionProbe(root, "Probe_Heart", TW(15, 15, 4f), new Vector3(26, 9, 26));
            AddReflectionProbe(root, "Probe_Courtyard", new Vector3(0, 4, -24), new Vector3(52, 12, 44));
        }

        private static void AddReflectionProbe(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            rp.resolution = 128;
            rp.size = size;
            rp.boxProjection = true;
        }

        // ------------------------------------------------------------ decoration

        private static void BuildDecor()
        {
            // ---- hall: statues, animal heads, braziers, pillars
            foreach (int x in new[] { 12, 18 })
            {
                var pos = TW(x, 3) + new Vector3(0f, 0f, 1.2f);
                PlaceModel("gothic_statue", pos, 180f, 3.4f, decorRoot, ModelMat("gothic_statue", new Color(0.85f, 0.85f, 0.82f)), true);
            }
            PlaceModel("lion_head", TW(14, 3) + new Vector3(0f, 3.4f, 1.85f), 180f, 1.3f, decorRoot, ModelMat("lion_head", new Color(1f, 0.9f, 0.7f)), false);
            PlaceModel("bull_head", TW(16, 3) + new Vector3(0f, 3.4f, 1.85f), 180f, 1.3f, decorRoot, ModelMat("bull_head", new Color(1f, 0.9f, 0.7f)), false);
            AddBrazier(decorRoot, TW(11, 1) + new Vector3(0.9f, 0f, 0.9f));
            AddBrazier(decorRoot, TW(19, 1) + new Vector3(-0.9f, 0f, 0.9f));
            foreach (int x in new[] { 13, 17 }) Prim(PrimitiveType.Cube, "HallPillar", decorRoot, TW(x, 2, H * 0.5f), new Vector3(1.1f, H, 1.1f), matTrim, true);

            // ---- rune chamber
            PlaceModel("marble_bust_01", TW(2, 23) + new Vector3(0f, 0f, -1.2f), 0f, 1.8f, decorRoot, ModelMat("marble_bust_01", new Color(0.9f, 0.92f, 0.95f)), true);
            AddBrazier(decorRoot, TW(1, 25) + new Vector3(-0.8f, 0f, 0f));

            // ---- sun vault
            PlaceModel("treasure_chest", TW(3, 3) + new Vector3(0.4f, 0f, 0.4f), 225f, 0.9f, decorRoot, ModelMat("treasure_chest", Color.white), true);

            // ---- gallery & vaults: a few braziers for light
            AddBrazier(decorRoot, TW(14, 27) + new Vector3(0f, 0f, 0.3f));

            // ---- heart chamber pillars and braziers
            foreach (var t in new[] { new Vector2Int(13, 13), new Vector2Int(17, 13), new Vector2Int(13, 17), new Vector2Int(17, 17) })
            {
                Prim(PrimitiveType.Cube, "HeartPillar", decorRoot, TW(t, H * 0.5f), new Vector3(1.4f, H, 1.4f), matTrim, true);
                Prim(PrimitiveType.Cube, "HeartPillarCap", decorRoot, TW(t, 0.4f), new Vector3(1.8f, 0.8f, 1.8f), matGoldDark);
                Prim(PrimitiveType.Cube, "HeartPillarGlow", decorRoot, TW(t, 3.2f), new Vector3(1.46f, 0.08f, 1.46f), glowCyan);
            }
            foreach (var t in new[] { new Vector2Int(14, 13), new Vector2Int(16, 17) }) AddBrazier(decorRoot, TW(t));

            // ---- rocks and moss in the maze corners
            var open = new List<Vector2Int>();
            for (int x = 1; x < TempleLayout.Size - 1; x++)
                for (int y = 1; y < TempleLayout.Size - 1; y++)
                    if (layout.Open[x, y] && layout.RegionOf[x, y] == Region.Maze) open.Add(new Vector2Int(x, y));
            for (int i = 0; i < 26 && open.Count > 0; i++)
            {
                var t = open[decorRng.Next(open.Count)];
                // push the rock against a solid side
                Vector3 c = TW(t);
                foreach (var d in Dirs)
                {
                    if (layout.IsOpen(t + d)) continue;
                    Vector3 p = c + new Vector3(d.x, 0, d.y) * 1.45f + new Vector3(d.y, 0, d.x) * ((float)decorRng.NextDouble() - 0.5f) * 2f;
                    PlaceModel("rock_moss_set_01", p, (float)decorRng.NextDouble() * 360f, 0.7f + (float)decorRng.NextDouble() * 0.5f, decorRoot, ModelMat("rock_moss_set_01", new Color(0.8f, 0.85f, 0.75f)), false);
                    break;
                }
            }
            for (int i = 0; i < 18 && open.Count > 0; i++)
            {
                var t = open[decorRng.Next(open.Count)];
                foreach (var d in Dirs)
                {
                    if (layout.IsOpen(t + d)) continue;
                    var m = PlaceModel("moss_01", TW(t) + new Vector3(d.x, 0, d.y) * 1.8f, (float)decorRng.NextDouble() * 360f, 0.9f, decorRoot, ModelMat("moss_01", new Color(0.7f, 0.9f, 0.6f)), false);
                    break;
                }
            }

            // ---- dust motes drifting in the big rooms
            foreach (var p in new[] { TW(15, 2, 2.5f), TW(15, 15, 3f), TW(2, 25, 2.5f) })
            {
                var ps = MakeLoopParticles("Dust", decorRoot, fxDust, new Color(0.8f, 0.9f, 1f, 0.5f), new Color(0.8f, 0.9f, 1f, 0f), 0.06f, 14f, 6f, 0.08f, 5f, -0.003f);
                ps.transform.position = p;
            }

            BuildCourtyardDecor();
        }

        private static void BuildCourtyardDecor()
        {
            var root = new GameObject("Jungle").transform;
            root.SetParent(decorRoot, false);
            var rng = new System.Random(5);
            float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

            var leaf = AssetKit.Mat("Foliage_Dark", new Color(0.07f, 0.2f, 0.09f), 0.1f);
            var bark = Pbr("Trunk_Roots", "roots", new Color(0.5f, 0.45f, 0.38f));
            // Jungle trees around the courtyard: tall trunks with a thick dark canopy
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * R(10f, 21f), z = -R(6f, 48f);
                if (Mathf.Abs(x) < 9f && z > -16f) continue;
                float h = R(18f, 28f);
                var t = Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(x, h * 0.5f, z), new Vector3(R(1.4f, 2.2f), h * 0.5f, R(1.4f, 2.2f)), bark, true);
                for (int k = 0; k < 4; k++)
                    Prim(PrimitiveType.Sphere, "Canopy", root, new Vector3(x + R(-3f, 3f), h + R(-1f, 3f), z + R(-3f, 3f)), Vector3.one * R(7f, 11f), leaf);
            }
            // Ferns and boulders
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(R(-20f, 20f), 0f, -R(3f, 44f));
                if (Mathf.Abs(p.x) < 2.6f && p.z > -22f) continue;   // keep the path to the door clear
                PlaceModel("fern_02", p, R(0f, 360f), R(0.8f, 1.6f), root, ModelMat("fern_02", new Color(0.7f, 0.95f, 0.7f)), false);
            }
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(R(-21f, 21f), 0f, -R(8f, 46f));
                if (Mathf.Abs(p.x) < 4f && p.z > -24f) continue;
                PlaceModel("boulder_01", p, R(0f, 360f), R(1.5f, 3.2f), root, ModelMat("boulder_01", new Color(0.7f, 0.78f, 0.65f)), true);
            }
            // Torches either side of the path
            foreach (float x in new[] { -3.6f, 3.6f })
                foreach (float z in new[] { -6f, -12f, -18f })
                {
                    var post = Prim(PrimitiveType.Cylinder, "TorchPost", root, new Vector3(x, 1.2f, z), new Vector3(0.18f, 1.2f, 0.18f), matMetal, true);
                    var flame = MakeLoopParticles("Flame", root, fxFlame, new Color(1f, 0.72f, 0.3f, 0.95f), new Color(1f, 0.2f, 0.05f, 0f), 0.7f, 22f, 0.9f, 0.7f, 0.12f, -0.2f, true, 10f);
                    flame.transform.position = new Vector3(x, 2.5f, z); flame.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                    MakeLight("TorchLight", new Vector3(x, 2.9f, z), new Color(1f, 0.62f, 0.28f), 4.5f, 13f, root, true);
                }
            // Fireflies
            var ff = MakeLoopParticles("Fireflies", root, fxHolo, new Color(0.7f, 1f, 0.5f, 1f), new Color(0.7f, 1f, 0.5f, 0f), 0.12f, 12f, 5f, 0.3f, 14f, 0f);
            ff.transform.position = new Vector3(0, 2.5f, -20f);
        }

        // ------------------------------------------------------------ torches along the maze

        private static void BuildTorches()
        {
            for (int x = 1; x < TempleLayout.Size - 1; x++)
                for (int y = 1; y < TempleLayout.Size - 1; y++)
                {
                    if (!layout.Open[x, y]) continue;
                    var region = layout.RegionOf[x, y];
                    if (region != Region.Maze && region != Region.Gallery && region != Region.SunVault && region != Region.Vault) continue;
                    if ((x * 7 + y * 13) % 8 != 0) continue;
                    var t = new Vector2Int(x, y);
                    foreach (var d in Dirs)
                    {
                        if (layout.IsOpen(t + d) || layout.Hidden.Contains(t + d)) continue;
                        Vector3 dir = new Vector3(d.x, 0, d.y);
                        AddSconce(lightRoot, TW(t) + dir * (T * 0.5f - 0.5f) + Vector3.up * 3.2f, dir);
                        break;
                    }
                }
            // the hall and the rune chamber get a row of torches too
            foreach (int x in new[] { 12, 14, 16, 18 }) AddSconce(lightRoot, TW(x, 3) + new Vector3(0, 3.3f, 1.5f), Vector3.forward);
            foreach (int y in new[] { 24, 26 }) AddSconce(lightRoot, TW(3, y) + new Vector3(1.5f, 3.3f, 0), Vector3.right);
        }

        // ------------------------------------------------------------ rooms

        private static void Glyphs(Transform parent, Vector3 wallCentre, Vector3 towardsWall, int[] order, float spacing, float size, string name)
        {
            // A row of four carved glyphs on a wall. They glow only while the Echo Lens reveals them.
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.position = wallCentre;
            var tint = new Color(0.4f, 0.95f, 1f);
            var rends = new List<Renderer>();
            Vector3 right = Vector3.Cross(Vector3.up, towardsWall).normalized;   // the viewer's right (he looks towards the wall)
            for (int i = 0; i < order.Length; i++)
            {
                Vector3 pos = wallCentre + right * ((i - (order.Length - 1) * 0.5f) * spacing) - towardsWall * 0.04f;
                var q = FlatQuad("Glyph" + i, holder, pos, Quaternion.LookRotation(towardsWall), size, ParticleMaterial("FX_Hidden_" + order[i], new Color(tint.r, tint.g, tint.b, 0.05f), true, Tex(GenTex + new[] { "glyph_sun", "glyph_moon", "glyph_serpent", "glyph_eye" }[order[i]] + ".png")));
                rends.Add(q.GetComponent<Renderer>());
            }
            var light = MakeLight("RevealLight", wallCentre - towardsWall * 1.2f, tint, 0f, 7f, holder);
            var hg = holder.gameObject.AddComponent<HiddenGlyph>();
            Wire(hg, "renderers", rends);
            Wire(hg, "tint", tint);
            Wire(hg, "revealLight", light);
        }

        private static void BuildVaultsAndPuzzle()
        {
            // ---- sun vault: a floor of spikes in two groups, the key in the far corner
            var spikeRoot = new GameObject("SunVaultSpikes").transform; spikeRoot.SetParent(trapRoot, false);
            for (int x = 1; x <= 3; x++)
                for (int y = 1; y <= 3; y++)
                {
                    var t = new Vector2Int(x, y);
                    if (t == new Vector2Int(3, 1) || t == layout.SunKeyTile) continue;     // entry and key tiles are safe
                    BuildSpikeTrap(TW(t), (x + y) % 2 == 0 ? 0f : 1.5f, spikeRoot);
                }
            // hidden mural of the rune order on the south wall
            Glyphs(decorRoot, TW(2, 1) + new Vector3(0, 3.2f, -T * 0.5f), Vector3.back, new[] { 1, 3, 0, 2 }, 1.1f, 1.0f, "RuneMural_SunVault");
            // text hint in stone: a small carved plaque (emissive) near the entrance
            // ---- rune chamber
            var puzzleGo = new GameObject("RunePuzzle");
            puzzleGo.transform.SetParent(triggerRoot, false);
            runePuzzle = puzzleGo.AddComponent<RunePuzzle>();
            var peds = new List<RunePedestal>
            {
                BuildRunePedestal(0, TW(1, 24), decorRoot, runePuzzle),
                BuildRunePedestal(2, TW(3, 24), decorRoot, runePuzzle),
                BuildRunePedestal(1, TW(1, 26), decorRoot, runePuzzle),
                BuildRunePedestal(3, TW(3, 26), decorRoot, runePuzzle),
            };
            Wire(runePuzzle, "solution", new[] { 1, 3, 0, 2 });
            Wire(runePuzzle, "gate", doorMap[DoorKind.RuneGate]);
            Wire(runePuzzle, "pedestals", peds);
            Glyphs(decorRoot, TW(2, 27) + new Vector3(0, 3.3f, T * 0.5f), Vector3.forward, new[] { 1, 3, 0, 2 }, 1.0f, 0.9f, "RuneMural_Chamber");
        }

        private static void BuildGalleryTraps()
        {
            int y = 29;
            Vector3 Wall(int x, float sideSign) => TW(x, y) + new Vector3(0f, 1.7f, sideSign * (T * 0.5f - 0.2f));
            BuildBlade(TW(2, y), 0f, trapRoot, true);
            BuildSpikeTrap(TW(3, y), 0f, trapRoot);
            BuildSpikeTrap(TW(4, y), 1.5f, trapRoot);
            BuildFlameVent(Wall(5, 1f), Vector3.back, 0f, trapRoot);
            BuildBlade(TW(6, y), Mathf.PI, trapRoot, true);
            BuildSpikeTrap(TW(7, y), 0f, trapRoot);
            BuildFlameVent(Wall(8, -1f), Vector3.forward, 2.1f, trapRoot);
            BuildBlade(TW(9, y), 1.6f, trapRoot, true);
            BuildSpikeTrap(TW(10, y), 1.5f, trapRoot);
        }

        private static void BuildCollectibles()
        {
            MakeCollectible(CollectibleKind.SunKey, TW(layout.SunKeyTile), collectRoot);
            MakeCollectible(CollectibleKind.MoonKey, TW(layout.MoonKeyTile), collectRoot);
            int relics = 0;
            int normalRelics = Mathf.Max(0, 5 - layout.SecretTiles.Count);
            foreach (var t in layout.RelicTiles) { if (relics >= normalRelics) break; MakeCollectible(CollectibleKind.Relic, TW(t), collectRoot); relics++; }
            foreach (var t in layout.SecretTiles) { MakeCollectible(CollectibleKind.Relic, TW(t), collectRoot); relics++; }
            foreach (var t in layout.OrbTiles) MakeCollectible(CollectibleKind.LifeOrb, TW(t), collectRoot);
            relicTotal = relics;
        }

        private static int relicTotal = 5;

        private static void BuildSecrets()
        {
            for (int i = 0; i < layout.SecretWallTiles.Count; i++)
            {
                var wallTile = layout.SecretWallTiles[i];
                var entry = layout.SecretEntryTiles[i];
                Vector3 toAlcove = TW(layout.SecretTiles[i]) - TW(wallTile); toAlcove.y = 0f; toAlcove.Normalize();
                Vector3 c = TW(wallTile);
                var root = new GameObject("SecretWall_" + i);
                root.transform.SetParent(decorRoot, false);
                root.transform.position = c;
                root.transform.rotation = Quaternion.LookRotation(toAlcove);   // local +Z points into the alcove
                var slab = new GameObject("Slab").transform; slab.SetParent(root.transform, false);
                // the slab fills the passage at the dead-end side of the tile
                Prim(PrimitiveType.Cube, "Body", slab, new Vector3(0f, H * 0.5f, -T * 0.5f + 0.5f), new Vector3(T - 0.04f, H - 0.02f, 1.0f), matWallDark, true);
                // glowing glyph on the face that looks into the dead end
                Vector3 facePos = c - toAlcove * (T * 0.5f + 0.03f) + Vector3.up * 2.4f;
                var holder = new GameObject("Glyph").transform; holder.SetParent(root.transform, false);
                var q = FlatQuad("Mark", holder, facePos, Quaternion.LookRotation(toAlcove), 1.5f, ParticleMaterial("FX_Hidden_Secret", new Color(0.4f, 0.95f, 1f, 0.05f), true, Tex(GenTex + "glyph_eye.png")));
                var light = MakeLight("RevealLight", facePos - toAlcove * 1.0f, new Color(0.4f, 0.95f, 1f), 0f, 6f, holder);
                var hg = holder.gameObject.AddComponent<HiddenGlyph>();
                Wire(hg, "renderers", new List<Renderer> { q.GetComponent<Renderer>() });
                Wire(hg, "revealLight", light);
                var sw = root.AddComponent<SecretWall>();
                var sc = root.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 2.2f; sc.center = new Vector3(0f, 1.2f, -T * 0.5f - 0.2f);
                Wire(sw, "slab", slab);
                Wire(sw, "height", H);
                Wire(sw, "revealPoint", q.transform);
            }
        }

        private static void BuildHeartChamber()
        {
            heartObj = BuildHeart(TW(layout.HeartTile), decorRoot);
        }

        // ------------------------------------------------------------ the way out

        private static void BuildEscapeRoute()
        {
            var path = new List<Vector2Int>(layout.EscapePath);
            path.Insert(0, new Vector2Int(17, 15));   // the boulder is released from the far end of the heart chamber, behind the explorer
            path.Insert(1, new Vector2Int(14, 15));
            path.Add(TempleLayout.EntranceTile);
            var points = new List<Vector3>();
            foreach (var t in path) points.Add(TW(t));

            // golden chevrons on the floor, hidden until the heart is taken
            var trailRoot = new GameObject("EscapeTrail").transform;
            trailRoot.SetParent(decorRoot, false);
            var pieces = new List<Renderer>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector3 dir = (b - a).normalized;
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                foreach (float f in new[] { 0f, 0.5f })
                {
                    var q = FlatQuad("Chevron", trailRoot, Vector3.Lerp(a, b, f) + Vector3.up * 0.04f, Quaternion.Euler(90f, yaw, 0f), 1.7f, fxChevron);
                    pieces.Add(q.GetComponent<Renderer>());
                }
            }
            trailObj = trailRoot.gameObject.AddComponent<EscapeTrail>();
            Wire(trailObj, "pieces", pieces);

            // the boulder
            var b0 = new GameObject("RollingBoulder");
            b0.transform.SetParent(decorRoot, false);
            b0.transform.position = points[0];
            var visual = Prim(PrimitiveType.Sphere, "Rock", b0.transform, new Vector3(0f, 1.7f, 0f), Vector3.one * 3.4f, matRockMoss);
            var src = b0.AddComponent<AudioSource>();
            src.clip = AssetKit.Clip("boulder_loop"); src.loop = true; src.playOnAwake = false; src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 4f; src.maxDistance = 45f; src.volume = 1f;
            boulderObj = b0.AddComponent<RollingBoulder>();
            Wire(boulderObj, "visual", visual.transform);
            Wire(boulderObj, "rumbleSource", src);
            var route = new List<Vector3>();
            foreach (var p in points) route.Add(new Vector3(p.x, 0f, p.z));
            boulderObj.SetRoute(route);
            EditorUtility.SetDirty(boulderObj);
            b0.SetActive(false);

            Wire(heartObj, "boulder", boulderObj);
            Wire(heartObj, "trail", trailObj);

            // The escape lasts as long as the route needs at a sprint, plus a margin.
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++) length += Vector3.Distance(points[i], points[i + 1]);
            escapeSeconds = Mathf.Clamp(length / 4.6f + 45f, 100f, 240f);
            Debug.Log("ESCAPE route: " + path.Count + " tiles, " + Mathf.RoundToInt(length) + " m, time limit " + Mathf.RoundToInt(escapeSeconds) + " s");
        }

        private static float escapeSeconds = 150f;

        // ------------------------------------------------------------ triggers

        private static GameObject MakeZone(string name, Vector3 centre, Vector3 size, ZoneKind kind, string message, TempleDoor door = null, float seconds = 4f)
        {
            var go = new GameObject("Zone_" + name);
            go.transform.SetParent(triggerRoot, false);
            go.transform.position = centre;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.size = size;
            var z = go.AddComponent<ZoneTrigger>();
            Wire(z, "kind", (int)kind);
            Wire(z, "message", message);
            Wire(z, "messageSeconds", seconds);
            if (door != null) Wire(z, "door", door);
            return go;
        }

        private static void BuildTriggers()
        {
            MakeZone("HallSeal", TW(15, 2, 2f), new Vector3(8f, 4f, 3f), ZoneKind.HallSeal, "The entrance has sealed itself behind you.\nThere must be another way out...", doorMap[DoorKind.Entrance], 5f);
            MakeZone("Tip", TW(15, 5, 2f), new Vector3(4f, 4f, 3f), ZoneKind.Hint, "Press Q: the Echo Lens reveals hidden carvings.\nFind the Sun Key.", null, 6f);
            MakeZone("SpikeWarning", TW(4, 1, 2f), new Vector3(2f, 4f, 2f), ZoneKind.Hint, "Spike tiles glow red just before they strike.\nWait for them to sink, then cross.", null, 5f);
            MakeZone("RuneChamber", TW(2, 25, 2f), new Vector3(10f, 4f, 18f), ZoneKind.RuneChamber, "The Rune Chamber. Four pedestals, four symbols.\nA hidden mural shows their order (Echo Lens).", null, 6f);
            MakeZone("GalleryWarning", TW(1, 29, 2f), new Vector3(4f, 4f, 4f), ZoneKind.Hint, "The Trap Gallery. Blades, spikes and flames.\nTime your run.", null, 4f);
            MakeZone("Exit", new Vector3(0f, 2f, -4.5f), new Vector3(8f, 4f, 3f), ZoneKind.ExitGate, "", null);
            foreach (var t in layout.CheckpointTiles) MakeZone("Checkpoint", TW(t, 1.5f), new Vector3(4f, 3f, 4f), ZoneKind.Checkpoint, "");
        }

        // ------------------------------------------------------------ navigation

        private static void AddNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemy")));
            surface.BuildNavMesh();
        }

        // ------------------------------------------------------------ player and cameras

        private static GameObject BuildPlayer(out ThirdPersonCameraController camCtrl, out PlayerInteractor interactor, out GameObject camerasRoot, out Camera introCam)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            var root = new GameObject("Player");
            root.tag = "Player";
            root.transform.position = new Vector3(0f, 0.05f, -17f);

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.75f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.35f; cc.skinWidth = 0.05f;
            var hp = root.AddComponent<Health>();
            Wire(hp, "maxHealth", 100f);
            interactor = root.AddComponent<PlayerInteractor>();
            root.AddComponent<Flashlight>();
            var pc = root.AddComponent<PlayerController>();
            var lens = root.AddComponent<EchoLens>();

            var modelHolder = new GameObject("ModelRoot");
            modelHolder.transform.SetParent(root.transform, false);
            var model = BuildHeroModel("Model", modelHolder.transform, ExplorerLook, AssetKit.BuildExplorerController());
            var animator = model.GetComponent<Animator>();
            AddExplorerGear(animator);

            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(root.transform, false);
            camTarget.transform.localPosition = new Vector3(0, 1.55f, 0);

            // Lantern (spot) and a soft glow around the explorer
            var torchGo = new GameObject("Lantern");
            torchGo.transform.SetParent(root.transform, false);
            torchGo.transform.localPosition = new Vector3(0.2f, 1.5f, 0.3f);
            var torch = torchGo.AddComponent<Light>();
            torch.type = LightType.Spot; torch.spotAngle = 78f; torch.innerSpotAngle = 30f;
            torch.range = 24f; torch.intensity = 11f; torch.color = new Color(1f, 0.86f, 0.62f);
            torch.shadows = LightShadows.Hard;
            Wire(root.GetComponent<Flashlight>(), "torch", torch);
            Wire(root.GetComponent<Flashlight>(), "toggleClip", AssetKit.Clip("click"));
            MakeLight("Halo", root.transform.position + Vector3.up * 1.6f, new Color(1f, 0.78f, 0.5f), 1.7f, 8f, root.transform);

            var ringPrefab = BuildScanRingPrefab();
            Wire(lens, "ringPrefab", ringPrefab);

            Wire(pc, "animator", animator);
            Wire(pc, "footstepClips", AssetKit.Clips("footstep_0", "footstep_1", "footstep_2"));
            Wire(pc, "hurtClip", AssetKit.Clip("hurt"));
            Wire(pc, "jumpClip", AssetKit.Clip("jump"));
            SetLayerRecursive(root, playerLayer);

            // ---- cameras
            camerasRoot = new GameObject("Cameras");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camerasRoot.transform, false);
            camGo.transform.position = new Vector3(0.6f, 2.4f, -21f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 260f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.3f);
            var urpData = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = true;
            urpData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urpData.renderShadows = true;

            var normal = BuildVirtualCamera("CM Normal", camerasRoot.transform, root.transform, camTarget.transform, 3.3f, Vector3.zero, 62f, out CinemachineOrbitalFollow normalOrbit);
            var aim = BuildVirtualCamera("CM Aim", camerasRoot.transform, root.transform, camTarget.transform, 3.0f, Vector3.zero, 56f, out CinemachineOrbitalFollow aimOrbit);
            camerasRoot.SetActive(false);   // switched on by the intro sequence

            var ctrlGo = new GameObject("CameraController");
            camCtrl = ctrlGo.AddComponent<ThirdPersonCameraController>();
            Wire(camCtrl, "normalCamera", normal);
            Wire(camCtrl, "aimCamera", aim);
            Wire(camCtrl, "normalOrbit", normalOrbit);
            Wire(camCtrl, "aimOrbit", aimOrbit);
            Wire(camCtrl, "playerTarget", root.transform);
            Wire(camCtrl, "shoulderNormal", 0.4f);
            Wire(camCtrl, "lookMask", new LayerMask { value = ~((1 << playerLayer) | (1 << LayerMask.NameToLayer("Enemy")) | (1 << 2)) });
            Wire(pc, "cameraController", camCtrl);

            // Intro camera: looks at the great portal from the jungle path
            var introGo = new GameObject("IntroCamera");
            introGo.transform.position = new Vector3(5f, 2.4f, -27f);
            introGo.transform.rotation = Quaternion.LookRotation(new Vector3(-0.15f, 0.08f, 1f), Vector3.up);
            introCam = introGo.AddComponent<Camera>();
            introCam.fieldOfView = 55f; introCam.allowHDR = true;
            introGo.AddComponent<AudioListener>();
            var introData = introGo.AddComponent<UniversalAdditionalCameraData>();
            introData.renderPostProcessing = true;
            introData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            return root;
        }

        public static readonly AssetKit.HeroLook ExplorerLook = new AssetKit.HeroLook
        {
            Id = "Explorer", Skin = new Color(0.9f, 0.76f, 0.64f), Shirt = new Color(0.62f, 0.5f, 0.3f), Pants = new Color(0.22f, 0.18f, 0.12f),
            Hair = new Color(0.07f, 0.05f, 0.04f), EyeGlow = Color.black, SkinSmooth = 0.3f
        };

        /// <summary>A brimmed hat, a satchel and the glowing Echo Lens band on the left wrist.</summary>
        private static void AddExplorerGear(Animator animator)
        {
            var leather = AssetKit.Mat("Leather", new Color(0.32f, 0.2f, 0.1f), 0.2f);
            var hatMat = AssetKit.Mat("HatFelt", new Color(0.36f, 0.26f, 0.14f), 0.1f);
            var band = AssetKit.Mat("HatBand", new Color(0.12f, 0.08f, 0.05f), 0.2f);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                var hat = new GameObject("Hat").transform;
                hat.SetParent(head, false);
                hat.position = head.position + Vector3.up * 0.1f;
                hat.rotation = Quaternion.identity;
                Prim(PrimitiveType.Cylinder, "Brim", hat, new Vector3(0f, 0.01f, 0.02f), new Vector3(0.58f, 0.006f, 0.58f), hatMat);
                Prim(PrimitiveType.Cylinder, "Crown", hat, new Vector3(0f, 0.055f, 0.02f), new Vector3(0.3f, 0.05f, 0.3f), hatMat);
                Prim(PrimitiveType.Cylinder, "Band", hat, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.305f, 0.014f, 0.305f), band);
                hat.localScale = Vector3.one / Mathf.Max(0.001f, head.lossyScale.x);
            }
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (spine != null)
            {
                var bag = new GameObject("Satchel").transform;
                bag.SetParent(spine, false);
                bag.position = spine.position + new Vector3(0f, -0.12f, 0f);
                bag.rotation = animator.transform.rotation;
                bag.position += animator.transform.rotation * new Vector3(0.22f, 0f, -0.22f);
                Prim(PrimitiveType.Cube, "Bag", bag, Vector3.zero, new Vector3(0.28f, 0.25f, 0.12f), leather);
                Prim(PrimitiveType.Cube, "Flap", bag, new Vector3(0f, 0.09f, 0.0f), new Vector3(0.29f, 0.09f, 0.125f), band);
                bag.localScale = Vector3.one / Mathf.Max(0.001f, spine.lossyScale.x);
            }
            Transform wrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (wrist != null && lower != null)
            {
                var gauntlet = new GameObject("EchoLensBand").transform;
                gauntlet.SetParent(wrist, false);
                Vector3 axis = (wrist.position - lower.position).normalized;
                gauntlet.position = wrist.position - axis * 0.05f;
                gauntlet.rotation = Quaternion.identity;
                var ring = MeshObject("Ring", gauntlet, MakeTorus("Torus_Wrist", 0.05f, 0.013f, 24, 8), glowCyan, Vector3.zero, Vector3.one);
                var ring2 = MeshObject("Ring2", gauntlet, MakeTorus("Torus_Wrist", 0.05f, 0.013f, 24, 8), matMetal, axis * 0.03f, Vector3.one * 1.1f);
                ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
                ring2.transform.rotation = ring.transform.rotation;
                gauntlet.localScale = Vector3.one / Mathf.Max(0.001f, wrist.lossyScale.x);
                MakeLight("BandGlow", gauntlet.position, new Color(0.3f, 0.9f, 1f), 0.9f, 3f, gauntlet);
            }
        }

        private static GameObject BuildScanRingPrefab()
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "ScanRing";
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = fxRing;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return SavePrefab(q, "ScanRing");
        }

        // ------------------------------------------------------------ managers

        private static GameManager BuildManagers(GameObject player)
        {
            var mgr = new GameObject("GameManager");
            var gm = mgr.AddComponent<GameManager>();
            var saver = mgr.AddComponent<ProgressSaver>();
            Wire(saver, "collectRoot", collectRoot);
            Wire(saver, "playerHealth", player.GetComponent<Health>());
            Wire(saver, "puzzle", runePuzzle);
            Wire(saver, "heart", heartObj);
            Wire(gm, "progress", saver);
            Wire(gm, "relicsTotal", relicTotal);
            Wire(gm, "escapeSecondsSetting", escapeSeconds);

            var trackerGo = new GameObject("ObjectiveTracker");
            tracker = trackerGo.AddComponent<ObjectiveTracker>();
            var targets = new GameObject("ObjectiveTargets").transform;
            targets.SetParent(trackerGo.transform, false);
            var list = new List<Transform>();
            Vector3[] spots =
            {
                TW(layout.SunKeyTile), TW(TempleLayout.SunDoorTile), TW(layout.RuneCentreTile), TW(layout.MoonKeyTile),
                TW(TempleLayout.MoonDoorTile), TW(layout.HeartTile), TW(TempleLayout.EntranceTile), TW(TempleLayout.EntranceTile)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var t = new GameObject("Stage" + i).transform;
                t.SetParent(targets, false);
                t.position = spots[i];
                list.Add(t);
            }
            Wire(tracker, "stageTargets", list);
            return gm;
        }
    }
}
