using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Améliorations par machine (11b) : configuration, état, dalles VITESSE (flèche) et AJOUTER (+) à côté de chaque machine,
// pastille « Niv N » et pièces de paliers sur les machines, conseiller « meilleur achat ». Appelé après ChainBuilder.
public static class MachineUpgradeBuilder
{
    const string Root = "Assets/Prototype";
    const float PadScale = 0.65f; // dalles de 1,95 m : six dalles de plus ne doivent pas encombrer l'atelier

    struct Spot
    {
        public string id;
        public string label;
        public Vector3 speed, add;
    }

    public static void Build()
    {
        foreach (string old in new[] { "MachineUpgradeState", "MachineUpgradeAdvisor" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }
        foreach (UnlockPad pad in Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pad != null && pad.name.StartsWith("UpPad_")) Object.DestroyImmediate(pad.gameObject);

        MachineUpgradeConfig config = LoadOrCreateConfig();
        var stateGo = new GameObject("MachineUpgradeState", typeof(MachineUpgradeState));
        var state = stateGo.GetComponent<MachineUpgradeState>();
        Set(state, "config", config);

        var spots = new[]
        {
            new Spot { id = "assembly", label = "Assemblage", speed = new Vector3(-5.5f, 0.01f, -3.4f), add = new Vector3(-3.3f, 0.01f, -3.4f) },
            new Spot { id = "test", label = "Test", speed = new Vector3(-2.7f, 0.01f, 2.4f), add = new Vector3(-0.5f, 0.01f, 2.4f) },
            new Spot { id = "pack", label = "Emballage", speed = new Vector3(9.6f, 0.01f, 6.1f), add = new Vector3(9.6f, 0.01f, 8.5f) },
        };

        InstallStation assembly = null;
        foreach (InstallStation s in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.name == "InstallStation") assembly = s;
        GameObject testRoot = FindRoot("TestMachine"), packRoot = FindRoot("PackMachine");
        var machineRoots = new Dictionary<string, GameObject>
        {
            { "assembly", assembly != null ? assembly.gameObject : null }, { "test", testRoot }, { "pack", packRoot }
        };

        // machines : niveaux appliqués sans toucher à leur logique
        if (assembly != null) { Set(assembly, "machineUpgrades", state); Set(assembly, "machineId", "assembly"); }
        if (testRoot != null) { var pm = testRoot.GetComponent<ProductionMachine>(); Set(pm, "upgrades", state); Set(pm, "machineId", "test"); }
        if (packRoot != null) { var pm = packRoot.GetComponent<ProductionMachine>(); Set(pm, "upgrades", state); Set(pm, "machineId", "pack"); }

        var allPads = new List<MachineUpgradePad>();
        foreach (Spot spot in spots)
        {
            GameObject root = machineRoots[spot.id];
            if (root == null) continue;
            allPads.Add(BuildPad(state, spot.id, MachineUpgradeState.Speed, spot.speed, root));
            allPads.Add(BuildPad(state, spot.id, MachineUpgradeState.Add, spot.add, root));
            BuildLevelView(root.transform, state, spot.id);
        }

        // conseiller : flèche verte sur la machine goulot, activation des dalles quand la machine existe
        var advisorGo = new GameObject("MachineUpgradeAdvisor", typeof(MachineUpgradeAdvisor));
        var advisor = advisorGo.GetComponent<MachineUpgradeAdvisor>();
        var aso = new SerializedObject(advisor);
        SerializedProperty ms = aso.FindProperty("machines"), ids = aso.FindProperty("machineIds");
        ms.arraySize = ids.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            MonoBehaviour mb = spots[i].id == "assembly" ? (MonoBehaviour)assembly : machineRoots[spots[i].id] != null ? machineRoots[spots[i].id].GetComponent<ProductionMachine>() : null;
            ms.GetArrayElementAtIndex(i).objectReferenceValue = mb;
            ids.GetArrayElementAtIndex(i).stringValue = spots[i].id;
        }
        SerializedProperty ps = aso.FindProperty("pads");
        ps.arraySize = allPads.Count;
        for (int i = 0; i < allPads.Count; i++) ps.GetArrayElementAtIndex(i).objectReferenceValue = allPads[i];
        aso.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ dalle d'un axe

    static MachineUpgradePad BuildPad(MachineUpgradeState state, string machineId, int axis, Vector3 position, GameObject machineRoot)
    {
        string padName = "UpPad_" + machineId + (axis == MachineUpgradeState.Speed ? "_speed" : "_add");
        var dummy = new GameObject(padName + "_pop");
        dummy.transform.SetParent(machineRoot.transform, false);
        dummy.SetActive(true);
        UnlockPad pad = PrototypeSetup.BuildUnlockPad(dummy, dummy, position, padName, padName + "Config", null, 0, true);
        pad.transform.localScale = Vector3.one * PadScale;

        // source de prix : le niveau suivant de l'axe (un seul composant, lu par UnlockPad)
        var source = pad.gameObject.AddComponent<MachineUpgradePad>();
        Set(source, "state", state);
        Set(source, "machineId", machineId);
        Set(source, "axis", axis);
        Set(source, "feedback", Object.FindFirstObjectByType<FeedbackSpawner>());
        Set(source, "cameraFollow", Object.FindFirstObjectByType<CameraFollow>());
        Set(pad, "repeatSource", source);
        Set(pad, "saveId", padName);

        // icône à plat sur la plaque : flèche (vitesse) ou plus (ajouter)
        Sprite icon = axis == MachineUpgradeState.Speed ? UiSprites.Arrow() : UiSprites.Plus();
        Color tint = axis == MachineUpgradeState.Speed ? UiTheme.Yellow : UiTheme.Money;
        var iconGo = new GameObject("Icon", typeof(SpriteRenderer));
        iconGo.transform.SetParent(pad.transform, false);
        iconGo.transform.localPosition = new Vector3(0f, 0.06f, -0.5f);
        iconGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        iconGo.transform.localScale = Vector3.one * 0.7f;
        var sr = iconGo.GetComponent<SpriteRenderer>();
        sr.sprite = icon;
        sr.color = tint;

        // niveau « Niv N » au bord sud de la dalle
        var levelRoot = new GameObject("Level", typeof(FaceCamera)).transform;
        levelRoot.SetParent(pad.transform, false);
        levelRoot.localPosition = new Vector3(0f, 0.18f, -1.3f);
        TextMesh outline = MachineBuilder.MakeText("LevelOutline", levelRoot, new Vector3(0.025f, -0.025f, 0.01f), UiTheme.Ink, 12, 0.11f, TextAnchor.MiddleCenter);
        TextMesh text = MachineBuilder.MakeText("LevelText", levelRoot, Vector3.zero, Color.white, 13, 0.11f, TextAnchor.MiddleCenter);
        Set(source, "levelText", text);
        Set(source, "levelOutline", outline);

        // flèches : verte qui rebondit (meilleur achat), grise (inutile pour l'instant)
        Transform best = Arrow(pad.transform, "BestBuyArrow", new Vector3(0f, 2.3f, 0f), UiTheme.Green, 1.1f);
        Transform useless = Arrow(pad.transform, "UselessArrow", new Vector3(0f, 1.9f, 0f), new Color(0.62f, 0.66f, 0.72f, 0.5f), 0.45f); // atténuée et petite
        Set(source, "bestBuyArrow", best);
        Set(source, "uselessArrow", useless);

        // la dalle n'existe que quand sa machine est là : le conseiller l'active
        Set(source, "machineRoot", machineRoot);
        pad.gameObject.SetActive(false);
        return source;
    }

    static Transform Arrow(Transform parent, string arrowName, Vector3 pos, Color color, float scale)
    {
        var go = new GameObject(arrowName, typeof(SpriteRenderer), typeof(FaceCamera));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = UiSprites.Arrow();
        sr.flipY = true; // pointe vers la dalle
        sr.color = color;
        sr.sortingOrder = 5;
        go.SetActive(false);
        return go.transform;
    }

    // ------------------------------------------------------------------ pastille « Niv N » et pièces de paliers

    static void BuildLevelView(Transform machine, MachineUpgradeState state, string machineId)
    {
        foreach (string old in new[] { "LevelView", "MilestoneParts" })
        {
            Transform t = machine.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        bool isAssembly = machineId == "assembly";

        var view = new GameObject("LevelView", typeof(FaceCamera));
        view.transform.SetParent(machine, false);
        view.transform.localPosition = new Vector3(0f, isAssembly ? 2.1f : 2.35f, 0f);
        TextMesh outline = MachineBuilder.MakeText("Outline", view.transform, new Vector3(0.03f, -0.03f, 0.01f), UiTheme.Ink, 12, 0.13f, TextAnchor.MiddleCenter);
        TextMesh text = MachineBuilder.MakeText("Text", view.transform, Vector3.zero, UiTheme.Yellow, 13, 0.13f, TextAnchor.MiddleCenter);
        text.text = outline.text = "";

        var parts = new GameObject("MilestoneParts").transform;
        parts.SetParent(machine, false);
        Material gold = PrototypeSetup.MakeMaterial("MachineGold", new Color32(0xFF, 0xC4, 0x00, 0xFF));
        // 1er palier : bordure dorée ; 2e : deuxième lampe ; 3e : antenne
        GameObject trim = isAssembly
            ? RoundedBox.Make(parts, "GoldTrim", new Vector3(0f, 0.03f, 0f), new Vector3(3.4f, 0.06f, 3.4f), 0.06f, gold).gameObject
            : RoundedBox.Make(parts, "GoldTrim", new Vector3(0f, 1.12f, 0f), new Vector3(2.25f, 0.09f, 1.65f), 0.04f, gold).gameObject;
        GameObject lamp2 = MachineBuilder.MakeLampHead(parts, new Vector3(isAssembly ? -1.2f : 0.1f, isAssembly ? 1.2f : 1.95f, isAssembly ? -1.1f : 0.1f)).gameObject;
        lamp2.GetComponent<Renderer>().sharedMaterial = gold;
        GameObject antenna = new GameObject("Antenna");
        antenna.transform.SetParent(parts, false);
        var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(mast.GetComponent<Collider>());
        mast.transform.SetParent(antenna.transform, false);
        mast.transform.localPosition = new Vector3(isAssembly ? 1.2f : 0.85f, isAssembly ? 1.0f : 2.35f, isAssembly ? -1.1f : -0.3f);
        mast.transform.localScale = new Vector3(0.05f, isAssembly ? 1.0f : 0.5f, 0.05f);
        mast.GetComponent<Renderer>().sharedMaterial = gold;
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.transform.SetParent(antenna.transform, false);
        ball.transform.localPosition = mast.transform.localPosition + new Vector3(0f, mast.transform.localScale.y + 0.05f, 0f);
        ball.transform.localScale = Vector3.one * 0.16f;
        ball.GetComponent<Renderer>().sharedMaterial = gold;

        trim.SetActive(false); lamp2.SetActive(false); antenna.SetActive(false);
        var lv = view.AddComponent<MachineLevelView>();
        Set(lv, "state", state);
        Set(lv, "machineId", machineId);
        Set(lv, "text", text);
        Set(lv, "outline", outline);
        var so = new SerializedObject(lv);
        SerializedProperty list = so.FindProperty("milestoneParts");
        list.arraySize = 3;
        list.GetArrayElementAtIndex(0).objectReferenceValue = trim;
        list.GetArrayElementAtIndex(1).objectReferenceValue = lamp2;
        list.GetArrayElementAtIndex(2).objectReferenceValue = antenna;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ données

    static MachineUpgradeConfig LoadOrCreateConfig()
    {
        string path = Root + "/MachineUpgradeConfig.asset";
        MachineUpgradeConfig config = AssetDatabase.LoadAssetAtPath<MachineUpgradeConfig>(path);
        if (config != null) return config;

        config = ScriptableObject.CreateInstance<MachineUpgradeConfig>();
        AssetDatabase.CreateAsset(config, path);
        var so = new SerializedObject(config);
        SerializedProperty list = so.FindProperty("machines");
        list.arraySize = 3;
        Fill(list.GetArrayElementAtIndex(0), "assembly", "Assemblage", 120, 200, 50, 3); // plateau de 4 places : 3 unités en plus au plus
        Fill(list.GetArrayElementAtIndex(1), "test", "Test", 180, 300, 50, 25);
        Fill(list.GetArrayElementAtIndex(2), "pack", "Emballage", 250, 400, 50, 25);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    static void Fill(SerializedProperty m, string id, string display, int speedCost, int addCost, int speedMax, int addMax)
    {
        m.FindPropertyRelative("id").stringValue = id;
        m.FindPropertyRelative("displayName").stringValue = display;
        m.FindPropertyRelative("speedBaseCost").intValue = speedCost;
        m.FindPropertyRelative("addBaseCost").intValue = addCost;
        m.FindPropertyRelative("speedMax").intValue = speedMax;
        m.FindPropertyRelative("addMax").intValue = addMax;
    }

    static GameObject FindRoot(string objectName)
    {
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g.name == objectName && g.transform.parent == null) return g;
        return null;
    }

    static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (value is Object o) p.objectReferenceValue = o;
        else if (value is string s) p.stringValue = s;
        else if (value is int i) p.intValue = i;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
