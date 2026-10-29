using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Employés automatiques (même modèle humain que le technicien) et leurs dalles d'embauche, plus le NavMesh de la pièce
// (package officiel Unity AI Navigation) :
//  - 6a : le porteur de caisses (gilet bleu) : tas de cartons -> poste ;
//  - 6b : le livreur (gilet vert) : plateau de sortie -> comptoir des clients.
// Relançable.
public static class EmployeeBuilder
{
    const string Root = "Assets/Prototype";
    const float HumanHeight = 1.7f;

    // Dalle d'embauche du porteur : près du tas (au nord). Dalle du livreur : près du comptoir.
    static readonly Vector3 HaulerPadPosition = new Vector3(3.2f, 0.01f, 4.4f);
    static readonly Vector3 HaulerSpawn = new Vector3(2.2f, 0f, 2.6f);
    static readonly Vector3 CourierPadPosition = new Vector3(1.0f, 0.01f, -3.9f);
    static readonly Vector3 CourierSpawn = new Vector3(-0.8f, 0f, -2.6f);

    public static EmployeeConfig LoadOrCreateConfig()
    {
        return LoadOrCreateConfig("EmployeeConfig", 6, 150);
    }

    // Une config par type d'employé (conservée si elle existe : les réglages modifiés ne sont pas écrasés).
    static EmployeeConfig LoadOrCreateConfig(string file, int capacity, int hirePrice)
    {
        string path = Root + "/" + file + ".asset";
        EmployeeConfig config = AssetDatabase.LoadAssetAtPath<EmployeeConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<EmployeeConfig>();
            AssetDatabase.CreateAsset(config, path);
            var so = new SerializedObject(config);
            so.FindProperty("capacity").intValue = capacity;
            so.FindProperty("hirePrice").intValue = hirePrice;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return config;
    }

    public static void Build()
    {
        GameObject equipped = HumanCharacterBuilder.EnsurePrefab();
        if (equipped == null) return;

        ResourcePile pile = Object.FindFirstObjectByType<ResourcePile>();
        GameObject station = GameObject.Find("InstallStation");
        if (pile == null || station == null) return;

        foreach (string old in new[] { "Employee", "Courier", "EmployeeStands", "HirePad", "HirePadCourier" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }

        UnlockPad haulerPad = BuildHauler(equipped, LoadOrCreateConfig(), pile, station);
        UnlockPad courierPad = BuildCourier(equipped, LoadOrCreateConfig("CourierConfig", 4, 250), station);
        AddPlayerObstacle();

        // Les dalles apparaissent l'une après l'autre : poste 2 -> embauche du porteur -> embauche du livreur.
        UnlockPad station2Pad = Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(p => p.name == "UnlockPad");
        SetReveal(station2Pad, haulerPad);
        SetReveal(haulerPad, courierPad);
    }

    static void SetReveal(UnlockPad from, UnlockPad next)
    {
        if (from == null || next == null) return;
        var so = new SerializedObject(from);
        SerializedProperty list = so.FindProperty("revealOnUnlock");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = next.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ porteur de caisses (6a)

    static UnlockPad BuildHauler(GameObject equipped, EmployeeConfig config, ResourcePile pile, GameObject station)
    {
        // Points où l'employé se tient : à côté du tas (dans sa zone) et au poste, de côté pour laisser le centre au joueur
        Transform stands = new GameObject("EmployeeStands").transform;
        Transform pileStand = Marker(stands, "PileStand", pile.transform.position + new Vector3(-2.0f, 0f, 0f));
        Transform pileLook = Marker(stands, "PileLook", pile.transform.position);
        Transform stationStand = Marker(stands, "StationStand", station.transform.position + new Vector3(0.9f, 0f, -0.7f));
        Transform stationLook = Marker(stands, "StationLook", station.transform.position);

        GameObject root = new GameObject("Employee");
        root.transform.position = HaulerSpawn;
        GameObject model = BuildModel(root.transform, equipped, new Color(0.15f, 0.40f, 0.95f), "Emp_Vest"); // gilet bleu
        NavMeshAgent agent = AddAgentBody(root, config, 40);

        Transform anchor = new GameObject("StackAnchor").transform;
        anchor.SetParent(root.transform, false);
        anchor.localPosition = new Vector3(0f, 0.88f, -0.3f); // pile dans le dos, à hauteur de hanches, comme le joueur

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

        AddHumanAnimator(model, agent);
        root.SetActive(false); // la dalle d'embauche l'active

        return PrototypeSetup.BuildUnlockPad(root, model, HaulerPadPosition, "HirePad", "HireConfig", "EMBAUCHE", config.HirePrice, true);
    }

    // ------------------------------------------------------------------ livreur (6b)

    static UnlockPad BuildCourier(GameObject equipped, EmployeeConfig config, GameObject station)
    {
        Transform tray = station.transform.Find("OutputTray");
        GameObject counterGo = GameObject.Find("ServiceCounter");
        GameObject binGo = GameObject.Find("TrashBin");
        CustomerQueue queue = Object.FindFirstObjectByType<CustomerQueue>();
        if (tray == null || counterGo == null || binGo == null || queue == null) return null; // pas de clients : pas de livreur

        Transform stands = new GameObject("CourierStands").transform;
        stands.SetParent(GameObject.Find("EmployeeStands").transform, false);
        // plateau : à l'ouest du poste ; comptoir : sur sa gauche (le joueur se tient au centre) ; corbeille : à sa gauche
        Transform trayStand = Marker(stands, "TrayStand", tray.position + new Vector3(-0.5f, 0f, 0f));
        Transform trayLook = Marker(stands, "TrayLook", tray.position);
        Transform counterStand = Marker(stands, "CounterStand", counterGo.transform.position + new Vector3(-1.1f, 0f, 1.7f));
        Transform counterLook = Marker(stands, "CounterLook", counterGo.transform.position);
        Transform binStand = Marker(stands, "BinStand", binGo.transform.position + new Vector3(-0.1f, 0f, 0.6f));

        GameObject root = new GameObject("Courier");
        root.transform.position = CourierSpawn;
        GameObject model = BuildModel(root.transform, equipped, new Color(0.20f, 0.70f, 0.30f), "Emp2_Vest"); // gilet vert
        NavMeshAgent agent = AddAgentBody(root, config, 50);

        // Produits portés à la main, devant le torse, comme le joueur
        Transform anchor = new GameObject("CarryAnchor").transform;
        anchor.SetParent(root.transform, false);
        anchor.localPosition = new Vector3(0f, 0.9f, 0.5f);
        var carrier = root.AddComponent<ProductCarrier>();
        var pso = new SerializedObject(carrier);
        pso.FindProperty("carryAnchor").objectReferenceValue = anchor;
        pso.FindProperty("capacity").intValue = config.Capacity;
        pso.ApplyModifiedPropertiesWithoutUndo();

        var courier = root.AddComponent<Courier>();
        var cso = new SerializedObject(courier);
        cso.FindProperty("config").objectReferenceValue = config;
        cso.FindProperty("station").objectReferenceValue = station.GetComponent<InstallStation>();
        cso.FindProperty("queue").objectReferenceValue = queue;
        cso.FindProperty("trayStand").objectReferenceValue = trayStand;
        cso.FindProperty("trayLook").objectReferenceValue = trayLook;
        cso.FindProperty("counterStand").objectReferenceValue = counterStand;
        cso.FindProperty("counterLook").objectReferenceValue = counterLook;
        cso.FindProperty("binStand").objectReferenceValue = binStand;
        cso.ApplyModifiedPropertiesWithoutUndo();

        AddHumanAnimator(model, agent);
        root.SetActive(false);

        return PrototypeSetup.BuildUnlockPad(root, model, CourierPadPosition, "HirePadCourier", "HireCourierConfig", "LIVREUR", config.HirePrice, true);
    }

    // ------------------------------------------------------------------ éléments communs

    // Corps de l'agent : capsule non trigger (les zones du tas, du poste, du comptoir détectent ce collider),
    // Rigidbody cinématique, NavMeshAgent, exclu de la cuisson du NavMesh.
    static NavMeshAgent AddAgentBody(GameObject root, EmployeeConfig config, int avoidancePriority)
    {
        var capsule = root.AddComponent<CapsuleCollider>();
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
        agent.avoidancePriority = avoidancePriority; // priorités différentes : deux employés ne se traversent pas

        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        return agent;
    }

    static void AddHumanAnimator(GameObject model, NavMeshAgent agent)
    {
        var hum = model.AddComponent<HumanAnimator>();
        var hso = new SerializedObject(hum);
        hso.FindProperty("animator").objectReferenceValue = model.GetComponent<Animator>();
        hso.FindProperty("agent").objectReferenceValue = agent;
        hso.ApplyModifiedPropertiesWithoutUndo();
    }

    // Même modèle humain que le technicien ; gilet de couleur et casque blanc pour le distinguer du joueur et des clients.
    static GameObject BuildModel(Transform parent, GameObject equipped, Color vestColor, string vestMaterial)
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

        Material vest = HumanCharacterBuilder.Mat(vestMaterial, vestColor, 0.25f);
        Material helmet = HumanCharacterBuilder.Mat("Emp_Helmet", new Color(0.95f, 0.95f, 0.95f), 0.55f);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("vest")) r.sharedMaterial = vest;
            else if (n.Contains("helmet")) r.sharedMaterial = helmet; // coque et arête
        }
        return model;
    }

    // Le joueur est un obstacle mobile sans découpe : les employés le contournent au lieu de le traverser.
    static void AddPlayerObstacle()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        var obstacle = player.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = player.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.carving = false;      // évitement local seulement, le NavMesh n'est pas découpé
        obstacle.radius = 0.4f;
        obstacle.height = HumanHeight;
        obstacle.center = Vector3.zero;
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
    // Le poste 2 est activé le temps de la cuisson pour que les employés puissent le servir plus tard.
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
        foreach (TextMesh text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mod = text.GetComponent<NavMeshModifier>();
            if (mod == null) mod = text.gameObject.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
        }

        InstallStation station2 = Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(s => s.name == "InstallStation2");
        bool wasActive = station2 != null && station2.gameObject.activeSelf;
        if (station2 != null) station2.gameObject.SetActive(true);

        GameObject go = new GameObject("NavMesh");
        var surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~0;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.08f; // précision verticale ~4 cm
        surface.BuildNavMesh();

        if (station2 != null) station2.gameObject.SetActive(wasActive);

        if (surface.navMeshData != null)
        {
            string path = Root + "/NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path); // sauvé avec le projet, pas seulement en mémoire
        }
        EditorUtility.SetDirty(surface);
    }
}
