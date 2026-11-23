using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using NED2024.Core;

namespace NED2024.EditorTools
{
    /// <summary>Everything specific to the Blender character: imports, materials, animator controllers and the zombie ragdoll.</summary>
    public static partial class AssetKit
    {
        public const string HeroFbx = Root + "Art/Models/Hero/Zahoor.fbx";
        public const string Ual1 = Root + "Art/Animations/UAL1/AnimationLibrary_Unity_Standard.fbx";
        public const string Ual2 = Root + "Art/Animations/UAL2/UAL2_Standard.fbx";

        // ------------------------------------------------------------ imports

        public static void PrepareHeroImports()
        {
            foreach (var tex in new[] { "short02_diffuse", "young_lightskinned_male_diffuse3", "brown_eye" })
            {
                var ti = AssetImporter.GetAtPath(Root + "Art/Models/Hero/" + tex + ".png") as TextureImporter;
                if (ti == null) continue;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }

            var hero = (ModelImporter)AssetImporter.GetAtPath(HeroFbx);
            hero.animationType = ModelImporterAnimationType.Human;
            hero.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            hero.importAnimation = false;
            hero.SaveAndReimport();
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(HeroFbx);
            Debug.Log("HERO avatar valid=" + (avatar != null && avatar.isValid) + " human=" + (avatar != null && avatar.isHuman));

            foreach (var path in new[] { Ual1, Ual2 })
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                var clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    bool loop = c.name.Contains("_Loop") || c.name.Contains("Idle");
                    c.loopTime = loop;
                    c.loopPose = loop;
                    // Keep the animation in place; movement comes from the CharacterController / NavMeshAgent.
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = false;   // 'Body Orientation': the character faces the way its GameObject faces
                    c.rotationOffset = 0f;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                var a = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                Debug.Log("UAL avatar " + System.IO.Path.GetFileName(path) + " valid=" + (a != null && a.isValid) + " human=" + (a != null && a.isHuman));
            }
        }

        public static AnimationClip FindClip(string fbxPath, string shortName)
        {
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
            {
                if (c.name.StartsWith("__preview__")) continue;
                if (c.name == shortName || c.name.EndsWith("|" + shortName)) return c;
            }
            Debug.LogError("Animation clip not found: " + shortName + " in " + fbxPath);
            return null;
        }

        private static AnimationClip C1(string n) { return FindClip(Ual1, n); }
        private static AnimationClip C2(string n) { return FindClip(Ual2, n); }

        // ------------------------------------------------------------ materials

        public struct HeroLook
        {
            public Color Skin, Shirt, Pants, Hair, EyeGlow;
            public float SkinSmooth;
            public string Id;
        }

        public static HeroLook SurvivorLook = new HeroLook
        {
            Id = "Survivor", Skin = new Color(0.9f, 0.76f, 0.64f), Shirt = new Color(0.62f, 0.6f, 0.55f), Pants = new Color(0.07f, 0.07f, 0.08f),
            Hair = new Color(0.07f, 0.05f, 0.04f), EyeGlow = Color.black, SkinSmooth = 0.3f
        };

        private static Material HeroMat(string name, Color tint, Texture2D albedo, float smooth, bool cutout, bool doubleSided, Color? emission = null)
        {
            var m = Mat(name, tint, smooth, 0f, albedo);
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
            }
            if (doubleSided) m.SetFloat("_Cull", 0f);
            if (emission.HasValue && emission.Value.maxColorComponent > 0.01f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Replaces the imported materials of the character with URP Lit materials chosen by name.</summary>
        public static void ApplyHeroMaterials(GameObject model, HeroLook look)
        {
            string dir = Root + "Art/Models/Hero/";
            var skinTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "young_lightskinned_male_diffuse3.png");
            var hairTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "short02_diffuse.png");
            var browTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "eyebrow001.png");
            var lashTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "eyelashes02.png");
            var eyeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "brown_eye.png");

            var skin = HeroMat("Hero_Skin_" + look.Id, look.Skin, skinTex, look.SkinSmooth, false, false);
            var brow = HeroMat("Hero_Brow_" + look.Id, look.Hair, browTex, 0.1f, true, true);
            var lash = HeroMat("Hero_Lash_" + look.Id, look.Hair, lashTex, 0.1f, true, true);
            var eye = HeroMat("Hero_Eye_" + look.Id, Color.white, eyeTex, 0.8f, false, false, look.EyeGlow);
            var hair = HeroMat("Hero_Hair_" + look.Id, look.Hair, hairTex, 0.25f, true, true);
            var shirt = HeroMat("Hero_Shirt_" + look.Id, look.Shirt, null, 0.15f, false, false);
            var pants = HeroMat("Hero_Pants_" + look.Id, look.Pants, null, 0.15f, false, false);

            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    if (n.Contains("eyebrow")) mats[i] = brow;
                    else if (n.Contains("eyelash")) mats[i] = lash;
                    else if (n.Contains("low-poly") || n.Contains("eye")) mats[i] = eye;
                    else if (n.Contains("short02") || n.Contains("hair")) mats[i] = hair;
                    else if (n.Contains("shirt")) mats[i] = shirt;
                    else if (n.Contains("pants")) mats[i] = pants;
                    else mats[i] = skin;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        // ------------------------------------------------------------ animator controllers

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, float speed = 1f)
        {
            var s = sm.AddState(name);
            s.motion = motion;
            s.speed = speed;
            return s;
        }

        private static BlendTree Tree1D(AnimatorController ctrl, string name, string param, params (Motion m, float t, float speed)[] items)
        {
            var tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = param, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            foreach (var it in items) tree.AddChild(it.m, it.t);
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) children[i].timeScale = items[i].speed;
            tree.children = children;
            return tree;
        }

        private static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, bool exit, float exitTime = 0.9f, float duration = 0.12f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = exit; t.exitTime = exitTime; t.duration = duration; t.hasFixedDuration = true;
            return t;
        }

        private static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, string trigger, float duration = 0.1f)
        {
            var t = sm.AddAnyStateTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            t.hasExitTime = false; t.duration = duration; t.canTransitionToSelf = false; t.hasFixedDuration = true;
            return t;
        }

        /// <summary>
        /// Explorer controller (no weapon): locomotion blend tree (idle, walk, jog, sprint), jump states, a hit reaction and death.
        /// </summary>
        public static AnimatorController BuildExplorerController()
        {
            EnsureFolder(AnimDir);
            string path = AnimDir + "/Explorer.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AimPitch", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Reverse", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Aiming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Weapon", AnimatorControllerParameterType.Int);
            foreach (var t in new[] { "Jump", "Die", "Hit" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var tree = Tree1D(ctrl, "Locomotion", "Speed",
                (C1("Idle_Loop"), 0f, 1f), (C1("Walk_Loop"), 0.36f, 1.15f), (C1("Jog_Fwd_Loop"), 0.64f, 1f), (C1("Sprint_Loop"), 1f, 1f));
            var loco = AddState(sm, "Locomotion", tree);
            loco.speedParameterActive = true; loco.speedParameter = "Reverse";
            sm.defaultState = loco;

            var jumpStart = AddState(sm, "JumpStart", C1("Jump_Start"));
            var jumpLoop = AddState(sm, "JumpLoop", C1("Jump_Loop"));
            var jumpLand = AddState(sm, "JumpLand", C1("Jump_Land"));
            Any(sm, jumpStart, "Jump", 0.08f);
            Go(jumpStart, jumpLoop, true, 0.85f, 0.1f);
            var land = Go(jumpLoop, jumpLand, false, 0f, 0.08f); land.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            Go(jumpLand, loco, true, 0.7f, 0.12f);
            var landFromStart = Go(jumpStart, jumpLand, true, 0.6f, 0.05f);
            landFromStart.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

            var hit = AddState(sm, "Hit", C1("Hit_Chest"), 1.2f);
            Any(sm, hit, "Hit", 0.05f); Go(hit, loco, true, 0.85f, 0.12f);

            var death = AddState(sm, "Death", C1("Death01"));
            Any(sm, death, "Die", 0.15f);

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
