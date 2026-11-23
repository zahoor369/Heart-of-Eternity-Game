using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NED2024.EditorTools
{
    /// <summary>Editor helpers for materials, procedural textures, imported models and animator controllers.</summary>
    public static partial class AssetKit
    {
        public const string Root = "Assets/_Project/";
        public const string MatDir = Root + "Art/Materials";
        public const string TexDir = Root + "Art/Textures";
        public const string PrefabDir = Root + "Prefabs";
        public const string AnimDir = Root + "Animations";

        public static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");

        public static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        // ------------------------------------------------------------ materials

        public static Material Mat(string name, Color color, float smoothness = 0.15f, float metallic = 0f, Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null)
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = LitShader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (tiling.HasValue)
            {
                m.SetTextureScale("_BaseMap", tiling.Value);
                m.SetTextureScale("_BumpMap", tiling.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Emissive(string name, Color color, float intensity)
        {
            var m = Mat(name, color * 0.4f, 0.3f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Horror palette for the Kenney materials. The packs import in bright, toy-like colours; these are darker and duller.
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            { "stone", new Color(0.30f, 0.31f, 0.32f) }, { "stoneDark", new Color(0.2f, 0.2f, 0.22f) },
            { "wood", new Color(0.27f, 0.19f, 0.13f) }, { "woodDark", new Color(0.15f, 0.1f, 0.08f) },
            { "woodBark", new Color(0.2f, 0.15f, 0.11f) }, { "woodInner", new Color(0.35f, 0.27f, 0.18f) },
            { "metal", new Color(0.18f, 0.19f, 0.21f) }, { "foliage", new Color(0.13f, 0.21f, 0.06f) },
            { "leafsGreen", new Color(0.13f, 0.21f, 0.07f) }, { "brick", new Color(0.33f, 0.18f, 0.15f) },
            { "Brick", new Color(0.3f, 0.2f, 0.17f) }, { "Dark_brick", new Color(0.2f, 0.14f, 0.12f) },
            { "Glass", new Color(0.08f, 0.1f, 0.12f) }, { "grass", new Color(0.12f, 0.2f, 0.1f) },
            { "dirt", new Color(0.2f, 0.15f, 0.1f) }, { "_defaultMat", new Color(0.22f, 0.22f, 0.22f) },
            { "paintRed", new Color(0.35f, 0.06f, 0.05f) }, { "paintWhite", new Color(0.55f, 0.55f, 0.52f) },
            { "paintGreen", new Color(0.12f, 0.22f, 0.14f) }, { "paintBlue", new Color(0.1f, 0.14f, 0.25f) },
            { "paintYellow", new Color(0.5f, 0.4f, 0.1f) }, { "plastic", new Color(0.1f, 0.1f, 0.11f) },
            { "window", new Color(0.05f, 0.07f, 0.09f) }, { "carTire", new Color(0.04f, 0.04f, 0.04f) },
            { "lightFront", new Color(0.45f, 0.42f, 0.32f) }, { "lightBack", new Color(0.35f, 0.08f, 0.06f) },
            { "lightBlue", new Color(0.12f, 0.2f, 0.45f) },
        };

        /// <summary>Replaces imported Standard materials with dark URP Lit materials so models are not pink or toy-bright.</summary>
        public static void FixMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) { mats[i] = Mat("Fallback_Grey", new Color(0.25f, 0.25f, 0.25f)); continue; }
                    string baseName = m.name.Replace(" (Instance)", "");
                    if (m.shader != null && m.shader.name.Contains("Universal Render Pipeline")) continue;

                    if (baseName == "light")
                    {
                        mats[i] = Emissive("Imp_lamp_glow", new Color(1f, 0.72f, 0.38f), 2.5f);
                        continue;
                    }

                    Color c;
                    if (!Palette.TryGetValue(baseName, out c))
                    {
                        c = new Color(0.5f, 0.5f, 0.5f);
                        if (m.HasProperty("_Color")) c = m.GetColor("_Color");
                        else if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
                        // Desaturate and darken unknown colours.
                        Color.RGBToHSV(c, out float h, out float s, out float v);
                        c = Color.HSVToRGB(h, s * 0.6f, v * 0.7f);
                    }
                    float smooth = baseName.StartsWith("paint") || baseName == "window" || baseName == "Glass" ? 0.7f : 0.2f;
                    float metal = baseName == "metal" || baseName.StartsWith("paint") ? 0.5f : 0f;
                    mats[i] = Mat("Imp_" + baseName, c, smooth, metal);
                }
                r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------ procedural textures

        /// <summary>Makes a tileable noise texture plus a matching normal map and saves both as PNG files.</summary>
        public static void MakeNoiseTextures(string name, Color dark, Color light, float scale, float speckle, out Texture2D albedo, out Texture2D normal)
        {
            EnsureFolder(TexDir);
            string albedoPath = TexDir + "/" + name + "_albedo.png";
            string normalPath = TexDir + "/" + name + "_normal.png";
            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (albedo != null && normal != null) return;

            const int size = 512;
            var height = new float[size * size];
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = 0f, amp = 1f, freq = scale, total = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        // Sampling noise on a torus makes the texture tile without a visible seam.
                        n += amp * TileNoise(u, v, freq, o * 17f);
                        total += amp; amp *= 0.5f; freq *= 2f;
                    }
                    n /= total;
                    float grain = (Mathf.PerlinNoise(x * 0.9f, y * 0.9f) - 0.5f) * speckle;
                    float h = Mathf.Clamp01(n + grain);
                    height[y * size + x] = h;
                    colors[y * size + x] = Color.Lerp(dark, light, h);
                }
            }

            var a = new Texture2D(size, size, TextureFormat.RGBA32, true);
            a.SetPixels(colors);
            File.WriteAllBytes(albedoPath, a.EncodeToPNG());

            var nrm = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hl = height[y * size + (x - 1 + size) % size];
                    float hr = height[y * size + (x + 1) % size];
                    float hd = height[((y - 1 + size) % size) * size + x];
                    float hu = height[((y + 1) % size) * size + x];
                    Vector3 nv = new Vector3((hl - hr) * 3f, (hd - hu) * 3f, 1f).normalized;
                    nrm[y * size + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
                }
            }
            var nt = new Texture2D(size, size, TextureFormat.RGBA32, true);
            nt.SetPixels(nrm);
            File.WriteAllBytes(normalPath, nt.EncodeToPNG());
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(nt);

            AssetDatabase.ImportAsset(albedoPath);
            AssetDatabase.ImportAsset(normalPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        }

        private static float TileNoise(float u, float v, float freq, float offset)
        {
            // Blend four shifted samples so the edges match.
            float x = u * freq, y = v * freq;
            float w = freq;
            float a = Mathf.PerlinNoise(x + offset, y + offset);
            float b = Mathf.PerlinNoise(x - w + offset, y + offset);
            float c = Mathf.PerlinNoise(x + offset, y - w + offset);
            float d = Mathf.PerlinNoise(x - w + offset, y - w + offset);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static Sprite MakeVignetteSprite()
        {
            string path = TexDir + "/vignette_red.png";
            if (!File.Exists(path))
            {
                EnsureFolder(TexDir);
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x / (size - 1f) - 0.5f) * 2f, dy = (y / (size - 1f) - 0.5f) * 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.SmoothStep(0.45f, 1.25f, d);
                        tex.SetPixel(x, y, new Color(0.7f, 0f, 0f, a));
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------ models

        public static T Load<T>(string relativePath) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(Root + relativePath);
        }

        public static AudioClip Clip(string relativePath)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Resources/Audio/" + relativePath + ".ogg");
            if (c == null) Debug.LogWarning("Missing audio clip: " + relativePath);
            return c;
        }

        public static AudioClip[] Clips(params string[] relativePaths)
        {
            return relativePaths.Select(Clip).Where(c => c != null).ToArray();
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Spawns an imported model, scales it so its height or footprint matches the request, and sits it on the ground.</summary>
        public static GameObject Place(string modelPath, Vector3 position, float yaw, float scale, Transform parent, bool box = true, float targetHeight = 0f)
        {
            var prefab = Load<GameObject>("Art/Models/" + modelPath);
            if (prefab == null) { Debug.LogWarning("Missing model " + modelPath); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = position;
            FixMaterials(go);
            if (targetHeight > 0f)
            {
                var b0 = BoundsOf(go);
                if (b0.size.y > 0.001f) go.transform.localScale *= targetHeight / b0.size.y;
            }
            var b = BoundsOf(go);
            go.transform.position += new Vector3(position.x - b.center.x, position.y - b.min.y, position.z - b.center.z);
            if (box)
            {
                var b2 = BoundsOf(go);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = go.transform.InverseTransformPoint(b2.center);
                var ls = go.transform.lossyScale;
                bc.size = new Vector3(b2.size.x / ls.x, b2.size.y / ls.y, b2.size.z / ls.z);
            }
            go.isStatic = true;
            return go;
        }
    }
}
