using UnityEditor;
using UnityEngine;

// Chaîne de production (11a-2) : machines Test et Emballage, leurs dalles de déblocage, leurs réglages (MachineConfig)
// et le branchement de la valeur par stade sur le comptoir. Appelé par PrototypeSetup après EmployeeBuilder.
// Test et Emballage sont posés au nord de la bande de déplacement du joueur (z >= 3,5) ; trajets de tapis courts.
public static class ChainBuilder
{
    const string Root = "Assets/Prototype";
    static readonly Vector3 TestPosition = new Vector3(-1.5f, 0.1f, 5f);
    static readonly Vector3 PackPosition = new Vector3(6.5f, 0.1f, 7f);

    public static void Build()
    {
        foreach (string old in new[] { "TestMachine", "PackMachine", "TestPad", "PackPad", "Belt1", "Belt2", "BeltPad1", "BeltPad2" })
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == old && go.transform.parent == null) Object.DestroyImmediate(go);

        // multiplicateurs de valeur : x1 assemblé, puis x1,6 testé et x2,4 emballé (défaut, à ajuster dans les assets)
        MachineConfig testConfig = LoadOrCreate("TestMachineConfig", "Test", 2.5f, 8, 4, 1.6f);
        MachineConfig packConfig = LoadOrCreate("PackMachineConfig", "Emballage", 3f, 8, 4, 2.4f);

        GameObject test = BuildMachine("TestMachine", TestPosition, testConfig, 0, new Color(0.12f, 0.5f, 0.85f), 2.2f);
        GameObject pack = BuildMachine("PackMachine", PackPosition, packConfig, 1, new Color(0.95f, 0.5f, 0.1f), -2.2f);

        UnlockPad testPad = PrototypeSetup.BuildUnlockPad(test, test, Flat(TestPosition), "TestPad", "TestPadConfig", Loc.T("pad.test"), 400, true);
        UnlockPad packPad = PrototypeSetup.BuildUnlockPad(pack, pack, Flat(PackPosition), "PackPad", "PackPadConfig", Loc.T("pad.pack"), 500, true);
        test.SetActive(false);
        pack.SetActive(false);

        // tapis (11a-3) : Assemblage -> Test et Test -> Emballage, chacun débloqué par sa dalle
        ProcessStation testStation = test.GetComponent<ProcessStation>();
        ProcessStation packStation = pack.GetComponent<ProcessStation>();
        InstallStation assembly = FindAssembly();
        GameObject belt1 = BuildBelt("Belt1", assembly, testStation, new[]
        {
            new Vector3(-6.6f, 0f, 0f), new Vector3(-6.6f, 0f, 5f), new Vector3(-2.7f, 0f, 5f)
        });
        GameObject belt2 = BuildBelt("Belt2", testStation, packStation, new[]
        {
            new Vector3(-0.7f, 0f, 7.2f), new Vector3(5.3f, 0f, 7.2f)
        });
        UnlockPad beltPad1 = PrototypeSetup.BuildUnlockPad(belt1, belt1.transform.Find("Pop").gameObject, new Vector3(-1.5f, 0.01f, 9.3f), "BeltPad1", "BeltPad1Config", Loc.T("pad.belt"), 700, true);
        UnlockPad beltPad2 = PrototypeSetup.BuildUnlockPad(belt2, belt2.transform.Find("Pop").gameObject, new Vector3(2.5f, 0.01f, 9.5f), "BeltPad2", "BeltPad2Config", Loc.T("pad.belt"), 1500, true);
        belt1.SetActive(false);
        belt2.SetActive(false);

        // type des produits restaurés depuis la sauvegarde ; livreur : plateau d'Emballage quand les deux tapis sont achetés
        StationConfig stationConfig = AssetDatabase.LoadAssetAtPath<StationConfig>(Root + "/StationConfig.asset");
        ProductType fallback = stationConfig != null && stationConfig.Products != null && stationConfig.Products.Length > 0 ? stationConfig.Products[0] : null;
        foreach (ProcessStation st in new[] { testStation, packStation })
        {
            var fso = new SerializedObject(st);
            fso.FindProperty("fallbackType").objectReferenceValue = fallback;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }
        WireCourier(packStation, belt1, belt2);

        // vue d'ensemble : le centre de l'atelier, actif seulement une fois la machine Test débloquée
        var focus = new GameObject("OverviewFocus").transform;
        focus.SetParent(test.transform, false);
        focus.position = new Vector3(1.5f, 0f, 3.5f);
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

    static void WireCourier(ProcessStation chainEnd, GameObject belt1, GameObject belt2)
    {
        Courier courier = Object.FindFirstObjectByType<Courier>(FindObjectsInactive.Include);
        Transform stands = GameObject.Find("CourierStands") != null ? GameObject.Find("CourierStands").transform : null;
        if (courier == null || stands == null) return;
        Transform tray = chainEnd.transform.Find("OutputTray");
        foreach (string old in new[] { "ChainStand", "ChainLook" })
        {
            Transform t = stands.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        var stand = new GameObject("ChainStand").transform;
        stand.SetParent(stands, false);
        stand.position = tray.position + new Vector3(0f, 0f, -0.9f); // au sud du plateau d'Emballage
        var look = new GameObject("ChainLook").transform;
        look.SetParent(stands, false);
        look.position = tray.position;

        var so = new SerializedObject(courier);
        so.FindProperty("chainEnd").objectReferenceValue = chainEnd;
        so.FindProperty("chainBelt1").objectReferenceValue = belt1;
        so.FindProperty("chainBelt2").objectReferenceValue = belt2;
        so.FindProperty("chainStand").objectReferenceValue = stand;
        so.FindProperty("chainLook").objectReferenceValue = look;
        so.ApplyModifiedPropertiesWithoutUndo();
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
        Material cleatMat = PrototypeSetup.MakeMaterial("BeltCleatYellow", new Color32(0xE0, 0xA8, 0x10, 0xFF));

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

    // Machine : corps arrondi bleu/blanc (Test) ou orange/blanc (Emballage), lampe cylindrique sur le dessus, petit écran de face,
    // fente d'entrée côté tapis (ouest), zone de dépôt, plateau de sortie (`trayZ`, au nord ou au sud), pastille de stocks.
    // Créée désactivée : la dalle la fait apparaître.
    static GameObject BuildMachine(string machineName, Vector3 position, MachineConfig config, int inputStage, Color bodyColor, float trayZ)
    {
        var root = new GameObject(machineName);
        root.transform.position = position;

        var zone = root.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.size = new Vector3(3f, 10f, 3f); // assez haute pour englober le joueur
        zone.center = new Vector3(0f, 5f, 0f);

        Material lower = PrototypeSetup.MakeMaterial(machineName + "Body", bodyColor);
        Material upper = PrototypeSetup.MakeMaterial("MachineWhite", new Color(0.93f, 0.94f, 0.96f));
        Material dark = PrototypeSetup.MakeMaterial("MachineScreen", new Color(0.08f, 0.12f, 0.18f));
        Material steel = PrototypeSetup.MakeMaterial("MachineHopper", new Color(0.55f, 0.6f, 0.68f));

        var group = new GameObject("BodyGroup").transform; // tout ce qui vibre quand la machine travaille
        group.SetParent(root.transform, false);
        RoundedBox.Make(group, "Lower", new Vector3(0f, 0.6f, 0f), new Vector3(2.1f, 1.2f, 1.5f), 0.16f, lower);
        RoundedBox.Make(group, "Upper", new Vector3(0f, 1.45f, 0f), new Vector3(2.15f, 0.7f, 1.55f), 0.2f, upper);
        RoundedBox.Make(group, "Screen", new Vector3(0f, 1.45f, -0.78f), new Vector3(0.95f, 0.45f, 0.1f), 0.04f, dark);
        RoundedBox.Make(group, "Slot", new Vector3(-1.06f, 0.55f, 0f), new Vector3(0.12f, 0.4f, 0.75f), 0.04f, dark);   // entrée, côté tapis
        RoundedBox.Make(group, "Panel", new Vector3(0f, 0.65f, -0.76f), new Vector3(1.3f, 0.5f, 0.06f), 0.03f, steel); // trappe de service
        Transform hopper = RoundedBox.Make(group, "Hopper", new Vector3(0.45f, 1.9f, 0.1f), new Vector3(0.6f, 0.25f, 0.5f), 0.06f, steel);
        // détails de machine : jambes, tuyau vertical et coude, rouleaux côté tapis
        foreach (Vector3 leg in new[] { new Vector3(-0.95f, 0.08f, -0.62f), new Vector3(0.95f, 0.08f, -0.62f), new Vector3(-0.95f, 0.08f, 0.62f), new Vector3(0.95f, 0.08f, 0.62f) })
            RoundedBox.Make(group, "Leg", leg, new Vector3(0.18f, 0.16f, 0.18f), 0.04f, dark);
        var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(pipe.GetComponent<Collider>());
        pipe.name = "Pipe";
        pipe.transform.SetParent(group, false);
        pipe.transform.localPosition = new Vector3(1.0f, 1.0f, 0.55f);
        pipe.transform.localScale = new Vector3(0.12f, 0.95f, 0.12f);
        pipe.GetComponent<Renderer>().sharedMaterial = steel;
        RoundedBox.Make(group, "Elbow", new Vector3(0.85f, 1.95f, 0.55f), new Vector3(0.4f, 0.12f, 0.12f), 0.04f, steel);
        for (int k = 0; k < 3; k++)
            RoundedBox.Make(group, "Roller", new Vector3(-1.12f, 0.35f + k * 0.18f, -0.28f + k * 0.28f), new Vector3(0.1f, 0.1f, 0.45f), 0.03f, steel);

        Renderer lamp = MachineBuilder.MakeLampHead(group, new Vector3(-0.55f, 1.9f, 0.1f));
        lamp.transform.localScale = new Vector3(0.34f, 0.2f, 0.34f);

        var machine = root.AddComponent<ProductionMachine>();
        var mso = new SerializedObject(machine);
        mso.FindProperty("config").objectReferenceValue = config;
        mso.ApplyModifiedPropertiesWithoutUndo();

        Transform tray = ProductBuilder.BuildTray(root.transform);
        tray.localPosition = new Vector3(0f, -0.1f, trayZ); // juste hors de la zone de dépôt
        tray.localRotation = Quaternion.identity;

        var station = root.AddComponent<ProcessStation>();
        var sso = new SerializedObject(station);
        sso.FindProperty("machine").objectReferenceValue = machine;
        sso.FindProperty("inputStage").intValue = inputStage;
        sso.FindProperty("outputTray").objectReferenceValue = tray;
        sso.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        sso.ApplyModifiedPropertiesWithoutUndo();

        var fx = root.AddComponent<MachineFx>();
        var fxo = new SerializedObject(fx);
        fxo.FindProperty("machine").objectReferenceValue = machine;
        fxo.FindProperty("body").objectReferenceValue = group;
        fxo.FindProperty("lamp").objectReferenceValue = lamp.transform;
        fxo.FindProperty("sparkPoint").objectReferenceValue = lamp.transform;
        fxo.FindProperty("piston").objectReferenceValue = hopper;
        fxo.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        fxo.ApplyModifiedPropertiesWithoutUndo();

        MachineBuilder.AddStockHud(root.transform, machine, 2.8f, lamp);
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
