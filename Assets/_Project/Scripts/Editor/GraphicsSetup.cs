using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NED2024.EditorTools
{
    /// <summary>Creates the URP assets (Low, Medium, High), the post-processing profile and the project layers.</summary>
    public static class GraphicsSetup
    {
        public const string SettingsDir = "Assets/_Project/Settings";
        public const string VolumePath = SettingsDir + "/TemplePostProcessing.asset";

        public static RenderPipelineAsset[] Tiers = new RenderPipelineAsset[3];

        public static void ConfigureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            SetLayer(layers, 8, "Player");
            SetLayer(layers, 9, "Enemy");
            SetLayer(layers, 10, "Debris");
            tagManager.ApplyModifiedProperties();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            var p = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = name;
        }

        public static void CreateRenderPipelines()
        {
            AssetKit.EnsureFolder(SettingsDir);
            string[] names = { "Low", "Medium", "High" };
            float[] shadowDistance = { 35f, 55f, 80f };
            int[] msaa = { 1, 2, 4 };
            int[] shadowRes = { 1024, 2048, 4096 };
            float[] renderScale = { 0.85f, 1f, 1f };

            for (int i = 0; i < 3; i++)
            {
                string rdPath = SettingsDir + "/URP_" + names[i] + "_Renderer.asset";
                string rpPath = SettingsDir + "/URP_" + names[i] + ".asset";
                AssetDatabase.DeleteAsset(rdPath);
                AssetDatabase.DeleteAsset(rpPath);

                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                rd.renderingMode = RenderingMode.ForwardPlus;   // many torches: Forward+ handles lots of lights per pixel
                AssetDatabase.CreateAsset(rd, rdPath);

                if (i >= 1) AddSsao(rd, i == 2);

                var rp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(rp, rpPath);

                var so = new SerializedObject(rp);
                SetFloat(so, "m_ShadowDistance", shadowDistance[i]);
                SetInt(so, "m_MSAA", msaa[i]);
                SetInt(so, "m_MainLightShadowmapResolution", shadowRes[i]);
                SetInt(so, "m_AdditionalLightsShadowmapResolution", i == 0 ? 512 : (i == 1 ? 1024 : 2048));   // atlas for point/spot light shadows
                SetInt(so, "m_AdditionalLightsPerObjectLimit", i == 0 ? 4 : 6);
                SetBool(so, "m_SupportsHDR", true);
                SetBool(so, "m_SoftShadowsSupported", i > 0);
                SetBool(so, "m_AdditionalLightShadowsSupported", i > 0);
                SetFloat(so, "m_RenderScale", renderScale[i]);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(rp);
                Tiers[i] = rp;
            }
            AssetDatabase.SaveAssets();

            // Medium is the default; the settings menu switches between the three at runtime.
            GraphicsSettings.defaultRenderPipeline = Tiers[1];
            for (int q = 0; q < QualitySettings.names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, false);
                QualitySettings.renderPipeline = Tiers[1];
            }
            QualitySettings.SetQualityLevel(2, false);
        }

        private static void AddSsao(UniversalRendererData rd, bool high)
        {
            try
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, rd);
                AssetDatabase.SaveAssets();
                var so = new SerializedObject(rd);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string guid, out long fileId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = fileId;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(rd);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Could not add SSAO: " + e.Message);
            }
        }

        public static VolumeProfile CreatePostProcessing()
        {
            AssetKit.EnsureFolder(SettingsDir);
            AssetDatabase.DeleteAsset(VolumePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.95f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.5f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.25f);
            color.contrast.Override(18f);
            color.saturation.Override(8f);
            color.colorFilter.Override(new Color(0.96f, 1f, 1f));

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.85f, 1.05f, 1.15f, -0.04f));    // teal shadows
            smh.highlights.Override(new Vector4(1.15f, 1.04f, 0.85f, 0.05f));  // warm highlights

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.14f);

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.05f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; else Debug.LogWarning("URP property missing: " + name); }
        private static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; else Debug.LogWarning("URP property missing: " + name); }
        private static void SetBool(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; else Debug.LogWarning("URP property missing: " + name); }
    }
}
