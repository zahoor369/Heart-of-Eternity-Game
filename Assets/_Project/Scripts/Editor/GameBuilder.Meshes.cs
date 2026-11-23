using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NED2024.EditorTools
{
    /// <summary>Procedural meshes (rings, crystals, spike fields) and small helpers for building props from primitives.</summary>
    public static partial class GameBuilder
    {
        private static Mesh torusMeshCache;

        private static Mesh MakeTorus(string name, float radius, float tube, int segments = 40, int sides = 12)
        {
            string path = AssetKit.Root + "Art/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments * Mathf.PI * 2f;
                Vector3 centre = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u)) * radius;
                for (int j = 0; j <= sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    verts.Add(centre + n * tube); norms.Add(n); uvs.Add(new Vector2((float)i / segments * 4f, (float)j / sides));
                }
            }
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            var m = new Mesh { name = name };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            SaveMeshAsset(m, name);
            return m;
        }

        /// <summary>A pointed crystal: a bipyramid with a few sides, wider in the middle.</summary>
        private static Mesh MakeCrystal(string name, float height, float radius, int sides = 6, float belt = 0.45f)
        {
            string path = AssetKit.Root + "Art/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            Vector3 top = new Vector3(0, height * 0.5f, 0), bottom = new Vector3(0, -height * 0.5f, 0);
            float beltY = (belt - 0.5f) * height;
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); norms.Add(n); norms.Add(n); norms.Add(n);
                tris.AddRange(new[] { i, i + 1, i + 2 });
            }
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f, a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, beltY, Mathf.Sin(a0) * radius), p1 = new Vector3(Mathf.Cos(a1) * radius, beltY, Mathf.Sin(a1) * radius);
                Tri(top, p1, p0);       // upper facet (wound so the normal points outwards)
                Tri(bottom, p0, p1);    // lower facet
            }
            var m = new Mesh { name = name };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            SaveMeshAsset(m, name);
            return m;
        }

        /// <summary>A 5 x 5 field of steel spikes (square pyramids) that fits on one 4 m tile.</summary>
        private static Mesh MakeSpikeField()
        {
            const string name = "SpikeField";
            string path = AssetKit.Root + "Art/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            const float baseHalf = 0.17f, height = 0.9f;
            for (int gx = -2; gx <= 2; gx++)
                for (int gz = -2; gz <= 2; gz++)
                {
                    Vector3 o = new Vector3(gx * 0.72f, 0f, gz * 0.72f);
                    Vector3 apex = o + Vector3.up * height;
                    Vector3[] c = { o + new Vector3(-baseHalf, 0, -baseHalf), o + new Vector3(baseHalf, 0, -baseHalf), o + new Vector3(baseHalf, 0, baseHalf), o + new Vector3(-baseHalf, 0, baseHalf) };
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 a = c[i], b = c[(i + 1) % 4];
                        Vector3 n = Vector3.Cross(apex - a, b - a).normalized;
                        if (Vector3.Dot(n, (a + b) * 0.5f - o) < 0f) { n = -n; var t = a; a = b; b = t; }
                        int k = verts.Count;
                        verts.Add(a); verts.Add(apex); verts.Add(b); norms.Add(n); norms.Add(n); norms.Add(n);
                        // keep winding consistent with the normal
                        if (Vector3.Dot(Vector3.Cross(apex - a, b - a), n) < 0f) tris.AddRange(new[] { k, k + 2, k + 1 }); else tris.AddRange(new[] { k, k + 1, k + 2 });
                    }
                }
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            SaveMeshAsset(m, name);
            return m;
        }

        // ------------------------------------------------------------ primitive helpers

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A flat quad (glyph, glow, trail piece). Faces along -forward of the rotation, like a sign facing the viewer.</summary>
        private static GameObject FlatQuad(string name, Transform parent, Vector3 pos, Quaternion rot, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(size, size, 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Places an imported prop, scaled to a height and standing on the ground at pos.</summary>
        private static GameObject PlaceModel(string model, Vector3 pos, float yaw, float height, Transform parent, Material mat, bool collider = true)
        {
            var prefab = AssetKit.Load<GameObject>("Art/Models/Temple/" + model + "/" + model + ".fbx");
            if (prefab == null) { Debug.LogWarning("Missing model " + model); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = pos;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            var b0 = AssetKit.BoundsOf(go);
            if (height > 0f && b0.size.y > 0.001f) go.transform.localScale *= height / b0.size.y;
            var b = AssetKit.BoundsOf(go);
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            if (collider)
            {
                var b2 = AssetKit.BoundsOf(go);
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
