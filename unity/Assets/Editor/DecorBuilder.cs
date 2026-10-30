using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 10b : « Meubler l'atelier » (menu Tools > Idle Prototype > Meubler l'atelier, aussi appelé par Build scene).
// Pose par code, en boîtes arrondies (RoundedBox), une bibliothèque d'accessoires et une disposition fixe : sol à deux tons,
// marquages, murs habillés, accessoires dans chaque zone libre, extérieur vivant. Vérifie que chaque case de 3 x 3 m est meublée.
public static class DecorBuilder
{
    const string TexFolder = "Assets/Prototype/Textures";

    [MenuItem("Tools/Idle Prototype/Meubler l'atelier")]
    public static void Run() { Build(); }

    static Transform root;
    static Material cream, lightBlue, yellow, blueStrip, white, steel, darkSteel, red, blue, orange, green, wood, woodDark, crateBrown, crateTape, black;

    public static void Build()
    {
        GameObject old = GameObject.Find("Decor");
        if (old != null) Object.DestroyImmediate(old);
        float before = Coverage(null);
        root = new GameObject("Decor").transform;
        MakeMaterials();

        TwoToneFloor();
        FloorMarkings();
        WallDressing();
        PlaceProps();
        LivingDecor();
        ExteriorLife();

        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            bool moves = t.GetComponent<Spinner>() != null || t.GetComponent<Sweeper>() != null || t.GetComponent<CarLoop>() != null;
            if (!moves && t.GetComponentInParent<Spinner>() == null && t.GetComponentInParent<Sweeper>() == null && t.GetComponentInParent<CarLoop>() == null)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        var report = new StringBuilder();
        float after = Coverage(report);
        Debug.Log("Décor : cases de 3 x 3 m meublées " + Mathf.RoundToInt(before * 100f) + " % avant, " + Mathf.RoundToInt(after * 100f) + " % après.\n" + report);
    }

    // ------------------------------------------------------------------ matériaux

    static Material M(string name, Color c) { return PrototypeSetup.MakeMaterial(name, c); }

    static void MakeMaterials()
    {
        cream = M("Dc_Cream", new Color32(0xF2, 0xE8, 0xD5, 0xFF));
        lightBlue = M("Dc_LightBlue", new Color32(0xCF, 0xE3, 0xF5, 0xFF));
        yellow = M("Dc_Yellow", new Color32(0xFF, 0xC4, 0x00, 0xFF));
        blueStrip = M("Dc_BlueStrip", new Color32(0xB7, 0xD6, 0xF5, 0xFF));
        white = M("Dc_White", new Color32(0xF7, 0xF5, 0xEE, 0xFF));
        steel = M("Dc_Steel", new Color32(0x8D, 0x99, 0xA8, 0xFF));
        darkSteel = M("Dc_DarkSteel", new Color32(0x3A, 0x45, 0x55, 0xFF));
        red = M("Dc_Red", new Color32(0xE0, 0x4A, 0x40, 0xFF));
        blue = M("Dc_Blue", new Color32(0x2A, 0x8C, 0xE0, 0xFF));
        orange = M("Dc_Orange", new Color32(0xFF, 0x7A, 0x1A, 0xFF));
        green = M("Dc_Green", new Color32(0x3D, 0xDC, 0x5A, 0xFF));
        wood = M("Dc_Wood", new Color32(0xB9, 0x82, 0x4A, 0xFF));
        woodDark = M("Dc_WoodDark", new Color32(0x7A, 0x52, 0x30, 0xFF));
        crateBrown = M("Dc_Crate", new Color32(0xC9, 0x93, 0x5A, 0xFF));
        crateTape = M("Dc_CrateTape", new Color32(0xE8, 0xCF, 0xA0, 0xFF));
        black = M("Dc_Black", new Color32(0x1B, 0x22, 0x2C, 0xFF));
    }

    // ------------------------------------------------------------------ primitives

    static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float radius = 0.05f)
    {
        float r = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f);
        return RoundedBox.Make(parent, name, pos, size, r, mat);
    }

    static GameObject Slab(string name, Vector3 center, Vector3 size, Material mat, Transform parent = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static Transform Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Transform Group(string name, Vector3 pos, float yaw = 0f)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        g.position = pos;
        g.rotation = Quaternion.Euler(0f, yaw, 0f);
        return g;
    }

    // ------------------------------------------------------------------ sol à deux tons

    static void TwoToneFloor()
    {
        string path = TexFolder + "/FloorTwoTone.png";
        if (!File.Exists(path))
        {
            if (!AssetDatabase.IsValidFolder(TexFolder)) AssetDatabase.CreateFolder("Assets/Prototype", "Textures");
            const int n = 256, cell = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
            Color a = new Color32(0xD5, 0xDC, 0xE4, 0xFF), b = new Color32(0xC3, 0xCC, 0xD7, 0xFF), grout = new Color32(0xAE, 0xB8, 0xC4, 0xFF);
            var rnd = new System.Random(7);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Color c = ((x / cell) + (y / cell)) % 2 == 0 ? a : b;
                    if (x % cell < 2 || y % cell < 2) c = grout;
                    float noise = (float)(rnd.NextDouble() - 0.5) * 0.03f; // léger bruit
                    tex.SetPixel(x, y, new Color(c.r + noise, c.g + noise, c.b + noise, 1f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        var tex2 = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        GameObject ground = GameObject.Find("Ground");
        if (ground == null || tex2 == null) return;
        Material floor = ground.GetComponent<Renderer>().sharedMaterial;
        floor.mainTexture = tex2;
        floor.mainTextureScale = new Vector2(5.625f, 6.375f); // la texture couvre 4 m : cases de 1 m
        floor.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(floor);
    }

    // ------------------------------------------------------------------ marquages au sol

    static void FloorMarkings()
    {
        // lignes existantes au-dessus du couloir bleu
        GameObject workshop = GameObject.Find("Workshop");
        Transform lines = workshop != null ? workshop.transform.Find("FloorLines") : null;
        if (lines != null)
            foreach (Transform c in lines)
                if (c.position.y < 0.015f) c.position = new Vector3(c.position.x, 0.02f, c.position.z);

        const float y = 0.006f;
        // couloir central du joueur : bande bleu clair (dégagée, mais marquée)
        Slab("CorridorStrip", new Vector3(0.5f, y, 0f), new Vector3(15.4f, 0.012f, 2.6f), blueStrip);
        // zones jaunes (cadres) autour des postes, de la palette et du comptoir
        Frame(new Vector3(-4.5f, y, 0f), 3.8f, 3.8f);
        Frame(new Vector3(4.5f, y, 0f), 3.8f, 3.8f);
        Frame(new Vector3(-4.5f, y, 7.5f), 3.8f, 3.8f);
        Frame(new Vector3(5f, y, -5.4f), 3.8f, 1.8f);
        // flèches de circulation blanches le long du couloir
        foreach (float x in new[] { -6.2f, -1.2f, 7.4f }) Chevron(new Vector3(x, y + 0.002f, 0.55f), 90f);
        // passage piéton devant le comptoir
        for (int i = 0; i < 7; i++) Slab("Crosswalk" + i, new Vector3(3.2f + i * 0.55f, y + 0.002f, -3.2f), new Vector3(0.32f, 0.012f, 1.5f), white);
        // logo au sol : anneau bleu, disque blanc et écusson (identité de l'entreprise) au centre de l'atelier
        var logo = Group("FloorLogo", new Vector3(-0.3f, 0.008f, 3.3f));
        Cyl(logo, "Ring", Vector3.zero, new Vector3(2.6f, 0.004f, 2.6f), blue);
        Cyl(logo, "Disc", new Vector3(0f, 0.004f, 0f), new Vector3(2.2f, 0.004f, 2.2f), white);
        Box(logo, "Shield", new Vector3(0f, 0.012f, 0f), new Vector3(0.9f, 0.012f, 1.1f), orange, 0.15f);
        Box(logo, "Lens", new Vector3(0f, 0.02f, 0.05f), new Vector3(0.4f, 0.012f, 0.4f), darkSteel, 0.18f);

        // paillassons orange devant les dalles
        foreach (Vector3 p in new[] { new Vector3(-4.5f, y, 2.15f), new Vector3(3.2f, y, 2.3f), new Vector3(1f, y, -6.2f) })
            Box(root, "Mat", p + Vector3.up * 0.015f, new Vector3(2.6f, 0.03f, 0.8f), orange, 0.1f);
    }

    static void Frame(Vector3 c, float w, float d)
    {
        const float t = 0.12f;
        Slab("FrameN", c + new Vector3(0f, 0f, d * 0.5f), new Vector3(w, 0.012f, t), yellow);
        Slab("FrameS", c + new Vector3(0f, 0f, -d * 0.5f), new Vector3(w, 0.012f, t), yellow);
        Slab("FrameE", c + new Vector3(w * 0.5f, 0f, 0f), new Vector3(t, 0.012f, d), yellow);
        Slab("FrameW", c + new Vector3(-w * 0.5f, 0f, 0f), new Vector3(t, 0.012f, d), yellow);
    }

    static void Chevron(Vector3 c, float yaw)
    {
        var g = Group("Chevron", c, yaw);
        Slab("A", Vector3.zero, new Vector3(0.18f, 0.012f, 0.9f), white, g).transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
        Slab("B", Vector3.zero, new Vector3(0.18f, 0.012f, 0.9f), white, g).transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
        g.GetChild(0).localPosition = new Vector3(-0.28f, 0f, 0f);
        g.GetChild(1).localPosition = new Vector3(0.28f, 0f, 0f);
    }

    // ------------------------------------------------------------------ murs

    static void WallDressing()
    {
        // lambris alternés crème / bleu clair, mur du fond et mur de gauche
        for (int i = 0; i < 9; i++)
            Box(root, "PanelBack", new Vector3(-7.3f + i * 2.5f, 0.62f, 11.32f), new Vector3(2.46f, 1.0f, 0.06f), i % 2 == 0 ? cream : lightBlue, 0.02f);
        for (int i = 0; i < 10; i++)
            Box(root, "PanelLeft", new Vector3(-8.5f, 0.62f, -12.3f + i * 2.5f), new Vector3(0.06f, 1.0f, 2.46f), i % 2 == 0 ? lightBlue : cream, 0.02f);

        // tuyaux le long du plafond (fond et gauche) avec colliers jaunes
        Cyl(root, "PipeBack", new Vector3(2.8f, 3.25f, 11.2f), new Vector3(0.22f, 11.3f, 0.22f), steel, new Vector3(0f, 0f, 90f));
        Cyl(root, "PipeLeft", new Vector3(-8.4f, 3.0f, -1.3f), new Vector3(0.22f, 12.5f, 0.22f), steel, new Vector3(90f, 0f, 0f));
        for (int i = 0; i < 6; i++)
        {
            Cyl(root, "ClampB", new Vector3(-4.5f + i * 3.6f, 3.25f, 11.2f), new Vector3(0.3f, 0.08f, 0.3f), yellow, new Vector3(0f, 0f, 90f));
            Cyl(root, "ClampL", new Vector3(-8.4f, 3.0f, -10f + i * 4.2f), new Vector3(0.3f, 0.08f, 0.3f), yellow, new Vector3(90f, 0f, 0f));
        }

        // affiches colorées (sans texte)
        Poster(new Vector3(-3.4f, 2.0f, 11.3f), 0f, orange);
        Poster(new Vector3(2.0f, 2.0f, 11.3f), 0f, blue);
        Poster(new Vector3(4.2f, 2.0f, 11.3f), 0f, green);
        Poster(new Vector3(-8.45f, 2.0f, -3.5f), 90f, red);
        Poster(new Vector3(-8.45f, 2.0f, 2.8f), 90f, blue);
        Poster(new Vector3(-8.45f, 2.0f, 9.5f), 90f, orange);

        // deux grandes fenêtres sur le mur de gauche
        foreach (float z in new[] { -6.2f, 6.2f }) Window(new Vector3(-8.46f, 1.9f, z));
    }

    static void Poster(Vector3 p, float yaw, Material m)
    {
        var g = Group("Poster", p, yaw);
        Box(g, "Frame", Vector3.zero, new Vector3(1.2f, 1.7f, 0.06f), white, 0.02f);
        Box(g, "Paper", new Vector3(0f, 0f, -0.02f), new Vector3(1.0f, 1.5f, 0.05f), m, 0.02f);
        Box(g, "Band", new Vector3(0f, -0.45f, -0.05f), new Vector3(0.8f, 0.2f, 0.04f), cream, 0.02f);
        Box(g, "Icon", new Vector3(0f, 0.25f, -0.05f), new Vector3(0.5f, 0.5f, 0.04f), cream, 0.1f);
    }

    static void Window(Vector3 p)
    {
        var g = Group("Window", p, 90f);
        Box(g, "Frame", Vector3.zero, new Vector3(2.6f, 1.7f, 0.1f), white, 0.03f);
        Box(g, "Glass", new Vector3(0f, 0f, -0.03f), new Vector3(2.35f, 1.45f, 0.06f), M("Dc_Glass", new Color32(0xA9, 0xCB, 0xE8, 0xFF)), 0.03f);
        Box(g, "BarV", new Vector3(0f, 0f, -0.06f), new Vector3(0.08f, 1.45f, 0.05f), white, 0.01f);
        Box(g, "BarH", new Vector3(0f, 0f, -0.06f), new Vector3(2.35f, 0.08f, 0.05f), white, 0.01f);
    }

    // ------------------------------------------------------------------ accessoires

    static void PlaceProps()
    {
        // mur du fond (z ~ 10,6) : établis, armoires, étagères
        Bench(new Vector3(-6.4f, 0f, 10.4f), 180f);
        Shelf(new Vector3(-3.6f, 0f, 10.7f), 180f);
        Cabinet(new Vector3(5.4f, 0f, 10.7f), 180f);
        Bench(new Vector3(7.6f, 0f, 10.4f), 180f);
        Barrels(new Vector3(11f, 0f, 10.2f), 3);
        Barrels(new Vector3(12.4f, 0f, 9f), 2);
        Crates(new Vector3(9.8f, 0f, 10.4f), 3);
        ExitSign(new Vector3(0.8f, 2.9f, 11.3f), 0f);

        // côté gauche : étagères existantes + bacs de tri et échelle
        Bins(new Vector3(-7.4f, 0f, -2.2f), 90f);
        Ladder(new Vector3(-8.2f, 0f, 11.2f - 3.2f), 90f);
        Crates(new Vector3(-7.2f, 0f, -4.5f), 4);

        // sud-ouest : atelier de pose (grande zone libre)
        Bench(new Vector3(-6.8f, 0f, -8.4f), 90f);
        Shelf(new Vector3(-7.9f, 0f, -11.6f), 90f);
        Bins(new Vector3(-4.2f, 0f, -9.6f), 0f);
        Barrels(new Vector3(-1.8f, 0f, -9.2f), 3);
        Cart(new Vector3(-3.4f, 0f, -12.4f), 20f);
        Cabinet(new Vector3(-6.2f, 0f, -13.1f), 0f);
        Crates(new Vector3(-0.2f, 0f, -13.1f), 5);

        // sud-centre et sud-est
        Pallets(new Vector3(2.2f, 0f, -10.6f));
        Cart(new Vector3(0.6f, 0f, -8f), -30f);
        Shelf(new Vector3(3.2f, 0f, -13.5f), 0f);
        Crates(new Vector3(6.6f, 0f, -13.1f), 3);
        Shelf(new Vector3(9.4f, 0f, -13.5f), 0f);
        Barrels(new Vector3(12.6f, 0f, -13f), 2);

        // côté est (le long du mur de droite)
        Shelf(new Vector3(13.4f, 0f, 1.6f), -90f);
        Bins(new Vector3(13.2f, 0f, -5.2f), -90f);
        Crates(new Vector3(12.4f, 0f, 6.4f), 3);
        Cart(new Vector3(11.2f, 0f, 2.2f), 70f);
        Cabinet(new Vector3(13.5f, 0f, -3.4f), -90f);
    }

    static void Shelf(Vector3 p, float yaw)
    {
        var g = Group("Shelf", p, yaw);
        Box(g, "PostA", new Vector3(-0.95f, 1.0f, 0f), new Vector3(0.08f, 2.0f, 0.5f), darkSteel, 0.02f);
        Box(g, "PostB", new Vector3(0.95f, 1.0f, 0f), new Vector3(0.08f, 2.0f, 0.5f), darkSteel, 0.02f);
        for (int level = 0; level < 3; level++)
        {
            float y = 0.45f + level * 0.7f;
            Box(g, "Board", new Vector3(0f, y, 0f), new Vector3(2.0f, 0.06f, 0.5f), steel, 0.02f);
            for (int k = 0; k < 3; k++)
            {
                Box(g, "Crate", new Vector3(-0.6f + k * 0.62f, y + 0.22f, 0f), new Vector3(0.5f, 0.36f, 0.4f), k % 2 == 0 ? crateBrown : white, 0.04f);
                Box(g, "Tape", new Vector3(-0.6f + k * 0.62f, y + 0.22f, 0f), new Vector3(0.1f, 0.37f, 0.41f), crateTape, 0.01f);
            }
        }
    }

    static void Bench(Vector3 p, float yaw)
    {
        var g = Group("Bench", p, yaw);
        Box(g, "Top", new Vector3(0f, 0.85f, 0f), new Vector3(2.2f, 0.1f, 0.85f), wood, 0.03f);
        foreach (float x in new[] { -0.95f, 0.95f }) Box(g, "Leg", new Vector3(x, 0.4f, 0f), new Vector3(0.1f, 0.8f, 0.7f), darkSteel, 0.02f);
        Box(g, "Vice", new Vector3(0.7f, 1.0f, 0f), new Vector3(0.25f, 0.2f, 0.25f), red, 0.04f);
        Box(g, "Tool1", new Vector3(-0.5f, 0.93f, 0.1f), new Vector3(0.5f, 0.05f, 0.1f), steel, 0.02f);
        Box(g, "Tool2", new Vector3(-0.1f, 0.93f, -0.15f), new Vector3(0.3f, 0.05f, 0.08f), orange, 0.02f);
        Box(g, "Board", new Vector3(0f, 1.6f, 0.38f), new Vector3(1.8f, 0.7f, 0.04f), woodDark, 0.02f); // panneau à outils au mur
        for (int i = 0; i < 4; i++) Box(g, "Hung", new Vector3(-0.6f + i * 0.4f, 1.6f, 0.34f), new Vector3(0.1f, 0.5f, 0.04f), i % 2 == 0 ? steel : orange, 0.02f);
    }

    static void Cabinet(Vector3 p, float yaw)
    {
        var g = Group("Cabinet", p, yaw);
        Box(g, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(1.1f, 2.0f, 0.55f), blue, 0.06f);
        Box(g, "Door", new Vector3(0f, 1.0f, -0.28f), new Vector3(1.0f, 1.9f, 0.04f), M("Dc_BlueDoor", new Color32(0x3A, 0x9C, 0xEC, 0xFF)), 0.03f);
        Box(g, "Handle", new Vector3(0.38f, 1.0f, -0.32f), new Vector3(0.06f, 0.4f, 0.06f), white, 0.02f);
        for (int i = 0; i < 4; i++) Box(g, "Led", new Vector3(-0.3f + i * 0.2f, 1.8f, -0.31f), new Vector3(0.08f, 0.08f, 0.04f), i % 2 == 0 ? green : red, 0.02f);
    }

    static void Barrels(Vector3 p, int count)
    {
        var g = Group("Barrels", p);
        for (int i = 0; i < count; i++)
        {
            Vector3 o = new Vector3((i % 2) * 0.62f - 0.3f, 0f, (i / 2) * 0.62f);
            Cyl(g, "Barrel", o + new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.45f, 0.55f), i % 2 == 0 ? red : blue);
            Cyl(g, "Lid", o + new Vector3(0f, 0.91f, 0f), new Vector3(0.5f, 0.03f, 0.5f), darkSteel);
            Cyl(g, "Band", o + new Vector3(0f, 0.45f, 0f), new Vector3(0.57f, 0.03f, 0.57f), white);
        }
    }

    static void Crates(Vector3 p, int count)
    {
        var g = Group("Crates", p);
        for (int i = 0; i < count; i++)
        {
            Vector3 o = i < 3 ? new Vector3(i * 0.7f - 0.7f, 0f, 0f) : new Vector3((i - 3) * 0.7f - 0.35f, 0.62f, 0.02f);
            Box(g, "Crate", o + new Vector3(0f, 0.3f, 0f), new Vector3(0.66f, 0.6f, 0.6f), crateBrown, 0.06f);
            Box(g, "Tape", o + new Vector3(0f, 0.3f, 0f), new Vector3(0.12f, 0.61f, 0.61f), crateTape, 0.01f);
        }
    }

    static void Bins(Vector3 p, float yaw)
    {
        var g = Group("Bins", p, yaw);
        Material[] cols = { red, blue, green };
        for (int i = 0; i < 3; i++)
        {
            Box(g, "Bin", new Vector3(-0.8f + i * 0.8f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.9f), cols[i], 0.08f);
            Box(g, "Hole", new Vector3(-0.8f + i * 0.8f, 0.61f, 0f), new Vector3(0.5f, 0.04f, 0.7f), black, 0.03f);
        }
    }

    static void Ladder(Vector3 p, float yaw)
    {
        var g = Group("Ladder", p, yaw);
        g.rotation = Quaternion.Euler(-12f, yaw, 0f);
        foreach (float x in new[] { -0.3f, 0.3f }) Box(g, "Rail", new Vector3(x, 1.2f, 0f), new Vector3(0.06f, 2.4f, 0.06f), yellow, 0.02f);
        for (int i = 0; i < 7; i++) Box(g, "Rung", new Vector3(0f, 0.3f + i * 0.33f, 0f), new Vector3(0.6f, 0.05f, 0.05f), steel, 0.01f);
    }

    static void Cart(Vector3 p, float yaw)
    {
        var g = Group("Cart", p, yaw);
        Box(g, "Platform", new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.08f, 0.7f), blue, 0.03f);
        Box(g, "Handle", new Vector3(-0.58f, 0.85f, 0f), new Vector3(0.06f, 0.9f, 0.6f), darkSteel, 0.02f);
        foreach (Vector3 w in new[] { new Vector3(-0.5f, 0.12f, 0.28f), new Vector3(-0.5f, 0.12f, -0.28f), new Vector3(0.5f, 0.12f, 0.28f), new Vector3(0.5f, 0.12f, -0.28f) })
            Cyl(g, "Wheel", w, new Vector3(0.2f, 0.04f, 0.2f), black, new Vector3(90f, 0f, 0f));
        Box(g, "Load", new Vector3(0.1f, 0.65f, 0f), new Vector3(0.6f, 0.5f, 0.5f), crateBrown, 0.05f);
        Box(g, "LoadTape", new Vector3(0.1f, 0.65f, 0f), new Vector3(0.1f, 0.51f, 0.51f), crateTape, 0.01f);
    }

    static void Pallets(Vector3 p)
    {
        var g = Group("Pallets", p);
        for (int i = 0; i < 4; i++)
        {
            Box(g, "Pallet", new Vector3(0f, 0.08f + i * 0.17f, 0f), new Vector3(1.3f, 0.12f, 1.0f), i % 2 == 0 ? wood : woodDark, 0.02f);
            Box(g, "Slat", new Vector3(0f, 0.01f + i * 0.17f, 0f), new Vector3(1.3f, 0.04f, 0.14f), woodDark, 0.01f);
        }
    }

    static void ExitSign(Vector3 p, float yaw)
    {
        var g = Group("ExitSign", p, yaw);
        Box(g, "Sign", Vector3.zero, new Vector3(0.9f, 0.34f, 0.06f), green, 0.04f);
        Box(g, "Arrow", new Vector3(0.25f, 0f, -0.04f), new Vector3(0.22f, 0.12f, 0.04f), white, 0.02f);
        Box(g, "Man", new Vector3(-0.15f, 0f, -0.04f), new Vector3(0.12f, 0.2f, 0.04f), white, 0.04f);
    }

    // ------------------------------------------------------------------ vie : ventilateur, caméra, horloge

    static void LivingDecor()
    {
        // ventilateur de plafond (les pales tournent)
        var fan = Group("CeilingFan", new Vector3(1.5f, 3.9f, 10.0f));
        fan.localScale = Vector3.one * 0.6f; // haut et petit : ne masque plus le HUD
        Cyl(fan, "Rod", new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.3f, 0.06f), darkSteel);
        Cyl(fan, "Hub", Vector3.zero, new Vector3(0.35f, 0.08f, 0.35f), steel);
        var blades = new GameObject("Blades").transform;
        blades.SetParent(fan, false);
        for (int i = 0; i < 4; i++)
        {
            var b = Box(blades, "Blade", Vector3.zero, new Vector3(1.4f, 0.04f, 0.22f), white, 0.02f);
            b.localPosition = Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0.75f, 0f, 0f);
            b.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
        }
        var sp = blades.gameObject.AddComponent<Spinner>();
        var so = new SerializedObject(sp);
        so.FindProperty("degreesPerSecond").floatValue = 240f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // caméra de surveillance qui pivote (±30°)
        var cam = Group("SecurityCam", new Vector3(8.4f, 3.1f, 11.1f));
        Box(cam, "Mount", new Vector3(0f, 0.15f, 0.12f), new Vector3(0.14f, 0.3f, 0.2f), darkSteel, 0.02f);
        var head = new GameObject("Head").transform;
        head.SetParent(cam, false);
        Box(head, "Body", new Vector3(0f, 0f, -0.15f), new Vector3(0.22f, 0.2f, 0.5f), white, 0.05f);
        Box(head, "Lens", new Vector3(0f, 0f, -0.42f), new Vector3(0.14f, 0.14f, 0.06f), black, 0.03f);
        Box(head, "Led", new Vector3(0.08f, 0.09f, -0.3f), new Vector3(0.05f, 0.04f, 0.05f), red, 0.02f);
        head.gameObject.AddComponent<Sweeper>();

        // horloge murale
        var clock = Group("Clock", new Vector3(-6.2f, 2.9f, 11.3f));
        Cyl(clock, "Face", Vector3.zero, new Vector3(0.7f, 0.03f, 0.7f), white, new Vector3(90f, 0f, 0f));
        Box(clock, "HandA", new Vector3(0f, 0.12f, 0.03f), new Vector3(0.04f, 0.25f, 0.02f), black, 0.01f);
        Box(clock, "HandB", new Vector3(0.1f, 0f, 0.03f), new Vector3(0.2f, 0.04f, 0.02f), black, 0.01f);
    }

    // ------------------------------------------------------------------ extérieur vivant

    static void ExteriorLife()
    {
        // route derrière l'atelier + voiture qui passe toutes les 25 s ; camionnette garée sur le parking
        Slab("Road", new Vector3(17f, -0.02f, -18.5f), new Vector3(75f, 0.08f, 4.2f), M("Dc_Road", new Color32(0x4A, 0x52, 0x60, 0xFF)));
        for (int i = 0; i < 12; i++) Slab("RoadLine", new Vector3(-9f + i * 5f, 0.03f, -18.5f), new Vector3(2f, 0.02f, 0.14f), white);

        var car = Group("PassingCar", new Vector3(45f, 0f, -18.5f), 90f);
        Box(car, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(2.0f, 0.7f, 1.0f), red, 0.2f);
        Box(car, "Cabin", new Vector3(-0.1f, 1.1f, 0f), new Vector3(1.0f, 0.5f, 0.9f), M("Dc_CarGlass", new Color32(0xA9, 0xCB, 0xE8, 0xFF)), 0.15f);
        foreach (Vector3 w in new[] { new Vector3(-0.65f, 0.28f, 0.5f), new Vector3(-0.65f, 0.28f, -0.5f), new Vector3(0.65f, 0.28f, 0.5f), new Vector3(0.65f, 0.28f, -0.5f) })
            Cyl(car, "Wheel", w, new Vector3(0.5f, 0.1f, 0.5f), black, new Vector3(90f, 0f, 0f));
        car.gameObject.AddComponent<CarLoop>();

        var van = Group("ParkedVan", new Vector3(21.8f, 0f, -14.8f), 0f);
        OrderBuilder.VanBody(van);
    }

    // ------------------------------------------------------------------ mesure : cases de 3 x 3 m meublées

    // Part des cases de 3 x 3 m (zone jouable élargie) qui contiennent un accessoire, une machine, une dalle ou un marquage.
    // La bande du couloir central compte (« dégagée mais marquée »).
    static float Coverage(StringBuilder report)
    {
        const float x0 = -7.5f, z0 = -13.5f, cell = 3f;
        int cols = 7, rows = 8, furnished = 0;
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var b = new Bounds(new Vector3(x0 + (c + 0.5f) * cell, 1f, z0 + (r + 0.5f) * cell), new Vector3(cell, 8f, cell));
                bool hit = false;
                foreach (Renderer rd in renderers)
                {
                    if (!Countable(rd)) continue;
                    if (rd.bounds.Intersects(b)) { hit = true; break; }
                }
                if (hit) furnished++;
                else if (report != null) report.Append("case vide : x " + (x0 + c * cell).ToString("0.0") + " à " + (x0 + (c + 1) * cell).ToString("0.0") + ", z " + (z0 + r * cell).ToString("0.0") + " à " + (z0 + (r + 1) * cell).ToString("0.0") + "\n");
            }
        }
        if (report != null) report.Append(furnished + " / " + (cols * rows) + " cases meublées");
        return furnished / (float)(cols * rows);
    }

    static bool Countable(Renderer rd)
    {
        string n = rd.name;
        Transform t = rd.transform;
        if (n == "Ground" || n.StartsWith("Wall") || n.StartsWith("Stripe") || n.StartsWith("Baseboard") || n.StartsWith("TopTrim")
            || n.StartsWith("Cornice") || n.StartsWith("Cap") || n.StartsWith("Shadow") || n.StartsWith("Halo") || n.Contains("Shadow")) return false;
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.name == "Exterior" || p.name == "PlayBounds") return false;
            if (p.name == "Canvas") return false;
        }
        if (rd is SpriteRenderer || rd is MeshRenderer == false) return false;
        // à plat : le sol lui-même est exclu, mais pas les marquages (épaisseur >= 0,01 m)
        return rd.bounds.max.y > 0.005f;
    }
}
