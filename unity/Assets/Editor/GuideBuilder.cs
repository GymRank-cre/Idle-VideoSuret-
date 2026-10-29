using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Étape 6b : guide d'objectif du début de partie (flèche 3D au-dessus du joueur + une phrase en haut de l'écran).
// Formes simples, texte UI classique, aucun package.
public static class GuideBuilder
{
    const string Root = "Assets/Prototype";

    [MenuItem("Tools/Idle Prototype/Réinitialiser le guide")]
    static void ResetGuide()
    {
        GuideConfig config = LoadOrCreateConfig();
        PlayerPrefs.DeleteKey(config.DoneKey);
        PlayerPrefs.Save();
        Debug.Log("Guide réinitialisé : il réapparaîtra au prochain Play.");
    }

    public static GuideConfig LoadOrCreateConfig()
    {
        string path = Root + "/GuideConfig.asset";
        GuideConfig config = AssetDatabase.LoadAssetAtPath<GuideConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GuideConfig>();
            AssetDatabase.CreateAsset(config, path); // conservé s'il existe
        }
        return config;
    }

    public static void Build()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        ResourcePile pile = Object.FindFirstObjectByType<ResourcePile>();
        GameObject station = GameObject.Find("InstallStation");
        GameObject counter = GameObject.Find("ServiceCounter");
        Transform safeArea = GameObject.Find("Canvas") != null ? GameObject.Find("Canvas").transform.Find("HudSafeArea") : null;
        if (player == null || pile == null || station == null || safeArea == null) return;

        foreach (string old in new[] { "ObjectiveGuide", "GuideArrow" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }
        Transform oldBar = safeArea.Find("GuideBar");
        if (oldBar != null) Object.DestroyImmediate(oldBar.gameObject);

        GuideConfig config = LoadOrCreateConfig();
        Transform arrow = BuildArrow();
        Text label;
        GameObject bar = BuildBar(safeArea, out label);

        GameObject go = new GameObject("ObjectiveGuide", typeof(ObjectiveGuide));
        var so = new SerializedObject(go.GetComponent<ObjectiveGuide>());
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.FindProperty("crates").objectReferenceValue = player.GetComponent<StackCarrier>();
        so.FindProperty("products").objectReferenceValue = player.GetComponent<ProductCarrier>();
        so.FindProperty("wallet").objectReferenceValue = Object.FindFirstObjectByType<Wallet>();
        so.FindProperty("pile").objectReferenceValue = pile.transform;
        so.FindProperty("station").objectReferenceValue = station.transform;
        so.FindProperty("tray").objectReferenceValue = station.transform.Find("OutputTray");
        so.FindProperty("counter").objectReferenceValue = counter != null ? counter.transform : null;
        so.FindProperty("payPadObject").objectReferenceValue = GameObject.Find("UnlockPad");
        so.FindProperty("hirePadObject").objectReferenceValue = GameObject.Find("HirePad");
        so.FindProperty("arrow").objectReferenceValue = arrow;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("labelRoot").objectReferenceValue = bar;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Flèche 3D : une tige et une tête en losange (un cube tourné de 45°), jaune lumineux ; elle pointe vers +z.
    static Transform BuildArrow()
    {
        Material mat = HumanCharacterBuilder.Mat("Guide_Arrow", new Color(1f, 0.85f, 0.15f), 0.4f, new Color(1.2f, 0.9f, 0.1f));
        Material rim = HumanCharacterBuilder.Mat("Guide_ArrowRim", new Color(0.15f, 0.12f, 0.02f), 0.2f);

        GameObject root = new GameObject("GuideArrow");
        Part(root.transform, "ShaftRim", new Vector3(0f, -0.012f, 0f), new Vector3(0.2f, 0.07f, 0.56f), 0f, rim);
        Part(root.transform, "HeadRim", new Vector3(0f, -0.012f, 0.48f), new Vector3(0.5f, 0.07f, 0.5f), 45f, rim);
        Part(root.transform, "Shaft", new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.08f, 0.5f), 0f, mat);
        Part(root.transform, "Head", new Vector3(0f, 0f, 0.48f), new Vector3(0.42f, 0.08f, 0.42f), 45f, mat);
        root.transform.localScale = Vector3.one * 1.1f;
        root.SetActive(false);
        return root.transform;
    }

    static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, float yaw, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // Phrase : une ligne dans une pastille sombre arrondie, sous les pastilles du HUD (zone sûre de l'écran).
    static GameObject BuildBar(Transform safeArea, out Text label)
    {
        Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject bar = new GameObject("GuideBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(safeArea, false);
        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -330f); // sous les trois pastilles
        rt.sizeDelta = new Vector2(960f, 100f);
        Image img = bar.GetComponent<Image>();
        img.sprite = round;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 3f;
        img.color = new Color(0.05f, 0.07f, 0.10f, 0.6f);
        img.raycastTarget = false;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bar.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(24f, 0f);
        tr.offsetMax = new Vector2(-24f, 0f);
        label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 50;
        label.resizeTextForBestFit = true; // une seule ligne, la taille s'adapte
        label.resizeTextMinSize = 28;
        label.resizeTextMaxSize = 50;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        textGo.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        label.text = "";
        return bar;
    }
}
