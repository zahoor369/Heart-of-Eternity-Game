using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using NED2024.Core;
using NED2024.Temple;

namespace NED2024.EditorTools
{
    /// <summary>Doors, keys, relics, the heart, the rune pedestals, traps, torches and the sentinel guardians.</summary>
    public static partial class GameBuilder
    {
        private static Material fxGlyphGold, fxGlyphSilver, fxRedGlow, matSteel, matStone;

        private static void BuildPropMaterials()
        {
            fxGlyphGold = ParticleMaterial("FX_DoorSun", new Color(1f, 0.75f, 0.25f, 0.85f), true, Tex(GenTex + "glyph_sun.png"));
            fxGlyphSilver = ParticleMaterial("FX_DoorMoon", new Color(0.7f, 0.85f, 1f, 0.85f), true, Tex(GenTex + "glyph_moon.png"));
            fxRedGlow = ParticleMaterial("FX_RedWarning", new Color(1f, 0.15f, 0.08f, 0.8f), true, SoftDot());
            matSteel = AssetKit.Mat("SpikeSteel", new Color(0.62f, 0.64f, 0.68f), 0.8f, 1f);
            matStone = AssetKit.Mat("PedestalStone", new Color(0.55f, 0.52f, 0.47f), 0.25f, 0f, Tex(TempleTex + "old_sandstone_02_albedo.jpg"), Tex(TempleTex + "old_sandstone_02_normal.jpg"));
        }

        // ------------------------------------------------------------ torches and braziers

        private static GameObject sconcePrefab;

        private static GameObject GetSconce()
        {
            if (sconcePrefab != null) return sconcePrefab;
            var root = new GameObject("WallSconce");
            Prim(PrimitiveType.Cube, "Bracket", root.transform, new Vector3(0f, 0f, 0.12f), new Vector3(0.12f, 0.12f, 0.34f), matMetal);
            Prim(PrimitiveType.Cylinder, "Bowl", root.transform, new Vector3(0f, 0.05f, 0.3f), new Vector3(0.3f, 0.08f, 0.3f), matGoldDark);
            Prim(PrimitiveType.Cylinder, "Stem", root.transform, new Vector3(0f, 0.2f, 0.3f), new Vector3(0.07f, 0.16f, 0.07f), matMetal);
            var flame = MakeLoopParticles("Flame", root.transform, fxFlame, new Color(1f, 0.7f, 0.3f, 0.9f), new Color(1f, 0.25f, 0.05f, 0f), 0.38f, 18f, 0.8f, 0.45f, 0.06f, -0.15f, true, 8f);
            flame.transform.localPosition = new Vector3(0f, 0.3f, 0.3f);
            flame.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(0f, 0.55f, 0.6f);
            l.type = LightType.Point; l.color = new Color(1f, 0.66f, 0.32f); l.intensity = 3.2f; l.range = 11f; l.shadows = LightShadows.None;
            var f = l.gameObject.AddComponent<NED2024.Environment.LightFlicker>();
            Wire(f, "amount", 0.22f);
            sconcePrefab = SavePrefab(root, "WallSconce");
            return sconcePrefab;
        }

        private static GameObject AddSconce(Transform parent, Vector3 wallPoint, Vector3 towardsWall)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(GetSconce());
            go.transform.SetParent(parent, false);
            go.transform.position = wallPoint;
            go.transform.rotation = Quaternion.LookRotation(-towardsWall, Vector3.up);   // local +Z points into the room
            return go;
        }

        private static GameObject AddBrazier(Transform parent, Vector3 pos, float height = 1.25f)
        {
            var root = new GameObject("Brazier");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var pit = PlaceModel("stone_fire_pit", pos, 0f, height * 0.55f, root.transform, ModelMat("stone_fire_pit", new Color(0.8f, 0.78f, 0.74f)), true);
            var flame = MakeLoopParticles("Flame", root.transform, fxFlame, new Color(1f, 0.72f, 0.3f, 0.95f), new Color(1f, 0.2f, 0.05f, 0f), 0.9f, 28f, 1.1f, 0.9f, 0.25f, -0.2f, true, 10f);
            flame.transform.position = pos + Vector3.up * (height * 0.5f);
            flame.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var l = MakeLight("Light", pos + Vector3.up * (height + 0.8f), new Color(1f, 0.62f, 0.28f), 5.5f, 14f, root.transform, true);
            return root;
        }

        // ------------------------------------------------------------ doors

        private static TempleDoor BuildDoor(DoorSpec spec, Transform parent)
        {
            Vector3 c = TempleLayout.TileToWorld(spec.Tile);
            var root = new GameObject("Door_" + spec.Kind).transform;
            root.SetParent(parent, false);
            root.position = c;
            root.rotation = Quaternion.Euler(0f, spec.AlongX ? 90f : 0f, 0f);

            const float width = 3.96f, height = 5.8f, thick = 0.9f;
            var slab = new GameObject("Slab").transform;
            slab.SetParent(root, false);
            var body = Prim(PrimitiveType.Cube, "Body", slab, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, thick), matWallHall, true);
            var glows = new List<Renderer>();
            // glowing seams along the edges and a cross line
            foreach (float sx in new[] { -1f, 1f })
                glows.Add(Prim(PrimitiveType.Cube, "SeamSide", slab, new Vector3(sx * (width * 0.5f - 0.12f), height * 0.5f, 0f), new Vector3(0.07f, height - 0.4f, thick + 0.06f), glowAmber).GetComponent<Renderer>());
            glows.Add(Prim(PrimitiveType.Cube, "SeamTop", slab, new Vector3(0f, height - 0.2f, 0f), new Vector3(width - 0.3f, 0.07f, thick + 0.06f), glowAmber).GetComponent<Renderer>());
            glows.Add(Prim(PrimitiveType.Cube, "SeamMid", slab, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.06f, height - 0.4f, thick + 0.1f), glowAmber).GetComponent<Renderer>());

            Material glyph = spec.Kind == DoorKind.SunDoor ? fxGlyphGold : (spec.Kind == DoorKind.MoonDoor ? fxGlyphSilver : null);
            if (glyph != null)
                foreach (float side in new[] { -1f, 1f })
                {
                    var q = FlatQuad("Glyph", slab, root.position + root.rotation * new Vector3(0f, 3.3f, side * (thick * 0.5f + 0.02f)), root.rotation * Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f), 2.3f, glyph);
                    q.transform.SetParent(slab, true);
                }
            var lg = MakeLight("DoorGlow", c + Vector3.up * 3.2f, new Color(1f, 0.15f, 0.1f), 2.2f, 8f, root);

            var door = root.gameObject.AddComponent<TempleDoor>();
            Wire(door, "kind", (int)spec.Kind);
            Wire(door, "slab", slab);
            Wire(door, "glowRenderers", glows);
            Wire(door, "glowLight", lg);
            Wire(door, "height", height);
            Wire(door, "startOpen", spec.Kind == DoorKind.Entrance);
            return door;
        }

        // ------------------------------------------------------------ collectibles

        private static GameObject BuildKeyVisual(Transform parent, bool sun)
        {
            var root = new GameObject(sun ? "SunKey" : "MoonKey").transform;
            root.SetParent(parent, false);
            Material metal = sun ? matGold : AssetKit.Mat("SilverKey", new Color(0.78f, 0.86f, 0.97f), 0.6f, 0.4f);
            Material glow = sun ? glowAmber : glowCyan;
            var ring = MeshObject("Ring", root, MakeTorus("Torus_Key", 0.2f, 0.05f, 32, 10), metal, new Vector3(0f, 0.32f, 0f), Vector3.one);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Gem", root, new Vector3(0f, 0.32f, 0f), Vector3.one * 0.14f, glow);
            Prim(PrimitiveType.Cube, "Shaft", root, new Vector3(0f, -0.12f, 0f), new Vector3(0.07f, 0.58f, 0.07f), metal);
            for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, "Tooth", root, new Vector3(0.09f, -0.3f + i * 0.1f, 0f), new Vector3(0.14f, 0.05f, 0.05f), metal);
            root.localScale = Vector3.one * 1.5f;
            return root.gameObject;
        }

        private static GameObject MakeCollectible(CollectibleKind kind, Vector3 floorPos, Transform parent)
        {
            var root = new GameObject(kind + "_Pickup");
            root.transform.SetParent(parent, false);
            root.transform.position = floorPos;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = Vector3.up * 1.05f;
            Color lightColor = new Color(1f, 0.8f, 0.3f);
            float lightIntensity = 3f;

            switch (kind)
            {
                case CollectibleKind.SunKey: { var k = BuildKeyVisual(visual, true); k.transform.localPosition = Vector3.zero; lightColor = new Color(1f, 0.75f, 0.25f); lightIntensity = 4f; break; }
                case CollectibleKind.MoonKey: { var k = BuildKeyVisual(visual, false); k.transform.localPosition = Vector3.zero; lightColor = new Color(0.45f, 0.8f, 1f); lightIntensity = 4f; break; }
                case CollectibleKind.Relic:
                {
                    MeshObject("Crystal", visual, MakeCrystal("Crystal_Relic", 0.62f, 0.2f, 6), glowAmber, Vector3.zero, Vector3.one);
                    var ring = MeshObject("Ring", visual, MakeTorus("Torus_Relic", 0.3f, 0.02f, 32, 8), matGold, Vector3.zero, Vector3.one);
                    ring.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                    lightIntensity = 2.4f;
                    break;
                }
                case CollectibleKind.LifeOrb:
                    Prim(PrimitiveType.Sphere, "Orb", visual, Vector3.zero, Vector3.one * 0.42f, glowGreen);
                    lightColor = new Color(0.3f, 1f, 0.55f); lightIntensity = 2.2f;
                    break;
            }
            MakeLight("Light", floorPos + Vector3.up * 1.2f, lightColor, lightIntensity, 6.5f, root.transform);
            if (kind != CollectibleKind.LifeOrb)
            {
                var halo = FlatQuad("Halo", root.transform, floorPos + Vector3.up * 1.1f, Quaternion.identity, 1.5f, fxHolo);
                halo.AddComponent<Billboard>();
                halo.GetComponent<MeshRenderer>().sharedMaterial = ParticleMaterial("FX_Halo_" + kind, new Color(lightColor.r, lightColor.g, lightColor.b, 0.55f), true, SoftDot());
            }
            if (kind == CollectibleKind.SunKey || kind == CollectibleKind.MoonKey)
            {
                Prim(PrimitiveType.Cylinder, "Pedestal", root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.35f, 0.9f), matStone, true);
                Prim(PrimitiveType.Cylinder, "PedestalTop", root.transform, new Vector3(0f, 0.72f, 0f), new Vector3(1.1f, 0.03f, 1.1f), matGoldDark);
            }
            var sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = kind == CollectibleKind.LifeOrb ? 0.9f : 1.1f; sc.center = Vector3.up * 1.0f;
            var col = root.AddComponent<Collectible>();
            Wire(col, "kind", (int)kind);
            Wire(col, "visual", visual);
            Wire(col, "healAmount", 35f);
            return root;
        }

        // ------------------------------------------------------------ heart of eternity and the rune pedestals

        private static HeartOfEternity BuildHeart(Vector3 floorPos, Transform parent)
        {
            var root = new GameObject("HeartOfEternity");
            root.transform.SetParent(parent, false);
            root.transform.position = floorPos;
            Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.45f, 3.2f), matStone, true);
            Prim(PrimitiveType.Cylinder, "BaseTop", root.transform, new Vector3(0f, 0.92f, 0f), new Vector3(2.5f, 0.05f, 2.5f), matGoldDark);
            var ringFloor = MeshObject("RuneRing", root.transform, MakeTorus("Torus_HeartBase", 1.15f, 0.04f, 48, 8), glowCyan, new Vector3(0f, 0.97f, 0f), Vector3.one);

            var crystalRoot = new GameObject("Crystal");
            crystalRoot.transform.SetParent(root.transform, false);
            crystalRoot.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            crystalRoot.AddComponent<Float>();
            MeshObject("Gem", crystalRoot.transform, MakeCrystal("Crystal_Heart", 1.7f, 0.55f, 8, 0.5f), AssetKit.Emissive("Glow_Heart", new Color(0.15f, 0.7f, 1f), 1.7f), Vector3.zero, Vector3.one)
                .AddComponent<Spin>();
            var core = Prim(PrimitiveType.Sphere, "Core", crystalRoot.transform, Vector3.zero, Vector3.one * 0.45f, AssetKit.Emissive("Glow_HeartCore", new Color(1f, 0.7f, 0.25f), 2.5f));
            for (int i = 0; i < 2; i++)
            {
                var r = MeshObject("Orbit" + i, crystalRoot.transform, MakeTorus("Torus_Orbit" + i, 0.85f + i * 0.2f, 0.022f, 48, 8), glowAmber, Vector3.zero, Vector3.one);
                r.transform.localRotation = Quaternion.Euler(60f * (i + 1), 25f * i, 0f);
                var sp = r.AddComponent<Spin>();
                Wire(sp, "degreesPerSecond", new Vector3(0f, (i == 0 ? 55f : -40f), 0f));
            }
            var halo = FlatQuad("Halo", root.transform, floorPos + Vector3.up * 2.5f, Quaternion.identity, 6f, ParticleMaterial("FX_HeartHalo", new Color(0.4f, 0.9f, 1f, 0.5f), true, SoftDot()));
            halo.AddComponent<Billboard>();
            var dust = MakeLoopParticles("Motes", root.transform, fxHolo, new Color(0.6f, 0.95f, 1f, 0.9f), new Color(1f, 0.85f, 0.4f, 0f), 0.09f, 22f, 3f, 0.25f, 2.2f, -0.02f);
            dust.transform.position = floorPos + Vector3.up * 1.6f;
            var light = MakeLight("HeartLight", floorPos + Vector3.up * 2.7f, new Color(0.5f, 0.92f, 1f), 7f, 18f, root.transform);

            var sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 2.4f; sc.center = Vector3.up * 1.5f;
            var heart = root.AddComponent<HeartOfEternity>();
            Wire(heart, "crystal", crystalRoot);
            Wire(heart, "heartLight", light);
            return heart;
        }

        private static RunePedestal BuildRunePedestal(int glyph, Vector3 floorPos, Transform parent, RunePuzzle puzzle)
        {
            var root = new GameObject("RunePedestal_" + glyph);
            root.transform.SetParent(parent, false);
            root.transform.position = floorPos;
            Prim(PrimitiveType.Cylinder, "Column", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 0.55f, 0.95f), matStone, true);
            var top = Prim(PrimitiveType.Cylinder, "Top", root.transform, new Vector3(0f, 1.12f, 0f), new Vector3(1.1f, 0.04f, 1.1f), glowCyan);
            var holo = FlatQuad("Hologram", root.transform, floorPos + Vector3.up * 2.35f, Quaternion.identity, 1.5f, fxGlyphs[glyph]);
            holo.AddComponent<Billboard>();
            holo.AddComponent<Float>();
            var l = MakeLight("Glow", floorPos + Vector3.up * 1.6f, new Color(0.3f, 0.8f, 1f), 2.6f, 6f, root.transform);
            var sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 1.6f; sc.center = Vector3.up * 1f;
            var ped = root.AddComponent<RunePedestal>();
            Wire(ped, "glyph", glyph);
            Wire(ped, "puzzle", puzzle);
            Wire(ped, "glowRenderers", new List<Renderer> { top.GetComponent<Renderer>() });
            Wire(ped, "glowLight", l);
            return ped;
        }

        // ------------------------------------------------------------ traps

        private static SpikeTrap BuildSpikeTrap(Vector3 tileCentre, float offset, Transform parent)
        {
            var root = new GameObject("SpikeTrap");
            root.transform.SetParent(parent, false);
            root.transform.position = tileCentre;
            Prim(PrimitiveType.Cube, "Plate", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(3.8f, 0.08f, 3.8f), matMetal);
            var spikes = MeshObject("Spikes", root.transform, MakeSpikeField(), matSteel, new Vector3(0f, -0.92f, 0f), Vector3.one);
            var warn = FlatQuad("Warning", root.transform, tileCentre + Vector3.up * 0.1f, Quaternion.Euler(90f, 0f, 0f), 3.7f, fxRedGlow);
            var trap = root.AddComponent<SpikeTrap>();
            Wire(trap, "spikes", spikes.transform);
            Wire(trap, "warningGlow", warn.GetComponent<Renderer>());
            Wire(trap, "offset", offset);
            return trap;
        }

        private static SwingingBlade BuildBlade(Vector3 tileCentre, float phase, Transform parent, bool alongX)
        {
            var root = new GameObject("SwingingBlade");
            root.transform.SetParent(parent, false);
            root.transform.position = tileCentre;
            root.transform.rotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, H - 0.15f, 0f);
            Prim(PrimitiveType.Cylinder, "Mount", root.transform, new Vector3(0f, H - 0.1f, 0f), new Vector3(0.5f, 0.12f, 0.5f), matMetal);
            Prim(PrimitiveType.Cube, "Arm", pivot, new Vector3(0f, -2.5f, 0f), new Vector3(0.1f, 5f, 0.1f), matMetal);
            var centre = new GameObject("BladeCentre").transform;
            centre.SetParent(pivot, false);
            centre.localPosition = new Vector3(0f, -5.0f, 0f);
            Prim(PrimitiveType.Cube, "Blade", centre, Vector3.zero, new Vector3(2.0f, 1.5f, 0.12f), matSteel);
            Prim(PrimitiveType.Cube, "Edge", centre, new Vector3(0f, -0.76f, 0f), new Vector3(2.02f, 0.05f, 0.14f), glowRed);
            Prim(PrimitiveType.Cube, "Boss", centre, new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.35f, 0.3f), matGoldDark);
            var blade = root.AddComponent<SwingingBlade>();
            Wire(blade, "pivot", pivot);
            Wire(blade, "bladeCentre", centre);
            Wire(blade, "phase", phase);
            return blade;
        }

        private static FlameVent BuildFlameVent(Vector3 wallPoint, Vector3 towardsRoom, float offset, Transform parent)
        {
            var root = new GameObject("FlameVent");
            root.transform.SetParent(parent, false);
            root.transform.position = wallPoint;
            root.transform.rotation = Quaternion.LookRotation(towardsRoom, Vector3.up);   // local +Z = across the corridor
            Prim(PrimitiveType.Cube, "Vent", root.transform, new Vector3(0f, 0f, 0.12f), new Vector3(1.1f, 0.7f, 0.3f), matMetal);
            Prim(PrimitiveType.Cube, "Grill", root.transform, new Vector3(0f, 0f, 0.28f), new Vector3(0.8f, 0.4f, 0.05f), glowRed);
            var flames = MakeLoopParticles("Jet", root.transform, fxFlame, new Color(1f, 0.75f, 0.35f, 0.95f), new Color(1f, 0.2f, 0.05f, 0f), 1.1f, 60f, 0.7f, 5f, 0.15f, 0f, true, 6f);
            flames.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            flames.transform.localRotation = Quaternion.identity;
            flames.Stop();
            var l = MakeLight("JetLight", wallPoint + towardsRoom * 1.6f, new Color(1f, 0.55f, 0.2f), 0f, 9f, root.transform);
            var vent = root.AddComponent<FlameVent>();
            Wire(vent, "flames", flames);
            Wire(vent, "glow", l);
            Wire(vent, "offset", offset);
            return vent;
        }

        // ------------------------------------------------------------ sentinel guardians

        private static GameObject sentinelPrefab;

        private static GameObject GetSentinel()
        {
            if (sentinelPrefab != null) return sentinelPrefab;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            var root = new GameObject("Sentinel");
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.55f; agent.height = 2.5f; agent.acceleration = 10f; agent.angularSpeed = 200f; agent.stoppingDistance = 0.8f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;   // switched on by SentinelAI.Start once the NavMesh exists
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 1.25f, 0f); cap.radius = 0.55f; cap.height = 2.5f;

            Material stone = matWallDark;
            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            Prim(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.75f, 0f), new Vector3(1.05f, 1.15f, 0.6f), stone);
            Prim(PrimitiveType.Cube, "Pelvis", body, new Vector3(0f, 1.05f, 0f), new Vector3(0.8f, 0.35f, 0.5f), matStoneDark);
            Prim(PrimitiveType.Cube, "ShoulderL", body, new Vector3(-0.68f, 2.2f, 0f), new Vector3(0.45f, 0.35f, 0.55f), matStoneDark);
            Prim(PrimitiveType.Cube, "ShoulderR", body, new Vector3(0.68f, 2.2f, 0f), new Vector3(0.45f, 0.35f, 0.55f), matStoneDark);
            Prim(PrimitiveType.Sphere, "Core", body, new Vector3(0f, 1.8f, 0.31f), Vector3.one * 0.3f, glowAmber);
            var head = Prim(PrimitiveType.Cube, "Head", body, new Vector3(0f, 2.55f, 0.02f), new Vector3(0.55f, 0.5f, 0.5f), stone);
            var eye = Prim(PrimitiveType.Cube, "Visor", body, new Vector3(0f, 2.57f, 0.27f), new Vector3(0.42f, 0.09f, 0.04f), glowAmber);
            Prim(PrimitiveType.Cube, "Crest", body, new Vector3(0f, 2.88f, 0f), new Vector3(0.12f, 0.2f, 0.55f), matGoldDark);

            Transform Limb(string name, Vector3 pivotPos, Vector3 size, Material m, bool fist)
            {
                var p = new GameObject(name).transform; p.SetParent(root.transform, false); p.localPosition = pivotPos;
                Prim(PrimitiveType.Cube, name + "Mesh", p, new Vector3(0f, -size.y * 0.5f, 0f), size, m);
                if (fist) Prim(PrimitiveType.Cube, name + "Fist", p, new Vector3(0f, -size.y - 0.1f, 0f), new Vector3(size.x * 1.5f, 0.3f, size.z * 1.5f), matStoneDark);
                return p;
            }
            var legL = Limb("LegL", new Vector3(-0.28f, 1.0f, 0f), new Vector3(0.38f, 1.0f, 0.4f), stone, false);
            var legR = Limb("LegR", new Vector3(0.28f, 1.0f, 0f), new Vector3(0.38f, 1.0f, 0.4f), stone, false);
            var armL = Limb("ArmL", new Vector3(-0.72f, 2.15f, 0f), new Vector3(0.3f, 1.1f, 0.32f), stone, true);
            var armR = Limb("ArmR", new Vector3(0.72f, 2.15f, 0f), new Vector3(0.3f, 1.1f, 0.32f), stone, true);

            var light = new GameObject("EyeLight").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.6f, 0.6f);
            light.type = LightType.Point; light.color = new Color(1f, 0.65f, 0.15f); light.intensity = 2f; light.range = 6f;

            var ai = root.AddComponent<SentinelAI>();
            Wire(ai, "body", body);
            Wire(ai, "legL", legL); Wire(ai, "legR", legR); Wire(ai, "armL", armL); Wire(ai, "armR", armR);
            Wire(ai, "eyeRenderer", eye.GetComponent<Renderer>());
            Wire(ai, "eyeLight", light);
            Wire(ai, "eyes", head.transform);
            Wire(ai, "sightMask", new LayerMask { value = ~((1 << enemyLayer) | (1 << 2)) });
            SetLayerRecursive(root, enemyLayer);
            sentinelPrefab = SavePrefab(root, "Sentinel");
            return sentinelPrefab;
        }

        private static SentinelAI SpawnSentinel(Vector3 pos, List<Vector3> route, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(GetSentinel());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var ai = go.GetComponent<SentinelAI>();
            ai.SetWaypoints(route);
            EditorUtility.SetDirty(ai);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
            return ai;
        }
    }
}
