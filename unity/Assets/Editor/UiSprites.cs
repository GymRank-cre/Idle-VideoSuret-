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

    // Pictogrammes blancs à contour sombre (flèche vers le haut, plus), générés une fois. Teintés par SpriteRenderer.color.
    public static Sprite Arrow()
    {
        return Glyph("Assets/Prototype/Textures/ArrowIcon.png", (x, y) =>
            (x >= 46 && x <= 82 && y >= 8 && y <= 66) ||
            (y >= 60 && y <= 120 && Mathf.Abs(x - 64f) <= (120f - y) * 0.88f));
    }

    // P1 : main qui pointe (index levé, pouce sur le côté). Le bout du doigt est en haut, vers le milieu.
    public static Sprite Hand()
    {
        return Glyph("Assets/Prototype/Textures/HandIcon.png", (x, y) =>
            (x >= 52 && x <= 74 && y >= 58 && y <= 112) || Dist(x, y, 63, 112) <= 11 ||   // index
            (x >= 34 && x <= 98 && y >= 14 && y <= 70) ||                                   // paume
            Seg(x, y, 40, 40, 20, 64, 9));                                                  // pouce
    }

    public static Sprite Plus()
    {
        return Glyph("Assets/Prototype/Textures/PlusIcon.png", (x, y) =>
            (x >= 48 && x <= 80 && y >= 16 && y <= 112) || (x >= 16 && x <= 112 && y >= 48 && y <= 80));
    }

    // Pictogrammes blancs à contour sombre de l'arbre Réputation (clé = champ iconText du noeud).
    public static Sprite Picto(string key)
    {
        string path = "Assets/Prototype/Textures/Picto_" + key + ".png";
        System.Func<int, int, bool> shape;
        switch (key)
        {
            case "coin": shape = (x, y) => { float d = Dist(x, y, 64, 64); return d <= 54 && (d <= 34 || d >= 42); }; break;
            case "fast": shape = (x, y) => Chevron(x, y, 14) || Chevron(x, y, 58); break;
            case "moon": shape = (x, y) => Dist(x, y, 62, 64) <= 50 && Dist(x, y, 86, 72) > 40; break;
            case "clock": shape = (x, y) => { float d = Dist(x, y, 64, 64); return (d <= 54 && d >= 44) || (x >= 60 && x <= 68 && y >= 62 && y <= 98) || (x >= 62 && x <= 92 && y >= 60 && y <= 68); }; break;
            case "crates": shape = (x, y) => (x >= 12 && x <= 116 && y >= 10 && y <= 60) || (x >= 30 && x <= 98 && y >= 66 && y <= 116); break;
            case "runner": shape = (x, y) => Dist(x, y, 82, 104) <= 15 || Seg(x, y, 80, 88, 60, 56, 9) || Seg(x, y, 60, 56, 40, 20, 9) || Seg(x, y, 60, 56, 86, 24, 9) || Seg(x, y, 74, 80, 44, 76, 8) || Seg(x, y, 74, 80, 102, 66, 8); break;
            case "smile": shape = (x, y) => { float d = Dist(x, y, 64, 64); if (d > 54) return false; if (Dist(x, y, 44, 78) <= 9 || Dist(x, y, 84, 78) <= 9) return false; float m = Dist(x, y, 64, 62); return !(m >= 26 && m <= 36 && y <= 56); }; break;
            case "percent": shape = (x, y) => Dist(x, y, 38, 92) <= 18 || Dist(x, y, 90, 36) <= 18 || Seg(x, y, 102, 104, 26, 24, 8); break;
            default: return Plus();
        }
        return Glyph(path, shape);
    }

    static float Dist(float x, float y, float cx, float cy) { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); }

    static bool Chevron(int x, int y, int x0)
    {
        float u = x - x0;
        return u >= 0 && u <= 50 && Mathf.Abs(Mathf.Abs(y - 64f) - (50f - u)) <= 9f;
    }

    static bool Seg(int x, int y, float ax, float ay, float bx, float by, float half)
    {
        return SegDist(x, y, ax, ay, bx, by) <= half;
    }

    // Étoile à cinq branches (pastille Points Pro).
    public static Sprite Star()
    {
        return Glyph("Assets/Prototype/Textures/StarIcon.png", (x, y) =>
        {
            float dx = x - 64f, dy = y - 60f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90f;
            float a = Mathf.Repeat(deg, 72f);
            float r = 56f + (24f - 56f) * (Mathf.Min(a, 72f - a) / 36f);
            return dist <= r;
        });
    }

    // Visage de la bulle d'humeur : 0 content, 1 neutre, 2 en colère (bulle ronde, traits sombres, fond teinté).
    public static Sprite Mood(int kind)
    {
        string path = "Assets/Prototype/Textures/Mood" + kind + ".png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Prototype/Textures"))
            AssetDatabase.CreateFolder("Assets/Prototype", "Textures");

        const int n = 128;
        Color ink = new Color32(0x0E, 0x16, 0x20, 0xFF);
        Color fill = kind == 0 ? new Color32(0xFF, 0xFF, 0xFF, 0xFF) : kind == 1 ? new Color32(0xFF, 0xEC, 0xB0, 0xFF) : new Color32(0xFF, 0xA8, 0x9E, 0xFF);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - 63.5f, dy = y - 63.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = new Color(0f, 0f, 0f, 0f);
                if (d <= 62f) c = ink;
                if (d <= 55f) c = fill;
                bool feature = false;
                // yeux
                if ((x - 44) * (x - 44) + (y - 78) * (y - 78) <= 64f || (x - 84) * (x - 84) + (y - 78) * (y - 78) <= 64f) feature = true;
                if (kind == 0) // sourire : arc du bas
                {
                    float md = Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 66f) * (y - 66f));
                    if (md >= 22f && md <= 30f && y <= 58f) feature = true;
                }
                else if (kind == 1) // bouche droite
                {
                    if (x >= 46 && x <= 82 && y >= 40 && y <= 47) feature = true;
                }
                else // moue + sourcils en colère
                {
                    float md = Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 22f) * (y - 22f));
                    if (md >= 20f && md <= 28f && y >= 42f) feature = true;
                    if (SegDist(x, y, 30f, 100f, 56f, 90f) <= 4f || SegDist(x, y, 98f, 100f, 72f, 90f) <= 4f) feature = true;
                }
                if (feature && d <= 55f) c = ink;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
        float cx = ax + vx * t - px, cy = ay + vy * t - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }

    static Sprite Glyph(string path, System.Func<int, int, bool> inside)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Prototype/Textures"))
            AssetDatabase.CreateFolder("Assets/Prototype", "Textures");

        const int n = 128, r = 6;
        var mask = new bool[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) mask[y * n + x] = inside(x, y);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color ink = new Color32(0x0E, 0x16, 0x20, 0xFF);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool near = false;
                for (int dy = -r; dy <= r && !near; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= n || yy >= n || dx * dx + dy * dy > r * r) continue;
                        if (mask[yy * n + xx]) { near = true; break; }
                    }
                tex.SetPixel(x, y, mask[y * n + x] ? Color.white : near ? ink : new Color(0f, 0f, 0f, 0f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Couverture (0 à 1) d'un rectangle arrondi inset de `inset` pixels avec le rayon `radius`.
    static float RoundedBox(float x, float y, float w, float h, float inset, float radius)
    {
        float hx = w * 0.5f - inset - radius, hy = h * 0.5f - inset - radius;
        float dx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - hx, 0f), dy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - hy, 0f);
        return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    }
}
