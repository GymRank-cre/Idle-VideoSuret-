using UnityEditor;
using UnityEngine;

// Chaîne de production (11a-2) : machines Test et Emballage, leurs dalles de déblocage, leurs réglages (MachineConfig)
// et le branchement de la valeur par stade sur le comptoir. Appelé par PrototypeSetup après EmployeeBuilder.
// Test et Emballage sont posés le long du mur du fond (z = 9), hors de la bande de déplacement du joueur.
public static class ChainBuilder
{
    const string Root = "Assets/Prototype";
    static readonly Vector3 TestPosition = new Vector3(-1.5f, 0.1f, 9f);
    static readonly Vector3 PackPosition = new Vector3(3f, 0.1f, 9f);

    public static void Build()
    {
        foreach (string old in new[] { "TestMachine", "PackMachine", "TestPad", "PackPad", "Belt1", "Belt2", "BeltPad1", "BeltPad2" })
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == old && go.transform.parent == null) Object.DestroyImmediate(go);

        // multiplicateurs de valeur : x1 assemblé, puis x1,6 testé et x2,4 emballé (défaut, à ajuster dans les assets)
        MachineConfig testConfig = LoadOrCreate("TestMachineConfig", "Test", 2.5f, 8, 4, 1.6f);
        MachineConfig packConfig = LoadOrCreate("PackMachineConfig", "Emballage", 3f, 8, 4, 2.4f);

        GameObject test = BuildMachine("TestMachine", TestPosition, testConfig, 0, new Color(0.12f, 0.5f, 0.85f));
        GameObject pack = BuildMachine("PackMachine", PackPosition, packConfig, 1, new Color(0.95f, 0.55f, 0.12f));

        UnlockPad testPad = PrototypeSetup.BuildUnlockPad(test, test, Flat(TestPosition), "TestPad", "TestPadConfig", "TEST", 400, true);
        UnlockPad packPad = PrototypeSetup.BuildUnlockPad(pack, pack, Flat(PackPosition), "PackPad", "PackPadConfig", "EMBALLAGE", 900, true);
        test.SetActive(false);
        pack.SetActive(false);

        // tapis (11a-3) : Assemblage -> Test et Test -> Emballage, chacun débloqué par sa dalle
        ProcessStation testStation = test.GetComponent<ProcessStation>();
        ProcessStation packStation = pack.GetComponent<ProcessStation>();
        InstallStation assembly = FindAssembly();
        GameObject belt1 = BuildBelt("Belt1", assembly, testStation, new[]
        {
            new Vector3(-6.6f, 0f, 0f), new Vector3(-7.3f, 0f, 0f), new Vector3(-7.3f, 0f, 9.4f), new Vector3(-2.7f, 0f, 9.4f)
        });
        GameObject belt2 = BuildBelt("Belt2", testStation, packStation, new[]
        {
            new Vector3(-0.7f, 0f, 6.8f), new Vector3(0.75f, 0f, 6.8f), new Vector3(0.75f, 0f, 8.4f), new Vector3(1.85f, 0f, 8.4f)
        });
        UnlockPad beltPad1 = PrototypeSetup.BuildUnlockPad(belt1, belt1.transform.Find("Pop").gameObject, new Vector3(-1.5f, 0.01f, 4f), "BeltPad1", "BeltPad1Config", "TAPIS", 600, true);
        UnlockPad beltPad2 = PrototypeSetup.BuildUnlockPad(belt2, belt2.transform.Find("Pop").gameObject, new Vector3(7f, 0.01f, 7f), "BeltPad2", "BeltPad2Config", "TAPIS", 1200, true);
        belt1.SetActive(false);
        belt2.SetActive(false);

        // vue d'ensemble : le centre de l'atelier, actif seulement une fois la machine Test débloquée
        var focus = new GameObject("OverviewFocus").transform;
        focus.SetParent(test.transform, false);
        focus.position = new Vector3(0.5f, 0f, 4.5f);
        CameraFollow cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            var cso = new SerializedObject(cameraFollow);
            cso.FindProperty("overviewFocus").objectReferenceValue = focus;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // enchaînement des dalles : ... -> livreur -> Test -> (Emballage, Tapis 1) ; Emballage -> Tapis 2
        Reveal(FindPad("HirePadCourier"), testPad);
        Reveal(testPad, packPad);
        Reveal(testPad, beltPad1);
        Reveal(packPad, beltPad2);

        ServiceCounter counter = Object.FindFirstObjectByType<ServiceCounter>(FindObjectsInactive.Include);
        if (counter != null)
        {
            var so = new SerializedObject(counter);
            SerializedProperty list = so.FindProperty("stageMachines");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = testConfig;
            list.GetArrayElementAtIndex(1).objectReferenceValue = packConfig;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static InstallStation FindAssembly()
    {
        foreach (InstallStation s in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.name == "InstallStation") return s;
        return null;
    }

    // Tapis : bande sombre à rails jaunes le long des points, taquets qui défilent, composant Conveyor.
    // Créé désactivé (la dalle l'active) ; « Pop » est le petit repère que la dalle fait gonfler.
    static GameObject BuildBelt(string beltName, MonoBehaviour source, ProcessStation destination, Vector3[] points)
    {
        const float width = 0.6f;
        var root = new GameObject(beltName);
        Material bed = PrototypeSetup.MakeMaterial("BeltBed", new Color(0.17f, 0.2f, 0.25f));
        Material rail = PrototypeSetup.MakeMaterial("BeltRail", new Color32(0xFF, 0xC4, 0x00, 0xFF));
        Material cleatMat = PrototypeSetup.MakeMaterial("BeltCleat", new Color32(0x3A, 0x45, 0x55, 0xFF));

        var pop = new GameObject("Pop").transform;
        pop.SetParent(root.transform, false);
        pop.position = points[points.Length / 2];

        var waypoints = new Transform[points.Length];
        float length = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            var wp = new GameObject("WP" + i).transform;
            wp.SetParent(root.transform, false);
            wp.position = points[i];
            waypoints[i] = wp;
            if (i > 0) length += Vector3.Distance(points[i - 1], points[i]);
        }

        for (int i = 1; i < points.Length; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            Vector3 dir = (b - a).normalized;
            float seg = Vector3.Distance(a, b);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 mid = (a + b) * 0.5f;
            Box(root.transform, "Bed" + i, mid + Vector3.up * 0.09f, rot, new Vector3(width, 0.12f, seg + width), bed);
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (width * 0.5f);
            Box(root.transform, "RailA" + i, mid + Vector3.up * 0.17f + side, rot, new Vector3(0.05f, 0.1f, seg + width), rail);
            Box(root.transform, "RailB" + i, mid + Vector3.up * 0.17f - side, rot, new Vector3(0.05f, 0.1f, seg + width), rail);
        }

        const float cleatSpacing = 0.5f;
        int count = Mathf.CeilToInt(length / cleatSpacing) + 1;
        var cleats = new Transform[count];
        for (int i = 0; i < count; i++)
            cleats[i] = Box(root.transform, "Cleat" + i, points[0], Quaternion.identity, new Vector3(width * 0.85f, 0.03f, 0.08f), cleatMat);

        var conveyor = root.AddComponent<Conveyor>();
        var so = new SerializedObject(conveyor);
        so.FindProperty("source").objectReferenceValue = source;
        so.FindProperty("destination").objectReferenceValue = destination;
        so.FindProperty("cleatSpacing").floatValue = cleatSpacing;
        SerializedProperty wps = so.FindProperty("waypoints");
        wps.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++) wps.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        SerializedProperty cl = so.FindProperty("cleats");
        cl.arraySize = cleats.Length;
        for (int i = 0; i < cleats.Length; i++) cl.GetArrayElementAtIndex(i).objectReferenceValue = cleats[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    static Transform Box(Transform parent, string boxName, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = boxName;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Vector3 Flat(Vector3 p) { return new Vector3(p.x, 0.01f, p.z); }

    static UnlockPad FindPad(string padName)
    {
        foreach (UnlockPad pad in Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pad.name == padName) return pad;
        return null;
    }

    // Ajoute `next` à la liste des dalles que `from` dévoile (sans écraser ce qu'elle dévoile déjà).
    static void Reveal(UnlockPad from, UnlockPad next)
    {
        if (from == null || next == null) return;
        var so = new SerializedObject(from);
        SerializedProperty list = so.FindProperty("revealOnUnlock");
        int n = list.arraySize;
        list.arraySize = n + 1;
        list.GetArrayElementAtIndex(n).objectReferenceValue = next.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static MachineConfig LoadOrCreate(string assetName, string displayName, float cycle, int inCap, int outCap, float multiplier)
    {
        string path = Root + "/" + assetName + ".asset";
        MachineConfig config = AssetDatabase.LoadAssetAtPath<MachineConfig>(path);
        if (config != null) return config;

        config = ScriptableObject.CreateInstance<MachineConfig>();
        AssetDatabase.CreateAsset(config, path);
        var so = new SerializedObject(config);
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("cycleSeconds").floatValue = cycle;
        so.FindProperty("inputCapacity").intValue = inCap;
        so.FindProperty("outputCapacity").intValue = outCap;
        so.FindProperty("valueMultiplier").floatValue = multiplier;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    // Machine en formes simples : corps coloré, plateau blanc, petit écran ; zone de dépôt, plateau de sortie au sud,
    // voyant et stocks au-dessus. Créée désactivée : la dalle la fait apparaître.
    static GameObject BuildMachine(string machineName, Vector3 position, MachineConfig config, int inputStage, Color bodyColor)
    {
        var root = new GameObject(machineName);
        root.transform.position = position;

        var zone = root.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.size = new Vector3(3f, 10f, 3f); // assez haute pour englober le joueur
        zone.center = new Vector3(0f, 5f, 0f);

        Part(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(2.3f, 1.1f, 1.5f), PrototypeSetup.MakeMaterial(machineName + "Body", bodyColor));
        Part(root.transform, "Top", PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(2.4f, 0.12f, 1.6f), PrototypeSetup.MakeMaterial("MachineTop", new Color(0.93f, 0.94f, 0.96f)));
        Part(root.transform, "Screen", PrimitiveType.Cube, new Vector3(0f, 0.8f, -0.77f), new Vector3(0.9f, 0.45f, 0.05f), PrototypeSetup.MakeMaterial("MachineScreen", new Color(0.08f, 0.12f, 0.18f)));
        Part(root.transform, "Hopper", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0f), new Vector3(1.0f, 0.3f, 0.8f), PrototypeSetup.MakeMaterial("MachineHopper", new Color(0.55f, 0.6f, 0.68f)));

        var machine = root.AddComponent<ProductionMachine>();
        var mso = new SerializedObject(machine);
        mso.FindProperty("config").objectReferenceValue = config;
        mso.ApplyModifiedPropertiesWithoutUndo();

        Transform tray = ProductBuilder.BuildTray(root.transform);
        tray.localPosition = new Vector3(0f, -0.1f, -2.2f); // au sud, juste hors de la zone de dépôt
        tray.localRotation = Quaternion.identity;

        var station = root.AddComponent<ProcessStation>();
        var sso = new SerializedObject(station);
        sso.FindProperty("machine").objectReferenceValue = machine;
        sso.FindProperty("inputStage").intValue = inputStage;
        sso.FindProperty("outputTray").objectReferenceValue = tray;
        sso.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        sso.ApplyModifiedPropertiesWithoutUndo();

        MachineBuilder.AddStockHud(root.transform, machine, 2.9f);
        return root;
    }

    static void Part(Transform parent, string partName, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
