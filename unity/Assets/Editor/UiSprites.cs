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
}
