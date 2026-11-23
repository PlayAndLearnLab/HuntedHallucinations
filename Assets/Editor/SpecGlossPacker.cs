// SpecGlossPacker.cs
// Put this file in any folder named "Editor" inside Assets (e.g. Assets/Editor/SpecGlossPacker.cs).
//
// Usage: in the Project window, select the specular + glossiness textures (you can select the
// textures of several zombies at once, or a whole folder's worth), then right-click >
// Zombie Tools > Pack Specular + Gloss.
//
// For every matching pair it writes <name>_SpecGloss.png next to the specular map:
//   RGB = specular map, A = glossiness (roughness maps are detected by name and inverted).
// Assign that texture to the material's Specular Map slot, Source = Specular Alpha, Smoothness = 1.
//
// Pairing is by file name: "Ch10_1001_Specular" pairs with "Ch10_1001_Glossiness" because the names
// match once the keyword is removed. Your original textures and their import settings are not changed.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SpecGlossPacker
{
    const string MenuPath = "Assets/Zombie Tools/Pack Specular + Gloss";

    enum Kind { None, Specular, Gloss, Roughness }

    // Checked in this order, so "glossiness" is never mistaken for a specular map.
    static readonly (string key, Kind kind)[] Keywords =
    {
        ("glossiness", Kind.Gloss), ("gloss", Kind.Gloss),
        ("roughness", Kind.Roughness), ("rough", Kind.Roughness),
        ("specular", Kind.Specular), ("spec", Kind.Specular),
    };

    class Pair
    {
        public Texture2D spec;
        public Texture2D gloss;
        public bool invert;
        public string baseName;
    }

    [MenuItem(MenuPath, false, 2000)]
    static void PackSelected()
    {
        var selected = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);
        var pairs = new Dictionary<string, Pair>();
        var skipped = new List<string>();

        foreach (Object obj in selected)
        {
            var tex = (Texture2D)obj;
            string path = AssetDatabase.GetAssetPath(tex);
            Kind kind = Classify(tex.name, out string baseName);
            if (kind == Kind.None) { skipped.Add(tex.name); continue; }

            string groupKey = Path.GetDirectoryName(path) + "|" + baseName.ToLowerInvariant();
            if (!pairs.TryGetValue(groupKey, out Pair pair))
                pairs[groupKey] = pair = new Pair { baseName = baseName };

            if (kind == Kind.Specular) pair.spec = tex;
            else { pair.gloss = tex; pair.invert = kind == Kind.Roughness; }
        }

        var created = new List<Object>();
        var unmatched = new List<string>();
        int i = 0;

        try
        {
            foreach (Pair pair in pairs.Values)
            {
                i++;
                if (pair.spec == null || pair.gloss == null)
                {
                    unmatched.Add((pair.spec ?? pair.gloss).name);
                    continue;
                }

                EditorUtility.DisplayProgressBar("Packing Specular + Gloss",
                    pair.spec.name, (float)i / pairs.Count);

                Texture2D result = PackPair(pair);
                if (result != null) created.Add(result);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (created.Count > 0) Selection.objects = created.ToArray();

        string msg = $"Created {created.Count} packed texture(s).";
        if (unmatched.Count > 0)
            msg += "\n\nNo partner found for:\n  " + string.Join("\n  ", unmatched);
        if (skipped.Count > 0)
            msg += "\n\nIgnored (name has no spec/gloss/rough keyword):\n  " + string.Join("\n  ", skipped);
        EditorUtility.DisplayDialog("Spec + Gloss Packer", msg, "OK");
    }

    [MenuItem(MenuPath, true)]
    static bool ValidatePackSelected() =>
        Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets).Length >= 2;

    static Kind Classify(string name, out string baseName)
    {
        string lower = name.ToLowerInvariant();
        foreach (var (key, kind) in Keywords)
        {
            int idx = lower.LastIndexOf(key, System.StringComparison.Ordinal);
            if (idx < 0) continue;
            baseName = name.Remove(idx, key.Length);
            return kind;
        }
        baseName = name;
        return Kind.None;
    }

    static Texture2D PackPair(Pair pair)
    {
        string specPath = AssetDatabase.GetAssetPath(pair.spec);
        string glossPath = AssetDatabase.GetAssetPath(pair.gloss);

        Color[] spec = ReadRawPixels(specPath, out int w, out int h);
        Color[] gloss = ReadRawPixels(glossPath, out int gw, out int gh);

        // If the gloss map is a different size, resample it to match the specular map.
        Texture2D glossResampler = null;
        if (gw != w || gh != h)
        {
            glossResampler = new Texture2D(gw, gh, TextureFormat.RGBA32, false, true);
            glossResampler.SetPixels(gloss);
            glossResampler.Apply();
        }

        var output = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                Color g = glossResampler != null
                    ? glossResampler.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h)
                    : gloss[idx];

                float smooth = g.grayscale;
                if (pair.invert) smooth = 1f - smooth;

                Color s = spec[idx];
                output[idx] = new Color(s.r, s.g, s.b, smooth);
            }
        }

        if (glossResampler != null) Object.DestroyImmediate(glossResampler);

        var packed = new Texture2D(w, h, TextureFormat.RGBA32, false);
        packed.SetPixels(output);
        packed.Apply();
        byte[] png = packed.EncodeToPNG();
        Object.DestroyImmediate(packed);

        string dir = Path.GetDirectoryName(specPath);
        string cleanBase = pair.baseName.Trim('_', '-', ' ', '.');
        if (string.IsNullOrEmpty(cleanBase)) cleanBase = pair.spec.name;
        string outPath = Path.Combine(dir, cleanBase + "_SpecGloss.png").Replace('\\', '/');

        File.WriteAllBytes(outPath, png);
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);

        // Import settings for the new texture: colour (sRGB) RGB, alpha used as data, not transparency.
        var specImporter = (TextureImporter)AssetImporter.GetAtPath(specPath);
        var outImporter = (TextureImporter)AssetImporter.GetAtPath(outPath);
        outImporter.textureType = TextureImporterType.Default;
        outImporter.sRGBTexture = true;
        outImporter.alphaSource = TextureImporterAlphaSource.FromInput;
        outImporter.alphaIsTransparency = false;
        outImporter.mipmapEnabled = true;
        outImporter.maxTextureSize = specImporter.maxTextureSize;
        outImporter.SaveAndReimport();

        Debug.Log($"[SpecGlossPacker] {pair.spec.name} + {pair.gloss.name}" +
                  (pair.invert ? " (roughness inverted)" : "") + $" -> {outPath}");

        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }

    // Reads the texture's stored pixel values at full resolution without compression.
    // Temporarily changes the import settings, then restores them exactly as they were.
    static Color[] ReadRawPixels(string path, out int width, out int height)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);

        bool origReadable = importer.isReadable;
        var origCompression = importer.textureCompression;
        int origMaxSize = importer.maxTextureSize;

        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 8192;
        importer.SaveAndReimport();

        try
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            width = tex.width;
            height = tex.height;
            return tex.GetPixels();
        }
        finally
        {
            importer.isReadable = origReadable;
            importer.textureCompression = origCompression;
            importer.maxTextureSize = origMaxSize;
            importer.SaveAndReimport();
        }
    }
}