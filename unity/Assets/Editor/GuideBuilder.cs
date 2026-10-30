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
        Transform oldHand = safeArea.Find("GuideHand");
        if (oldHand != null) Object.DestroyImmediate(oldHand.gameObject);
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
        so.FindProperty("payPadObject").objectReferenceValue = FindPad("UnlockPad");
        so.FindProperty("hirePadObject").objectReferenceValue = FindPad("HirePad");
        so.FindProperty("courierPadObject").objectReferenceValue = FindPad("HirePadCourier");
        so.FindProperty("testPad").objectReferenceValue = PadComponent("TestPad");
        so.FindProperty("packPad").objectReferenceValue = PadComponent("PackPad");
        so.FindProperty("beltPad").objectReferenceValue = PadComponent("BeltPad1");
        so.FindProperty("hand").objectReferenceValue = BuildHand(safeArea);
        so.FindProperty("upgradePanel").objectReferenceValue = Object.FindFirstObjectByType<UpgradePanel>();
        so.FindProperty("arrow").objectReferenceValue = arrow;
        so.FindProperty("board").objectReferenceValue = Object.FindFirstObjectByType<OrderBoard>();
        so.FindProperty("machineUpgrades").objectReferenceValue = Object.FindFirstObjectByType<MachineUpgradeState>();
        so.FindProperty("advisor").objectReferenceValue = Object.FindFirstObjectByType<MachineUpgradeAdvisor>();
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("labelRoot").objectReferenceValue = bar;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static UnlockPad PadComponent(string padName)
    {
        GameObject g = FindPad(padName);
        return g != null ? g.GetComponent<UnlockPad>() : null;
    }

    // P1 : main qui pointe, bout du doigt en haut ; n'intercepte aucun toucher.
    static RectTransform BuildHand(Transform parent)
    {
        GameObject go = new GameObject("GuideHand", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.88f);   // le bout du doigt est posé sur l'objectif
        rt.sizeDelta = new Vector2(130f, 130f);
        var img = go.GetComponent<Image>();
        img.sprite = UiSprites.Hand();
        img.color = new Color(1f, 0.87f, 0.68f);
        img.raycastTarget = false;
        go.SetActive(false);
        return rt;
    }

    // Les dalles d'embauche sont cachées au départ : GameObject.Find ne les voit pas.
    static GameObject FindPad(string padName)
    {
        foreach (UnlockPad pad in Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pad.name == padName) return pad.gameObject;
        return null;
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
        Sprite round = UiSprites.Rounded();

        GameObject bar = new GameObject("GuideBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(safeArea, false);
        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -330f); // sous les trois pastilles
        rt.sizeDelta = new Vector2(960f, 100f);
        Image img = bar.GetComponent<Image>();
        img.sprite = round;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.85f;
        img.color = UiTheme.Slate; // ardoise opaque (maquette)
        img.raycastTarget = false;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bar.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(24f, 0f);
        tr.offsetMax = new Vector2(-24f, 0f);
        label = textGo.GetComponent<Text>();
        UiTheme.Style(label, 50, Color.white);
        label.resizeTextForBestFit = true; // une seule ligne, la taille s'adapte
        label.resizeTextMinSize = 30;
        label.resizeTextMaxSize = 50;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.text = "";
        return bar;
    }
}
