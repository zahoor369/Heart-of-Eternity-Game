using System.IO;
using UnityEditor;
using UnityEngine;

namespace NED2024.EditorTools
{
    /// <summary>Texture import settings and the material library of the temple.</summary>
    public static partial class GameBuilder
    {
        private static Material matWallMaze, matWallHall, matWallDark, matWallHeart, matWallRune;
        private static Material matFloorMaze, matFloorHall, matFloorRune, matFloorHeart, matCeiling, matTrim, matGround, matRockMoss;
        private static Material matGold, matGoldDark, matMetal, matStoneDark;
        private static Material glowCyan, glowAmber, glowGreen, glowRed, glowWhite;
        private static Material fxRing, fxChevron, fxFlame, fxDust, fxHolo, fxBeam;
        private static Material[] fxGlyphs = new Material[4];

        private const string TempleTex = "Assets/_Project/Art/Textures/Temple/";
        private const string GenTex = "Assets/_Project/Art/Textures/Generated/";

        // ------------------------------------------------------------ imports

        private static void PrepareTempleImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/Textures", "Assets/_Project/Art/Models/Temple" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool changed = false;

                if (n.EndsWith("_normal")) { if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; } }
                else if (n.EndsWith("_mg") || n.EndsWith("_ao"))
                {
                    if (ti.sRGBTexture) { ti.sRGBTexture = false; changed = true; }
                }
                else if (path.Contains("/Generated/"))
                {
                    bool icon = n.StartsWith("icon_") || n.StartsWith("glyph_");
                    var wanted = icon ? TextureImporterType.Sprite : TextureImporterType.Default;
                    if (ti.textureType != wanted) { ti.textureType = wanted; changed = true; }
                    if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                    if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
                    if (ti.mipmapEnabled && icon) { ti.mipmapEnabled = false; changed = true; }
                }
                else if (n.EndsWith("albedoa"))
                {
                    if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                }
                int max = n.EndsWith("_mg") ? 1024 : 2048;
                if (ti.maxTextureSize != max) { ti.maxTextureSize = max; changed = true; }
                if (changed) ti.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Art/Models/Temple" }))
            {
                var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (mi == null) continue;
                if (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.importAnimation = false;
                    mi.SaveAndReimport();
                }
            }
        }

        // ------------------------------------------------------------ material helpers

        private static Texture2D Tex(string path) { return AssetDatabase.LoadAssetAtPath<Texture2D>(path); }

        /// <summary>A URP Lit material from one set of scanned PBR textures (albedo, normal, smoothness, ambient occlusion).</summary>
        private static Material Pbr(string matName, string id, Color tint, float normal = 1f, float smoothness = 1f, string folder = TempleTex)
        {
            var m = AssetKit.Mat(matName, tint, smoothness, 0f, Tex(folder + id + "_albedo.jpg"), Tex(folder + id + "_normal.jpg"));
            var mg = Tex(folder + id + "_mg.png");
            if (mg != null)
            {
                m.SetTexture("_MetallicGlossMap", mg);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", smoothness);
            }
            var ao = Tex(folder + id + "_ao.jpg");
            if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 0.9f); }
            m.SetFloat("_BumpScale", normal);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Material for an imported prop: textures live next to the model, named model_albedo / _normal / _mg.</summary>
        private static Material ModelMat(string model, Color tint, string suffix = "", float smoothMul = 1f)
        {
            string dir = "Assets/_Project/Art/Models/Temple/" + model + "/";
            Texture2D albedo = Tex(dir + model + suffix + "_albedo.jpg");
            if (albedo == null) albedo = Tex(dir + model + suffix + "_albedo.png");
            if (albedo == null) albedo = Tex(dir + model + suffix + "_albedoA.png");
            var m = AssetKit.Mat("Prop_" + model + suffix, tint, 0.35f * smoothMul, 0f, albedo, Tex(dir + model + suffix + "_normal.png"));
            var mg = Tex(dir + model + suffix + "_mg.png");
            if (mg != null) { m.SetTexture("_MetallicGlossMap", mg); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", smoothMul); }
            bool cutout = AssetDatabase.GetAssetPath(albedo).EndsWith("A.png");
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetFloat("_Cull", 0f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------ the material library

        private static void BuildTempleMaterials()
        {
            // Walls: warm sandstone for the maze, richer gold stone for the halls, dark mossy stone for the dangerous parts.
            matWallMaze = Pbr("Wall_Maze", "mossy_sandstone", new Color(0.82f, 0.8f, 0.74f));
            matWallHall = Pbr("Wall_Hall", "old_sandstone_02", new Color(0.9f, 0.82f, 0.7f));
            matWallDark = Pbr("Wall_Dark", "mossy_stone_wall", new Color(0.78f, 0.82f, 0.8f));
            matWallHeart = Pbr("Wall_Heart", "mossy_sandstone", new Color(0.72f, 0.78f, 0.82f));
            matWallRune = Pbr("Wall_Rune", "old_sandstone_02", new Color(0.7f, 0.78f, 0.9f));

            matFloorMaze = Pbr("Floor_Maze", "mossy_brick_floor", new Color(0.75f, 0.78f, 0.72f));
            matFloorHall = Pbr("Floor_Hall", "patterned_paving_02", new Color(0.85f, 0.85f, 0.85f));
            matFloorRune = Pbr("Floor_Rune", "marble_mosaic_tiles", new Color(0.8f, 0.85f, 0.95f));
            matFloorHeart = Pbr("Floor_Heart", "marble_mosaic_tiles", new Color(0.95f, 0.85f, 0.65f));
            matCeiling = Pbr("Ceiling", "slate_floor_03", new Color(0.45f, 0.45f, 0.48f));
            matTrim = Pbr("Trim", "old_sandstone_02", new Color(0.55f, 0.5f, 0.45f));
            matGround = Pbr("Ground_Jungle", "forest_ground_04", new Color(0.55f, 0.65f, 0.5f));
            matRockMoss = Pbr("Rock_Mossy", "rock_pitted_mossy", new Color(0.55f, 0.62f, 0.5f), 1f, 0.8f);

            matGold = AssetKit.Mat("Gold", new Color(1f, 0.74f, 0.28f), 0.6f, 0.45f);
            matGoldDark = AssetKit.Mat("GoldDark", new Color(0.62f, 0.44f, 0.16f), 0.5f, 0.3f);
            matMetal = AssetKit.Mat("DarkMetal", new Color(0.2f, 0.21f, 0.24f), 0.55f, 0.5f);
            matStoneDark = AssetKit.Mat("StoneDark", new Color(0.18f, 0.18f, 0.2f), 0.3f, 0f);

            glowCyan = AssetKit.Emissive("Glow_Cyan", new Color(0.2f, 0.85f, 1f), 3.2f);
            glowAmber = AssetKit.Emissive("Glow_Amber", new Color(1f, 0.62f, 0.15f), 3.2f);
            glowGreen = AssetKit.Emissive("Glow_Green", new Color(0.3f, 1f, 0.55f), 3.2f);
            glowRed = AssetKit.Emissive("Glow_Red", new Color(1f, 0.15f, 0.1f), 3.2f);
            glowWhite = AssetKit.Emissive("Glow_White", new Color(0.9f, 0.95f, 1f), 3.5f);

            fxRing = ParticleMaterial("FX_ScanRing", new Color(0.4f, 0.95f, 1f, 0.9f), true, Tex(GenTex + "scan_ring.png"));
            fxChevron = ParticleMaterial("FX_Chevron", new Color(1f, 0.78f, 0.25f, 0.9f), true, Tex(GenTex + "chevron.png"));
            fxFlame = ParticleMaterial("FX_Flame", new Color(1f, 0.6f, 0.2f, 0.9f), true);
            fxDust = ParticleMaterial("FX_Dust", new Color(0.8f, 0.85f, 0.9f, 0.35f), true);
            fxHolo = ParticleMaterial("FX_Holo", new Color(0.4f, 0.95f, 1f, 0.8f), true);
            fxBeam = ParticleMaterial("FX_Beam", new Color(1f, 0.85f, 0.45f, 0.35f), true, Tex(GenTex + "glow_strip.png"));
            string[] glyphNames = { "glyph_sun", "glyph_moon", "glyph_serpent", "glyph_eye" };
            for (int i = 0; i < 4; i++) fxGlyphs[i] = ParticleMaterial("FX_" + glyphNames[i], new Color(0.4f, 0.95f, 1f, 0.8f), true, Tex(GenTex + glyphNames[i] + ".png"));
            AssetDatabase.SaveAssets();
        }
    }
}
