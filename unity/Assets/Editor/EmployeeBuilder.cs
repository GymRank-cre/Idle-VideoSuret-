using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Étape 6a : dalle d'embauche et premier employé automatique (même modèle humain, gilet bleu, casque blanc),
// et NavMesh de la pièce (package officiel Unity AI Navigation). Relançable.
public static class EmployeeBuilder
{
    const string Root = "Assets/Prototype";
    const float HumanHeight = 1.7f;

    // La dalle d'embauche est près du tas (au nord), l'employé apparaît à côté.
    static readonly Vector3 HirePadPosition = new Vector3(4.5f, 0.01f, 4.4f);
    static readonly Vector3 EmployeeSpawn = new Vector3(2.2f, 0f, 2.6f);

    public static EmployeeConfig LoadOrCreateConfig()
    {
        string path = Root + "/EmployeeConfig.asset";
        EmployeeConfig config = AssetDatabase.LoadAssetAtPath<EmployeeConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<EmployeeConfig>();
            AssetDatabase.CreateAsset(config, path); // conservé s'il existe
        }
        return config;
    }

    public static void Build()
    {
        GameObject equipped = HumanCharacterBuilder.EnsurePrefab();
        if (equipped == null) return;

        EmployeeConfig config = LoadOrCreateConfig();
        ResourcePile pile = Object.FindFirstObjectByType<ResourcePile>();
        GameObject station = GameObject.Find("InstallStation");
        if (pile == null || station == null) return;

        foreach (string old in new[] { "Employee", "EmployeeStands", "HirePad" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }

        // Points où l'employé se tient : à côté du tas (dans sa zone) et au poste, de côté pour laisser le centre au joueur
        Transform stands = new GameObject("EmployeeStands").transform;
        Transform pileStand = Marker(stands, "PileStand", pile.transform.position + new Vector3(-2.0f, 0f, 0f));
        Transform pileLook = Marker(stands, "PileLook", pile.transform.position);
        Transform stationStand = Marker(stands, "StationStand", station.transform.position + new Vector3(0.9f, 0f, -0.7f));
        Transform stationLook = Marker(stands, "StationLook", station.transform.position);

        // --- l'employé (désactivé : la dalle d'embauche l'active)
        GameObject root = new GameObject("Employee");
        root.transform.position = EmployeeSpawn;

        GameObject model = BuildModel(root.transform, equipped);

        Transform anchor = new GameObject("StackAnchor").transform;
        anchor.SetParent(root.transform, false);
        anchor.localPosition = new Vector3(0f, 0.88f, -0.3f); // pile dans le dos, à hauteur de hanches, comme le joueur

        var capsule = root.AddComponent<CapsuleCollider>(); // non trigger : c'est lui que les zones du tas et du poste détectent
        capsule.center = new Vector3(0f, 0.85f, 0f);
        capsule.radius = 0.3f;
        capsule.height = HumanHeight;
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var agent = root.AddComponent<NavMeshAgent>();
        agent.speed = config.MoveSpeed;
        agent.angularSpeed = 540f;
        agent.acceleration = 14f;
        agent.stoppingDistance = 0.1f;
        agent.radius = 0.4f;
        agent.height = HumanHeight;
        agent.avoidancePriority = 40;

        var carrier = root.AddComponent<StackCarrier>();
        var cso = new SerializedObject(carrier);
        cso.FindProperty("stackAnchor").objectReferenceValue = anchor;
        cso.FindProperty("capacity").intValue = config.Capacity;
        cso.FindProperty("itemHeight").floatValue = 0.17f;
        cso.ApplyModifiedPropertiesWithoutUndo();

        var employee = root.AddComponent<Employee>();
        var eso = new SerializedObject(employee);
        eso.FindProperty("config").objectReferenceValue = config;
        eso.FindProperty("pileStand").objectReferenceValue = pileStand;
        eso.FindProperty("pileLookAt").objectReferenceValue = pileLook;
        eso.FindProperty("stationStand").objectReferenceValue = stationStand;
        eso.FindProperty("stationLookAt").objectReferenceValue = stationLook;
        eso.ApplyModifiedPropertiesWithoutUndo();

        var hum = model.AddComponent<HumanAnimator>();
        var hso = new SerializedObject(hum);
        hso.FindProperty("animator").objectReferenceValue = model.GetComponent<Animator>();
        hso.FindProperty("agent").objectReferenceValue = agent;
        hso.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // le NavMesh ne se cuit pas sur l'employé lui-même
        root.SetActive(false);

        // --- dalle d'embauche : même UnlockPad que le 2e poste, prix venant de l'EmployeeConfig
        UnlockPad pad = PrototypeSetup.BuildUnlockPad(root, model, HirePadPosition, "HirePad", "HireConfig", "EMBAUCHE");
        var hireConfig = AssetDatabase.LoadAssetAtPath<UnlockConfig>(Root + "/HireConfig.asset");
        var pso = new SerializedObject(hireConfig);
        pso.FindProperty("price").intValue = config.HirePrice;
        pso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hireConfig);
    }

    // Même modèle humain que le technicien ; gilet bleu et casque blanc pour le distinguer du joueur et des clients.
    static GameObject BuildModel(Transform parent, GameObject equipped)
    {
        var model = (GameObject)PrefabUtility.InstantiatePrefab(equipped, parent);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Bounds b = new Bounds();
        bool first = true;
        foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        model.transform.localScale = Vector3.one * (HumanHeight / Mathf.Max(0.1f, b.size.y));

        Material vest = HumanCharacterBuilder.Mat("Emp_Vest", new Color(0.15f, 0.40f, 0.95f), 0.25f);
        Material helmet = HumanCharacterBuilder.Mat("Emp_Helmet", new Color(0.95f, 0.95f, 0.95f), 0.55f);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("vest")) r.sharedMaterial = vest;
            else if (n.Contains("helmet")) r.sharedMaterial = helmet; // coque et arête
        }
        return model;
    }

    static Transform Marker(Transform parent, string name, Vector3 worldPos)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = worldPos;
        return t;
    }

    // ------------------------------------------------------------------ NavMesh

    // Cuit le NavMesh sur le sol de la pièce : établis, comptoir, étagères et murs sont des obstacles.
    public static void BakeNavMesh()
    {
        GameObject old = GameObject.Find("NavMesh");
        if (old != null) Object.DestroyImmediate(old);

        // Le joueur n'est pas du décor : on l'exclut de la cuisson.
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var mod = player.GetComponent<NavMeshModifier>();
            if (mod == null) mod = player.gameObject.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
        }

        // Les textes 3D (enseigne, prix, titres) n'ont pas de vrai maillage : on les exclut aussi.
        foreach (TextMesh text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            var mod = text.GetComponent<NavMeshModifier>();
            if (mod == null) mod = text.gameObject.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
        }

        GameObject go = new GameObject("NavMesh");
        var surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~0;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.08f; // précision verticale ~4 cm (par défaut ~10 cm : l'employé flottait)
        surface.BuildNavMesh();

        if (surface.navMeshData != null)
        {
            string path = Root + "/NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path); // sauvé avec le projet, pas seulement en mémoire
        }
        EditorUtility.SetDirty(surface);
    }
}
