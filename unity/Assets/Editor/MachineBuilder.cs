using UnityEditor;
using UnityEngine;

// Chaîne de production (11a) : ajoute à chaque machine sa lampe et sa pastille de stock.
// Appelé par PrototypeSetup une fois les postes construits ; ChainBuilder s'en sert pour Test et Emballage.
public static class MachineBuilder
{
    public static void Build()
    {
        foreach (InstallStation station in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Renderer lamp = AddLamp(station.transform, new Vector3(1.35f, 0f, -1.1f), 1.5f, "StationLamp");
            AddStockHud(station.transform, station, 2.6f, lamp);
        }
    }

    // Lampe cylindrique jaune sur un petit poteau (le poste d'installation est un établi, pas une machine fermée).
    public static Renderer AddLamp(Transform machine, Vector3 localBase, float topY, string lampName)
    {
        Transform old = machine.Find(lampName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(lampName).transform;
        root.SetParent(machine, false);
        root.localPosition = new Vector3(localBase.x, 0f, localBase.z);
        Material post = PrototypeSetup.MakeMaterial("LampPost", new Color(0.25f, 0.28f, 0.33f));
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Post";
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        pole.transform.SetParent(root, false);
        pole.transform.localPosition = new Vector3(0f, topY * 0.5f, 0f);
        pole.transform.localScale = new Vector3(0.08f, topY * 0.5f, 0.08f);
        pole.GetComponent<Renderer>().sharedMaterial = post;
        return MakeLampHead(root, new Vector3(0f, topY + 0.12f, 0f));
    }

    public static Renderer MakeLampHead(Transform parent, Vector3 localPos)
    {
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lamp.name = "Lamp";
        Object.DestroyImmediate(lamp.GetComponent<Collider>());
        lamp.transform.SetParent(parent, false);
        lamp.transform.localPosition = localPos;
        lamp.transform.localScale = new Vector3(0.3f, 0.14f, 0.3f);
        Renderer r = lamp.GetComponent<Renderer>();
        Shader lit = Shader.Find("Universal Render Pipeline/Unlit");
        r.sharedMaterial = PrototypeSetup.MakeMaterial("MachineLampMat", Color.white); // couleur pilotée par StockLabel
        if (lit != null && r.sharedMaterial.shader != lit) r.sharedMaterial.shader = lit;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    // Pastille ardoise (stock d'entrée et de sortie) au-dessus de `machine`, face caméra, taille écran constante.
    public static StockLabel AddStockHud(Transform machine, MonoBehaviour status, float height, Renderer lamp)
    {
        Transform old = machine.Find("StockHud");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("StockHud");
        root.transform.SetParent(machine, false);
        root.transform.localPosition = new Vector3(0f, height, 0f);
        var face = root.AddComponent<FaceCamera>();
        var fso = new SerializedObject(face);
        fso.FindProperty("keepScreenSize").boolValue = true;
        fso.FindProperty("referenceDistance").floatValue = 9.2f; // distance de la caméra de jeu
        fso.ApplyModifiedPropertiesWithoutUndo();

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        // pastille : rectangle arrondi sombre
        var pillGo = new GameObject("Pill", typeof(SpriteRenderer));
        pillGo.transform.SetParent(visual, false);
        SpriteRenderer pill = pillGo.GetComponent<SpriteRenderer>();
        pill.sprite = UiSprites.Rounded();
        pill.drawMode = SpriteDrawMode.Sliced;
        pill.size = new Vector2(2.05f, 0.55f);
        pill.color = UiTheme.Slate;
        pill.sortingOrder = 10;

        // icônes : caisse (entrée) et produit (sortie) en petits carrés arrondis colorés
        Icon(visual, "CrateIcon", new Vector3(-0.82f, 0f, 0f), new Color32(0xB9, 0x82, 0x4A, 0xFF));
        Icon(visual, "ProductIcon", new Vector3(0.12f, 0f, 0f), new Color32(0xE8, 0xEE, 0xF5, 0xFF));
        TextMesh[] inputs = Number(visual, "In", new Vector3(-0.6f, 0f, 0f));
        TextMesh[] outputs = Number(visual, "Out", new Vector3(0.34f, 0f, 0f));
        visual.localScale = Vector3.one * 0.6f; // pastille ~ 0,9 m à la distance de référence (< 9 % de la largeur d'écran)

        var label = root.AddComponent<StockLabel>();
        var so = new SerializedObject(label);
        so.FindProperty("machine").objectReferenceValue = status;
        so.FindProperty("visual").objectReferenceValue = visual.gameObject;
        so.FindProperty("pill").objectReferenceValue = pill;
        so.FindProperty("lamp").objectReferenceValue = lamp;
        SerializedProperty a = so.FindProperty("inputTexts");
        a.arraySize = inputs.Length;
        for (int i = 0; i < inputs.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = inputs[i];
        SerializedProperty b = so.FindProperty("outputTexts");
        b.arraySize = outputs.Length;
        for (int i = 0; i < outputs.Length; i++) b.GetArrayElementAtIndex(i).objectReferenceValue = outputs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return label;
    }

    static void Icon(Transform parent, string iconName, Vector3 pos, Color color)
    {
        var go = new GameObject(iconName, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = UiSprites.Rounded();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(0.3f, 0.3f);
        sr.color = color;
        sr.sortingOrder = 11;
    }

    // Nombre en Lilita : contour encre (copie décalée derrière) + texte blanc.
    static TextMesh[] Number(Transform parent, string numberName, Vector3 pos)
    {
        TextMesh outline = MakeText(numberName + "Outline", parent, pos + new Vector3(0.025f, -0.025f, 0.01f), UiTheme.Ink, 12);
        TextMesh text = MakeText(numberName, parent, pos, Color.white, 13);
        return new[] { text, outline };
    }

    public static TextMesh MakeText(string textName, Transform parent, Vector3 pos, Color color, int order, float characterSize = 0.07f, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(textName, typeof(MeshRenderer), typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        TextMesh tm = go.GetComponent<TextMesh>();
        tm.font = UiTheme.Font;
        tm.fontSize = 64;
        tm.characterSize = characterSize;
        tm.anchor = anchor;
        tm.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
        tm.color = color;
        tm.text = "0/0";
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = UiTheme.Font.material;
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return tm;
    }
}
