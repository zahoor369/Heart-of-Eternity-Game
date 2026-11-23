using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NED2024.Core;

namespace NED2024.EditorTools
{
    /// <summary>
    /// Builds the whole game from code: textures, materials, the temple, the props and traps, the Main Menu scene and the Game scene.
    /// Run from the menu "NED2024 > Build Everything" or from the command line with -executeMethod.
    /// </summary>
    public static partial class GameBuilder
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        public const string MenuScenePath = ScenesDir + "/MainMenu.unity";
        public const string GameScenePath = ScenesDir + "/Game.unity";

        private static VolumeProfile postProfile;

        [MenuItem("NED2024/Build Everything")]
        public static void BuildEverything()
        {
            AssetKit.EnsureFolder(ScenesDir);
            AssetKit.EnsureFolder(AssetKit.PrefabDir);
            UIKit.Init();

            GraphicsSetup.ConfigureLayers();
            GraphicsSetup.CreateRenderPipelines();
            postProfile = GraphicsSetup.CreatePostProcessing();
            AssetKit.PrepareHeroImports();
            PrepareTempleImports();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildTempleMaterials();
            BuildMenuScene();
            BuildGameScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("BUILD_EVERYTHING_DONE");
        }

        // ------------------------------------------------------------ small helpers

        public static void Wire(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("Wire: no field '" + field + "' on " + target.GetType().Name); return; }
            switch (value)
            {
                case null: p.objectReferenceValue = null; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Vector3 v: p.vector3Value = v; break;
                case Vector4 v4: p.vector4Value = v4; break;
                case Color c: p.colorValue = c; break;
                case LayerMask m: p.intValue = m.value; break;
                case Object single: p.objectReferenceValue = single; break;   // must come before IEnumerable: Transform is enumerable
                case int[] ints:
                    p.arraySize = ints.Length;
                    for (int k = 0; k < ints.Length; k++) p.GetArrayElementAtIndex(k).intValue = ints[k];
                    break;
                case System.Collections.IEnumerable list when !(value is string):
                    var items = new List<object>();
                    foreach (var o in list) items.Add(o);
                    p.arraySize = items.Count;
                    for (int k = 0; k < items.Count; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = items[k] as Object;
                    break;
                default: Debug.LogError("Wire: unsupported type " + value.GetType()); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        private static Light MakeLight(string name, Vector3 pos, Color color, float intensity, float range, Transform parent, bool flicker = false, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (flicker)
            {
                var f = go.AddComponent<NED2024.Environment.LightFlicker>();
                Wire(f, "amount", 0.22f);
            }
            return l;
        }

        // ------------------------------------------------------------ particles

        private static Material ParticleMaterial(string name, Color tint, bool additive, Texture2D texture = null)
        {
            string path = AssetKit.MatDir + "/" + name + ".mat";
            AssetKit.EnsureFolder(AssetKit.MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            var tex = texture != null ? texture : SoftDot();
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);                       // transparent
            m.SetFloat("_Blend", additive ? 2f : 0f);         // additive or alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D SoftDot()
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetKit.TexDir + "/Generated/soft_glow.png");
        }

        /// <summary>A looping particle system (flames, dust, fireflies).</summary>
        private static ParticleSystem MakeLoopParticles(string name, Transform parent, Material mat, Color start, Color end, float size, float rate, float life, float speed, float radius, float gravity, bool cone = false, float angle = 12f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = cone ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
            shape.radius = radius; shape.angle = angle;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(end, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.1f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            AssetKit.EnsureFolder(AssetKit.PrefabDir);
            string path = AssetKit.PrefabDir + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static T SaveMeshAsset<T>(T mesh, string name) where T : Object
        {
            string dir = AssetKit.Root + "Art/Meshes";
            AssetKit.EnsureFolder(dir);
            string path = dir + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------ characters

        /// <summary>Instantiates the Blender character (humanoid rig) with URP materials and the given animator.</summary>
        private static GameObject BuildHeroModel(string name, Transform parent, AssetKit.HeroLook look, RuntimeAnimatorController controller, float scale = 1f)
        {
            var prefab = AssetKit.Load<GameObject>("Art/Models/Hero/Zahoor.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            AssetKit.ApplyHeroMaterials(go, look);

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AssetKit.HeroFbx);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return go;
        }

        private static CinemachineCamera BuildVirtualCamera(string name, Transform parent, Transform follow, Transform lookAt, float radius, Vector3 offset, float fov, out CinemachineOrbitalFollow orbit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cm = go.AddComponent<CinemachineCamera>();
            cm.Follow = follow;
            cm.LookAt = lookAt;
            var lens = cm.Lens;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 260f;
            cm.Lens = lens;

            // Orbital Follow: the camera circles the player on a sphere. The mouse changes the two angles.
            orbit = go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = radius;
            orbit.TargetOffset = offset;
            var tracker = orbit.TrackerSettings;
            tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.25f, 0.2f, 0.25f);
            orbit.TrackerSettings = tracker;

            go.AddComponent<CinemachineHardLookAt>();

            // Deoccluder pulls the camera in when a wall is between it and the player (important in narrow corridors).
            var deo = go.AddComponent<CinemachineDeoccluder>();
            deo.CollideAgainst = 1 << 0;
            deo.IgnoreTag = "Player";
            var avoid = deo.AvoidObstacles;
            avoid.Enabled = true;
            avoid.CameraRadius = 0.3f;
            avoid.DistanceLimit = 0f;
            deo.AvoidObstacles = avoid;
            return cm;
        }
    }
}
