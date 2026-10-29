using System.IO;
using UnityEditor;
using UnityEngine;

// Sprite de rectangle à coins bien arrondis (généré par code, découpé en 9 zones) pour les pastilles, panneaux et boutons.
public static class UiSprites
{
    const string Path = "Assets/Prototype/Textures/RoundedRect.png";
    const int Size = 96;
    const int Radius = 40;

    public static Sprite Rounded()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prototype/Textures"))
            AssetDatabase.CreateFolder("Assets/Prototype", "Textures");

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // distance au coin le plus proche du rectangle intérieur
                float cx = Mathf.Clamp(x + 0.5f, Radius, Size - Radius);
                float cy = Mathf.Clamp(y + 0.5f, Radius, Size - Radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(Radius - d + 0.5f); // bord doux d'un pixel
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(Radius, Radius, Radius, Radius);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(Path);
    }

    const string BillPath = "Assets/Prototype/Textures/BillIcon.png";

    // Icône de billet (K1) : rectangle vert arrondi à contour sombre, liseré clair et sceau central. Un seul sprite pour tout le jeu.
    public static Sprite Bill()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(BillPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Prototype/Textures"))
            AssetDatabase.CreateFolder("Assets/Prototype", "Textures");

        const int w = 160, h = 100;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color ink = new Color32(0x0E, 0x16, 0x20, 0xFF), green = new Color32(0x3D, 0xDC, 0x5A, 0xFF);
        Color light = new Color32(0x8C, 0xFF, 0x9E, 0xFF), dark = new Color32(0x1F, 0x8F, 0x3A, 0xFF);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float outer = RoundedBox(px, py, w, h, 8f, 22f);          // contour
                float mid = RoundedBox(px, py, w, h, 16f, 14f);           // vert
                float rim = RoundedBox(px, py, w, h, 26f, 8f);            // liseré clair
                float seal = Mathf.Clamp01(26f - Vector2.Distance(new Vector2(px, py), new Vector2(w * 0.5f, h * 0.5f)) + 0.5f);
                float sealIn = Mathf.Clamp01(15f - Vector2.Distance(new Vector2(px, py), new Vector2(w * 0.5f, h * 0.5f)) + 0.5f);
                Color c = ink;
                c = Color.Lerp(c, green, mid);
                c = Color.Lerp(c, light, Mathf.Clamp01(mid - rim));
                c = Color.Lerp(c, dark, seal);
                c = Color.Lerp(c, light, sealIn);
                c.a = outer;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(BillPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(BillPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(BillPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(BillPath);
    }

    // Couverture (0 à 1) d'un rectangle arrondi inset de `inset` pixels avec le rayon `radius`.
    static float RoundedBox(float x, float y, float w, float h, float inset, float radius)
    {
        float hx = w * 0.5f - inset - radius, hy = h * 0.5f - inset - radius;
        float dx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - hx, 0f), dy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - hy, 0f);
        return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    }
}
