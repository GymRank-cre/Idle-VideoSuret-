using UnityEditor;
using UnityEngine;

// 10b-1 : fin du vide noir autour de l'atelier (sol continu extérieur, ciel clair), parking visible par la porte roulante,
// ambiance tri-couleur, quelques accessoires d'atelier. Tout en formes simples, statique (batching), sans ombre temps réel.
public static class ExteriorBuilder
{
    static readonly Color Sky = new Color32(0xBF, 0xD9, 0xFF, 0xFF);

    public static void Build()
    {
        GameObject old = GameObject.Find("Exterior");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Exterior").transform;

        // ciel clair et ambiance tri-couleur (ciel, horizon, sol)
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.85f, 1f) * 0.85f;
        RenderSettings.ambientEquatorColor = new Color(0.96f, 0.94f, 0.89f) * 0.8f;
        RenderSettings.ambientGroundColor = new Color(0.76f, 0.8f, 0.84f) * 0.7f;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.color = new Color32(0xFF, 0xF1, 0xD6, 0xFF);

        Material grass = M("Ext_Grass", new Color32(0x8B, 0xCB, 0x6B, 0xFF));
        Material asphalt = M("Ext_Asphalt", new Color32(0x5B, 0x63, 0x70, 0xFF));
        Material white = M("Ext_Line", new Color32(0xF4, 0xF1, 0xE8, 0xFF));
        Material hedge = M("Ext_Hedge", new Color32(0x5F, 0xA3, 0x52, 0xFF));
        Material crown = M("Ext_Crown", new Color32(0x6D, 0xBB, 0x5A, 0xFF));
        Material trunk = M("Ext_Trunk", new Color32(0x8A, 0x5A, 0x3A, 0xFF));
        Material metal = M("Ext_Metal", new Color32(0x4A, 0x52, 0x60, 0xFF));
        Material cone = M("Ext_Cone", new Color32(0xFF, 0x7A, 0x1A, 0xFF));
        Material lampGlow = M("Ext_LampGlow", new Color32(0xFF, 0xE9, 0xA8, 0xFF));

        // sol extérieur : grande pelouse + parking en asphalte devant la porte roulante (x = 14, z = -10,9)
        Slab(root, "Grass", new Vector3(4f, -0.06f, -2f), new Vector3(120f, 0.1f, 120f), grass);
        Slab(root, "Parking", new Vector3(24f, -0.03f, -10.9f), new Vector3(18f, 0.1f, 11f), asphalt);
        for (int i = 0; i < 4; i++) // places de stationnement : lignes blanches
            Slab(root, "Line" + i, new Vector3(18f + i * 3.6f, -0.01f, -13.6f), new Vector3(0.14f, 0.02f, 3.2f), white);
        Slab(root, "LineCenter", new Vector3(24f, -0.01f, -9f), new Vector3(17f, 0.02f, 0.12f), white);
        Slab(root, "Sidewalk", new Vector3(16.2f, -0.02f, -10.9f), new Vector3(3f, 0.1f, 11f), M("Ext_Sidewalk", new Color32(0xD5, 0xDC, 0xE4, 0xFF)));

        // haies, arbres, lampadaire, cônes
        foreach (float z in new[] { -17f, -4.5f })
            RoundedBox.Make(root, "Hedge", new Vector3(24f, 0.45f, z), new Vector3(16f, 0.9f, 1.1f), 0.3f, hedge);
        Tree(root, new Vector3(20f, 0f, -18.5f), trunk, crown);
        Tree(root, new Vector3(28.5f, 0f, -3f), trunk, crown);
        Tree(root, new Vector3(-12f, 0f, 14f), trunk, crown);
        Tree(root, new Vector3(18f, 0f, 4f), trunk, crown);
        Cylinder(root, "LampPost", new Vector3(17.2f, 1.6f, -7.2f), new Vector3(0.14f, 1.6f, 0.14f), metal);
        Sphere(root, "LampGlow", new Vector3(17.2f, 3.35f, -7.2f), Vector3.one * 0.5f, lampGlow);
        foreach (Vector3 p in new[] { new Vector3(16.6f, 0.2f, -9.6f), new Vector3(16.6f, 0.2f, -12.3f), new Vector3(19.4f, 0.2f, -8.6f) })
            Cylinder(root, "Cone", p, new Vector3(0.28f, 0.2f, 0.28f), cone);

        // côté sud (derrière le mur bas de devant) : chemin de gravier, haie et deux arbres
        Material path = M("Ext_Path", new Color32(0xE6, 0xDD, 0xC6, 0xFF));
        Slab(root, "SouthPath", new Vector3(4f, -0.02f, -16.5f), new Vector3(2.4f, 0.1f, 4.2f), path);
        RoundedBox.Make(root, "SouthHedgeL", new Vector3(-3f, 0.45f, -15.6f), new Vector3(14f, 0.9f, 1.1f), 0.3f, hedge);
        RoundedBox.Make(root, "SouthHedgeR", new Vector3(14.5f, 0.45f, -15.6f), new Vector3(15f, 0.9f, 1.1f), 0.3f, hedge);
        Tree(root, new Vector3(0.5f, 0f, -17.2f), trunk, crown);
        Tree(root, new Vector3(9f, 0f, -17.4f), trunk, crown);

        // accessoires d'atelier
        Material red = M("Ext_Red", new Color32(0xF2, 0x45, 0x3D, 0xFF));
        Material pot = M("Ext_Pot", new Color32(0xB9, 0x82, 0x4A, 0xFF));
        Material blue = M("Ext_WaterBlue", new Color32(0x1F, 0xA0, 0xFF, 0xFF));
        Material whiteBody = M("Ext_WhiteBody", new Color32(0xF4, 0xF1, 0xE8, 0xFF));
        Cylinder(root, "Extinguisher", new Vector3(-8.2f, 0.45f, -6f), new Vector3(0.28f, 0.45f, 0.28f), red);
        Cylinder(root, "ExtinguisherTop", new Vector3(-8.2f, 0.95f, -6f), new Vector3(0.12f, 0.08f, 0.12f), metal);
        Plant(root, new Vector3(-7.4f, 0f, -12.2f), pot, crown);
        Plant(root, new Vector3(13.2f, 0f, 3.4f), pot, crown);
        Plant(root, new Vector3(-7.4f, 0f, 10.4f), pot, crown);
        RoundedBox.Make(root, "WaterBody", new Vector3(13.3f, 0.55f, -1.4f), new Vector3(0.7f, 1.1f, 0.7f), 0.12f, whiteBody);
        RoundedBox.Make(root, "WaterTank", new Vector3(13.3f, 1.3f, -1.4f), new Vector3(0.55f, 0.45f, 0.55f), 0.12f, blue);

        foreach (Transform t in root.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    static Material M(string name, Color c) { return PrototypeSetup.MakeMaterial(name, c); }

    static void Slab(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Tree(Transform parent, Vector3 pos, Material trunk, Material crown)
    {
        Cylinder(parent, "TreeTrunk", pos + new Vector3(0f, 0.9f, 0f), new Vector3(0.3f, 0.9f, 0.3f), trunk);
        Sphere(parent, "TreeCrown", pos + new Vector3(0f, 2.5f, 0f), Vector3.one * 2.2f, crown);
        Sphere(parent, "TreeCrown2", pos + new Vector3(0.6f, 3.1f, 0.2f), Vector3.one * 1.5f, crown);
    }

    static void Plant(Transform parent, Vector3 pos, Material pot, Material leaves)
    {
        Cylinder(parent, "Pot", pos + new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.3f, 0.5f), pot);
        Sphere(parent, "Leaves", pos + new Vector3(0f, 0.95f, 0f), Vector3.one * 0.9f, leaves);
        Sphere(parent, "Leaves2", pos + new Vector3(0.2f, 1.3f, 0.1f), Vector3.one * 0.55f, leaves);
    }
}
