using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Étape 7a : sauvegarde JSON, gains hors ligne, écran « Bon retour ! » et menus de test. Relançable.
public static class SaveBuilder
{
    const string Root = "Assets/Prototype";

    [MenuItem("Tools/Idle Prototype/Effacer la sauvegarde")]
    static void EraseSave()
    {
        SaveSystem.DeleteSave();
        GuideConfig guide = GuideBuilder.LoadOrCreateConfig();
        PlayerPrefs.DeleteKey(guide.DoneKey);
        PlayerPrefs.Save();
        Debug.Log("Sauvegarde effacée (" + SaveSystem.FilePath + "). Prochain Play : nouvelle partie avec le guide.");
    }

    [MenuItem("Tools/Idle Prototype/Simuler une absence de 2 h")]
    static void SimulateAbsence()
    {
        if (SaveSystem.SimulateAbsence(2f))
            Debug.Log("Absence de 2 h simulée. Arrête puis relance Play pour voir l'écran « Bon retour ».");
        else
            Debug.LogWarning("Aucune sauvegarde : joue un moment (elle s'écrit toutes les 10 s), arrête Play, puis relance ce menu.");
    }

    public static OfflineConfig LoadOrCreateConfig()
    {
        string path = Root + "/OfflineConfig.asset";
        OfflineConfig config = AssetDatabase.LoadAssetAtPath<OfflineConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<OfflineConfig>();
            AssetDatabase.CreateAsset(config, path);
        }
        return config;
    }

    public static void Build()
    {
        Wallet wallet = Object.FindFirstObjectByType<Wallet>();
        Transform canvas = GameObject.Find("Canvas") != null ? GameObject.Find("Canvas").transform : null;
        if (wallet == null || canvas == null) return;

        foreach (string old in new[] { "SaveSystem" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }
        Transform oldScreen = canvas.Find("OfflineScreen");
        if (oldScreen != null) Object.DestroyImmediate(oldScreen.gameObject);

        OfflineWelcomeScreen screen = BuildScreen(canvas);

        GameObject hauler = FindInactive<Employee>();
        GameObject courier = FindInactive<Courier>();

        GameObject go = new GameObject("SaveSystem", typeof(SaveSystem));
        var so = new SerializedObject(go.GetComponent<SaveSystem>());
        so.FindProperty("wallet").objectReferenceValue = wallet;
        UnlockPad[] pads = Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(p => p.name).ToArray();
        SerializedProperty list = so.FindProperty("pads");
        list.arraySize = pads.Length;
        for (int i = 0; i < pads.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = pads[i];
        ProcessStation[] stations = Object.FindObjectsByType<ProcessStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(s => s.name).ToArray();
        SerializedProperty stationList = so.FindProperty("stations");
        stationList.arraySize = stations.Length;
        for (int i = 0; i < stations.Length; i++)
            stationList.GetArrayElementAtIndex(i).objectReferenceValue = stations[i];
        so.FindProperty("belt1").objectReferenceValue = FindRoot("Belt1");
        so.FindProperty("belt2").objectReferenceValue = FindRoot("Belt2");
        so.FindProperty("testConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MachineConfig>(Root + "/TestMachineConfig.asset");
        so.FindProperty("packConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MachineConfig>(Root + "/PackMachineConfig.asset");
        so.FindProperty("proPoints").objectReferenceValue = Object.FindFirstObjectByType<ProPoints>();
        so.FindProperty("reputation").objectReferenceValue = Object.FindFirstObjectByType<ReputationState>();
        so.FindProperty("machineUpgrades").objectReferenceValue = Object.FindFirstObjectByType<MachineUpgradeState>();
        so.FindProperty("haulerObject").objectReferenceValue = hauler;
        so.FindProperty("courierObject").objectReferenceValue = courier;
        so.FindProperty("upgradeState").objectReferenceValue = Object.FindFirstObjectByType<UpgradeState>();
        so.FindProperty("audioManager").objectReferenceValue = Object.FindFirstObjectByType<AudioManager>();
        so.FindProperty("guideConfig").objectReferenceValue = GuideBuilder.LoadOrCreateConfig();
        so.FindProperty("offlineConfig").objectReferenceValue = LoadOrCreateConfig();
        so.FindProperty("stationConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StationConfig>(Root + "/StationConfig.asset");
        so.FindProperty("haulerConfig").objectReferenceValue = EmployeeBuilder.LoadOrCreateConfig();
        so.FindProperty("courierConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EmployeeConfig>(Root + "/CourierConfig.asset");
        so.FindProperty("customerConfig").objectReferenceValue = CustomerBuilder.LoadOrCreateConfig();
        so.FindProperty("welcomeScreen").objectReferenceValue = screen;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject FindRoot(string objectName)
    {
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g.name == objectName && g.transform.parent == null) return g;
        return null;
    }

    static GameObject FindInactive<T>() where T : Component
    {
        T c = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        return c != null ? c.gameObject : null;
    }

    // ------------------------------------------------------------------ écran « Bon retour ! »

    static OfflineWelcomeScreen BuildScreen(Transform canvas)
    {
        Sprite round = UiSprites.Rounded();

        GameObject host = new GameObject("OfflineScreen", typeof(RectTransform), typeof(OfflineWelcomeScreen));
        host.transform.SetParent(canvas, false);
        Stretch(host.GetComponent<RectTransform>());

        // fond sombre plein écran : bloque aussi le joystick tant que l'écran est ouvert
        GameObject root = new GameObject("Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(host.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

        RectTransform panel = Box("Panel", root.transform, round, new Color(0.10f, 0.13f, 0.18f, 0.9f), new Vector2(0f, 60f), new Vector2(860f, 900f));
        panel.gameObject.AddComponent<PopIn>(); // léger « pop » à l'ouverture
        Label("Title", panel, Loc.T("offline.title"), 96, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 320f), new Vector2(800f, 140f));
        Text duration = Label("Duration", panel, "Absent depuis 2 h 00 min", 46, new Color(0.85f, 0.88f, 0.92f), new Vector2(0f, 190f), new Vector2(800f, 90f));
        Text amount = Label("Amount", panel, "+$ 0", 150, new Color(0.45f, 0.95f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 220f));

        Button claim = MakeButton("ClaimButton", panel, round, "Récupérer", 64, new Color(0.2f, 0.7f, 0.3f), Color.white, new Vector2(0f, -190f), new Vector2(660f, 150f));
        Button doubled = MakeButton("DoubleButton", panel, round, "×2  (bientôt)", 46, new Color(0.3f, 0.33f, 0.38f), new Color(0.65f, 0.68f, 0.72f), new Vector2(0f, -350f), new Vector2(660f, 110f));
        doubled.interactable = false;

        var so = new SerializedObject(host.GetComponent<OfflineWelcomeScreen>());
        so.FindProperty("root").objectReferenceValue = root;
        so.FindProperty("durationText").objectReferenceValue = duration;
        so.FindProperty("amountText").objectReferenceValue = amount;
        so.FindProperty("claimButton").objectReferenceValue = claim;
        so.ApplyModifiedPropertiesWithoutUndo();
        return host.GetComponent<OfflineWelcomeScreen>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform Box(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = size.y > 500f ? 0.9f : 0.8f; // coins bien arrondis
        img.color = color;
        return rt;
    }

    static Text Label(string name, Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        Text t = go.GetComponent<Text>();
        UiTheme.Style(t, size, color);
        t.alignment = TextAnchor.MiddleCenter;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(string name, Transform parent, Sprite sprite, string text, int size, Color back, Color textColor, Vector2 pos, Vector2 boxSize)
    {
        RectTransform rt = Box(name, parent, sprite, back, pos, boxSize);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = rt.GetComponent<Image>();
        Text t = Label("Text", rt, text, size, textColor, Vector2.zero, boxSize);
        return b;
    }
}
