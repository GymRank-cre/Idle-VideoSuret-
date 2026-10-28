using UnityEditor;
using UnityEngine;

// Produits finis (étape 5a) : 3 prefabs en formes simples, leurs ProductType, le plateau de sortie d'un poste,
// et l'action de test « Vider les produits ». Aucun asset externe.
public static class ProductBuilder
{
    const string Folder = "Assets/Prototype/Products";

    [MenuItem("Tools/Idle Prototype/Vider les produits")]
    static void ClearCarried()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("Vider les produits : à utiliser en mode Play.");
            return;
        }
        ProductCarrier carrier = Object.FindFirstObjectByType<ProductCarrier>();
        if (carrier != null) carrier.Clear();
    }

    // Les 3 types de produits (assets conservés s'ils existent : les valeurs modifiées ne sont pas écrasées).
    public static ProductType[] EnsureProductTypes()
    {
        EnsureFolder(Folder);
        return new[]
        {
            EnsureType("Dome", "Caméra dôme", 25, new Color(0.85f, 0.9f, 0.95f), BuildDome()),
            EnsureType("Badgeuse", "Badgeuse", 40, new Color(0.3f, 0.9f, 0.4f), BuildBadge()),
            EnsureType("Alarme", "Alarme", 60, new Color(0.9f, 0.2f, 0.2f), BuildAlarm()),
        };
    }

    static ProductType EnsureType(string file, string displayName, int value, Color accent, GameObject prefab)
    {
        string path = Folder + "/" + file + ".asset";
        ProductType type = AssetDatabase.LoadAssetAtPath<ProductType>(path);
        if (type == null)
        {
            type = ScriptableObject.CreateInstance<ProductType>();
            AssetDatabase.CreateAsset(type, path);
            var created = new SerializedObject(type);
            created.FindProperty("displayName").stringValue = displayName;
            created.FindProperty("value").intValue = value;
            created.FindProperty("accentColor").colorValue = accent;
            created.ApplyModifiedPropertiesWithoutUndo();
        }
        var so = new SerializedObject(type);
        so.FindProperty("prefab").objectReferenceValue = prefab; // le prefab est refait à chaque Build
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(type);
        return type;
    }

    // ------------------------------------------------------------------ produits (origine au pied, face = +z)

    // Caméra dôme : disque blanc + demi-sphère sombre + petit objectif.
    static GameObject BuildDome()
    {
        GameObject root = NewProduct("Product_Dome", new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.2f, 0.3f));
        Part(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.02f, 0.3f), Mat("Prod_White", new Color(0.93f, 0.94f, 0.96f)));
        Part(PrimitiveType.Sphere, "Dome", root, new Vector3(0f, 0.045f, 0f), new Vector3(0.22f, 0.15f, 0.22f), Mat("Prod_Glass", new Color(0.1f, 0.12f, 0.16f), 0.9f));
        Part(PrimitiveType.Sphere, "Lens", root, new Vector3(0f, 0.08f, 0.07f), new Vector3(0.05f, 0.05f, 0.04f), Mat("Prod_Black", new Color(0.02f, 0.02f, 0.03f)));
        return Save(root);
    }

    // Badgeuse : boîte noire avec écran et voyant vert.
    static GameObject BuildBadge()
    {
        GameObject root = NewProduct("Product_Badge", new Vector3(0f, 0.17f, 0f), new Vector3(0.24f, 0.34f, 0.12f));
        Part(PrimitiveType.Cube, "Stand", root, new Vector3(0f, 0.012f, 0f), new Vector3(0.22f, 0.024f, 0.12f), Mat("Prod_Black", new Color(0.02f, 0.02f, 0.03f)));
        Part(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0.18f, 0f), new Vector3(0.2f, 0.3f, 0.05f), Mat("Prod_Dark", new Color(0.09f, 0.1f, 0.12f), 0.5f));
        Part(PrimitiveType.Cube, "Screen", root, new Vector3(0f, 0.23f, 0.027f), new Vector3(0.14f, 0.1f, 0.006f), Mat("Prod_Screen", new Color(0.12f, 0.35f, 0.55f), 0.7f, new Color(0.1f, 0.4f, 0.7f)));
        Part(PrimitiveType.Sphere, "Led", root, new Vector3(0f, 0.1f, 0.03f), Vector3.one * 0.04f, Mat("Prod_LedGreen", new Color(0.2f, 0.9f, 0.3f), 0.5f, new Color(0.3f, 2.2f, 0.5f)));
        return Save(root);
    }

    // Alarme : boîte rouge avec sirène jaune.
    static GameObject BuildAlarm()
    {
        GameObject root = NewProduct("Product_Alarm", new Vector3(0f, 0.13f, 0f), new Vector3(0.28f, 0.26f, 0.14f));
        Part(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0.09f, 0f), new Vector3(0.26f, 0.16f, 0.12f), Mat("Prod_Red", new Color(0.85f, 0.18f, 0.18f), 0.4f));
        Part(PrimitiveType.Cylinder, "SirenBase", root, new Vector3(0f, 0.18f, 0f), new Vector3(0.1f, 0.012f, 0.1f), Mat("Prod_Black", new Color(0.02f, 0.02f, 0.03f)));
        Part(PrimitiveType.Sphere, "Siren", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 0.075f, 0.1f), Mat("Prod_Siren", new Color(1f, 0.8f, 0.1f), 0.6f, new Color(1.6f, 1.1f, 0.1f)));
        Part(PrimitiveType.Cube, "Grill", root, new Vector3(0f, 0.09f, 0.062f), new Vector3(0.14f, 0.05f, 0.006f), Mat("Prod_Dark", new Color(0.09f, 0.1f, 0.12f), 0.5f));
        return Save(root);
    }

    static GameObject NewProduct(string name, Vector3 colliderCenter, Vector3 colliderSize)
    {
        GameObject root = new GameObject(name);
        BoxCollider box = root.AddComponent<BoxCollider>(); // avant Product, qui exige un collider
        box.isTrigger = true; // détecté par ProductCarrier
        box.center = colliderCenter;
        box.size = colliderSize;
        root.AddComponent<Product>();
        return root;
    }

    static GameObject Save(GameObject root)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + root.name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ plateau de sortie

    // Plateau acier avec liseré jaune, sur le côté ouest du poste ; les places sont alignées sur son axe x.
    public static Transform BuildTray(Transform station)
    {
        Transform old = station.Find("OutputTray");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject tray = new GameObject("OutputTray");
        tray.transform.SetParent(station, false);
        tray.transform.localPosition = new Vector3(-2.1f, -0.1f, 0f); // le poste est à y = 0,1 : le plateau pose au sol
        tray.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        Material steel = Mat("Prod_TraySteel", new Color(0.35f, 0.43f, 0.52f), 0.4f);
        Material yellow = Mat("Prod_TrayYellow", new Color(1f, 0.8f, 0.1f), 0.3f);
        Part(PrimitiveType.Cube, "Base", tray, new Vector3(0f, 0.03f, 0f), new Vector3(1.9f, 0.06f, 0.62f), steel);
        Part(PrimitiveType.Cube, "EdgeFront", tray, new Vector3(0f, 0.062f, -0.3f), new Vector3(1.9f, 0.012f, 0.03f), yellow);
        Part(PrimitiveType.Cube, "EdgeBack", tray, new Vector3(0f, 0.062f, 0.3f), new Vector3(1.9f, 0.012f, 0.03f), yellow);
        for (int i = 0; i < 4; i++) // marques des places
            Part(PrimitiveType.Cube, "Slot", tray, new Vector3((i - 1.5f) * 0.42f, 0.062f, 0f), new Vector3(0.34f, 0.006f, 0.42f), Mat("Prod_TraySlot", new Color(0.22f, 0.28f, 0.35f), 0.2f));
        return tray.transform;
    }

    // ------------------------------------------------------------------ outils

    static GameObject Part(PrimitiveType type, string name, GameObject parent, Vector3 pos, Vector3 scale, Material mat)
    {
        return Part(type, name, parent.transform, pos, scale, mat);
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material Mat(string name, Color color, float smoothness = 0.3f, Color? emission = null)
    {
        EnsureFolder("Assets/Prototype/Materials");
        string path = "Assets/Prototype/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
