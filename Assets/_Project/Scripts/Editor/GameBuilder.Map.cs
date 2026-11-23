using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NED2024.EditorTools
{
    public static partial class GameBuilder
    {
        private static Sprite MakeIconSprite(string name, System.Func<float, float, float> alphaAt)
        {
            string path = AssetKit.TexDir + "/" + name + ".png";
            AssetKit.EnsureFolder(AssetKit.TexDir);
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(alphaAt((x + 0.5f) / s * 2f - 1f, (y + 0.5f) / s * 2f - 1f))));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectTransform MapIcon(string name, Transform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }
    }
}
