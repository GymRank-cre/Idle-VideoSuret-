using UnityEditor;
using UnityEngine;

// Étape 5b : construit les clients (même modèle humain que le technicien, sans casque ni gilet), le comptoir
// avec sa caisse enregistreuse, la file d'attente, la corbeille et leurs réglages. Formes simples uniquement.
public static class CustomerBuilder
{
    const string Root = "Assets/Prototype";
    const string CustomerPrefabPath = "Assets/Characters/Customer.prefab";
    const float HumanHeight = 1.7f;

    // Positions : le comptoir est au sud de la palette, la file s'allonge vers le sud, la porte est dans le mur de droite.
    static readonly Vector3 CounterPosition = new Vector3(5f, 0f, -5.4f); // le joueur peut s'en approcher jusqu'à z = -4,4
    static readonly Vector3 BinPosition = new Vector3(7.6f, 0f, -3.6f);
    const float InLaneZ = -11.5f;   // couloir d'arrivée
    const float OutLaneZ = -10.3f;  // couloir de sortie (ne croise pas l'arrivée)

    static readonly Color[] ShirtColors =
    {
        new Color(0.25f, 0.45f, 0.80f), new Color(0.30f, 0.70f, 0.40f), new Color(0.60f, 0.35f, 0.70f),
        new Color(0.80f, 0.30f, 0.30f), new Color(0.90f, 0.90f, 0.88f), new Color(0.20f, 0.65f, 0.70f),
    };
    static readonly Color[] HairColors =
    {
        new Color(0.05f, 0.05f, 0.05f), new Color(0.20f, 0.12f, 0.07f), new Color(0.38f, 0.24f, 0.12f),
        new Color(0.55f, 0.25f, 0.12f), new Color(0.85f, 0.70f, 0.35f), new Color(0.65f, 0.65f, 0.65f),
    };

    public static CustomerConfig LoadOrCreateConfig()
    {
        string path = Root + "/CustomerConfig.asset";
        CustomerConfig config = AssetDatabase.LoadAssetAtPath<CustomerConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<CustomerConfig>();
            AssetDatabase.CreateAsset(config, path); // conservé s'il existe : les réglages ne sont pas écrasés
        }
        return config;
    }

    // Construit toute la partie « clients » de la scène ; renvoie la file (ou null si les FBX manquent).
    public static CustomerQueue Build(StationConfig stationConfig, GameObject billPrefab)
    {
        if (HumanCharacterBuilder.EnsurePrefab() == null) return null; // le technicien équipé sert de modèle de matériaux
        GameObject customerPrefab = BuildCustomerPrefab();
        if (customerPrefab == null) return null;

        CustomerConfig config = LoadOrCreateConfig();
        FeedbackSpawner feedback = Object.FindFirstObjectByType<FeedbackSpawner>();

        GameObject old = GameObject.Find("Customers");
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject("Customers").transform;

        CustomerQueue queue = BuildQueue(root, config, customerPrefab, stationConfig);
        BuildCounter(root, config, queue, billPrefab, feedback);
        BuildBin(root, feedback);
        return queue;
    }

    // ------------------------------------------------------------------ prefab client

    static GameObject BuildCustomerPrefab()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HumanCharacterBuilder.ModelPath);
        var equipped = AssetDatabase.LoadAssetAtPath<GameObject>(HumanCharacterBuilder.PrefabPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HumanCharacterBuilder.ControllerPath);
        if (fbx == null || equipped == null || controller == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "Customer";
        try
        {
            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller; // le même Animator que le technicien
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Matériaux : ceux du technicien, mais pas de casque ni de gilet.
            SkinnedMeshRenderer shirt = null, hair = null, torso = null;
            foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("helmet")) { r.gameObject.SetActive(false); continue; }
                if (n.Contains("vest")) torso = r; // le maillage « gilet » est en fait le torse du modèle : sans lui on voit à travers

                foreach (SkinnedMeshRenderer src in equipped.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (src.name == r.name) { r.sharedMaterials = src.sharedMaterials; break; }
                if (n.Contains("shirt")) shirt = r;
                if (n.Contains("hair")) hair = r;
            }

            // Échelle de 1,7 m (mesurée avant d'ajouter le visage)
            Bounds b = new Bounds();
            bool first = true;
            foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            float scale = HumanHeight / Mathf.Max(0.1f, b.size.y);

            HumanCharacterBuilder.AddFace(go, animator);
            if (hair != null) hair.gameObject.SetActive(false); // le maillage de cheveux du modèle est déchiqueté sans casque
            CustomerHair hairStyles = AddHair(go, animator);
            Renderer[] collar = torso != null ? new Renderer[] { torso } : new Renderer[0]; // le torse prend la couleur de la chemise
            go.transform.localScale = Vector3.one * scale;

            // Bulle de commande au-dessus de la tête (taille réelle, malgré l'échelle du parent)
            OrderBubble bubble = BuildBubble(go.transform, scale);

            var customer = go.AddComponent<Customer>();
            var cso = new SerializedObject(customer);
            cso.FindProperty("animator").objectReferenceValue = animator;
            cso.FindProperty("bubble").objectReferenceValue = bubble;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var look = go.AddComponent<CustomerAppearance>();
            var lso = new SerializedObject(look);
            lso.FindProperty("shirt").objectReferenceValue = shirt;
            lso.FindProperty("hair").objectReferenceValue = hair;
            SerializedProperty extras = lso.FindProperty("shirtExtras");
            extras.arraySize = collar.Length;
            for (int i = 0; i < collar.Length; i++) extras.GetArrayElementAtIndex(i).objectReferenceValue = collar[i];
            SetColors(lso.FindProperty("shirtColors"), ShirtColors);
            SetColors(lso.FindProperty("hairColors"), HairColors);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, CustomerPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
    }

    // Coiffure en formes simples posée sur le crâne (mesuré sur le maillage) : 3 formes, 4 couleurs.
    static CustomerHair AddHair(GameObject go, Animator animator)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        float top = HumanCharacterBuilder.TopOf(go, "body");
        HumanCharacterBuilder.SliceOf(go, "body", top - 0.06f, 0.03f, out Vector3 c, out float w, out float d);
        Material mat = HumanCharacterBuilder.Mat("Cust_Hair", new Color(0.2f, 0.12f, 0.07f), 0.3f);

        var styles = new GameObject[3];
        string[] names = { "HairShort", "HairMid", "HairSpiky" };
        for (int i = 0; i < 3; i++)
        {
            styles[i] = new GameObject(names[i]);
            styles[i].transform.SetParent(head, false);
        }
        Transform shortS = styles[0].transform, mid = styles[1].transform, spiky = styles[2].transform;

        // courte : calotte arrondie
        HumanCharacterBuilder.Part(PrimitiveType.Sphere, "Cap", shortS, new Vector3(c.x, top - 0.03f, c.z), new Vector3(w * 1.08f, 0.13f, d * 1.08f), mat);

        // mi-longue : calotte, retombée dans le dos et sur les côtés
        HumanCharacterBuilder.Part(PrimitiveType.Sphere, "Cap", mid, new Vector3(c.x, top - 0.03f, c.z), new Vector3(w * 1.1f, 0.14f, d * 1.1f), mat);
        HumanCharacterBuilder.Part(PrimitiveType.Sphere, "Back", mid, new Vector3(c.x, top - 0.115f, c.z - d * 0.36f), new Vector3(w * 1.12f, 0.2f, d * 0.45f), mat);
        foreach (float sx in new[] { -1f, 1f })
            HumanCharacterBuilder.Part(PrimitiveType.Sphere, "Side", mid, new Vector3(c.x + sx * w * 0.52f, top - 0.085f, c.z - 0.01f), new Vector3(0.04f, 0.13f, d * 0.6f), mat);

        // hérissée : calotte basse + petites pointes
        HumanCharacterBuilder.Part(PrimitiveType.Sphere, "Cap", spiky, new Vector3(c.x, top - 0.035f, c.z), new Vector3(w * 1.05f, 0.1f, d * 1.05f), mat);
        HumanCharacterBuilder.Part(PrimitiveType.Capsule, "Spike", spiky, new Vector3(c.x, top + 0.012f, c.z), new Vector3(0.024f, 0.05f, 0.024f), mat);
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f;
            Vector3 p = new Vector3(c.x + Mathf.Cos(a) * w * 0.3f, top - 0.002f, c.z + Mathf.Sin(a) * d * 0.3f);
            HumanCharacterBuilder.Part(PrimitiveType.Capsule, "Spike", spiky, p, new Vector3(0.022f, 0.042f, 0.022f), mat);
        }

        var hairComp = go.AddComponent<CustomerHair>();
        var so = new SerializedObject(hairComp);
        SerializedProperty list = so.FindProperty("styles");
        list.arraySize = 3;
        for (int i = 0; i < 3; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = styles[i];
        Color[] hairPalette =
        {
            new Color(0.06f, 0.05f, 0.05f), new Color(0.32f, 0.20f, 0.10f),
            new Color(0.85f, 0.70f, 0.35f), new Color(0.65f, 0.65f, 0.65f),
        };
        SerializedProperty cols = so.FindProperty("colors");
        cols.arraySize = hairPalette.Length;
        for (int i = 0; i < hairPalette.Length; i++) cols.GetArrayElementAtIndex(i).colorValue = hairPalette[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return hairComp;
    }

    static void SetColors(SerializedProperty array, Color[] colors)
    {
        array.arraySize = colors.Length;
        for (int i = 0; i < colors.Length; i++)
            array.GetArrayElementAtIndex(i).colorValue = colors[i];
    }

    static OrderBubble BuildBubble(Transform customer, float parentScale)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject("OrderBubble", typeof(FaceCamera));
        root.transform.SetParent(customer, false);
        root.transform.localPosition = new Vector3(0f, 2.4f / parentScale, 0f);
        root.transform.localScale = Vector3.one * (0.75f / parentScale); // pastille 25 % plus petite que sa première version
        var faceSo = new SerializedObject(root.GetComponent<FaceCamera>());
        faceSo.FindProperty("keepScreenSize").boolValue = true;   // même taille à l'écran quelle que soit la distance
        faceSo.FindProperty("referenceDistance").floatValue = 14.3f; // distance de la caméra de jeu
        faceSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject pill = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        pill.name = "Pill";
        pill.transform.SetParent(root.transform, false);
        pill.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        pill.transform.localScale = new Vector3(0.42f, 0.5f, 0.06f); // pastille de 1 m x 0,42 m
        pill.GetComponent<Renderer>().sharedMaterial = HumanCharacterBuilder.Mat("Cust_Bubble", Color.white, 0.2f);
        Object.DestroyImmediate(pill.GetComponent<Collider>());

        Transform anchor = new GameObject("IconAnchor").transform;
        anchor.SetParent(root.transform, false);
        anchor.localPosition = new Vector3(-0.27f, 0f, -0.05f);

        TextMesh shadow = MakeText(root.transform, "TextShadow", new Vector3(0.287f, -0.012f, -0.04f), font, new Color(0.6f, 0.6f, 0.6f));
        TextMesh text = MakeText(root.transform, "Text", new Vector3(0.275f, 0f, -0.06f), font, new Color(0.12f, 0.14f, 0.18f));

        var bubble = root.AddComponent<OrderBubble>();
        var so = new SerializedObject(bubble);
        so.FindProperty("pill").objectReferenceValue = pill.GetComponent<Renderer>();
        so.FindProperty("text").objectReferenceValue = text;
        so.FindProperty("shadow").objectReferenceValue = shadow;
        so.FindProperty("iconAnchor").objectReferenceValue = anchor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return bubble;
    }

    static TextMesh MakeText(Transform parent, string name, Vector3 pos, Font font, Color color)
    {
        GameObject go = new GameObject(name, typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        TextMesh tm = go.GetComponent<TextMesh>();
        tm.font = font;
        tm.text = "x1";
        tm.fontSize = 100;
        tm.characterSize = 0.05f;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return tm;
    }

    // ------------------------------------------------------------------ file d'attente

    static CustomerQueue BuildQueue(Transform root, CustomerConfig config, GameObject customerPrefab, StationConfig stationConfig)
    {
        float doorX = WorkshopBuilder.RightWallX;
        GameObject go = new GameObject("CustomerQueue", typeof(CustomerQueue));
        go.transform.SetParent(root, false);

        Transform spawn = Marker(go.transform, "SpawnPoint", new Vector3(doorX - 0.8f, 0f, InLaneZ));
        Transform lane = Marker(go.transform, "LaneCorner", new Vector3(CounterPosition.x, 0f, InLaneZ));
        Transform head = Marker(go.transform, "QueueHead", new Vector3(CounterPosition.x, 0f, CounterPosition.z - 1.4f));
        Transform e1 = Marker(go.transform, "Exit1", new Vector3(9.5f, 0f, head.position.z));
        Transform e2 = Marker(go.transform, "Exit2", new Vector3(9.5f, 0f, OutLaneZ));
        Transform e3 = Marker(go.transform, "Exit3", new Vector3(doorX + 0.8f, 0f, OutLaneZ)); // hors du mur : le client disparaît

        var queue = go.GetComponent<CustomerQueue>();
        var so = new SerializedObject(queue);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("customerPrefab").objectReferenceValue = customerPrefab;
        so.FindProperty("products").objectReferenceValue = stationConfig;
        so.FindProperty("spawnPoint").objectReferenceValue = spawn;
        so.FindProperty("laneCorner").objectReferenceValue = lane;
        so.FindProperty("queueHead").objectReferenceValue = head;
        SerializedProperty exits = so.FindProperty("exitPoints");
        exits.arraySize = 3;
        exits.GetArrayElementAtIndex(0).objectReferenceValue = e1;
        exits.GetArrayElementAtIndex(1).objectReferenceValue = e2;
        exits.GetArrayElementAtIndex(2).objectReferenceValue = e3;
        so.ApplyModifiedPropertiesWithoutUndo();
        return queue;
    }

    static Transform Marker(Transform parent, string name, Vector3 worldPos)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = worldPos;
        return t;
    }

    // ------------------------------------------------------------------ comptoir et caisse enregistreuse

    static void BuildCounter(Transform root, CustomerConfig config, CustomerQueue queue, GameObject billPrefab, FeedbackSpawner feedback)
    {
        Material wood = HumanCharacterBuilder.Mat("Cust_Wood", new Color(0.72f, 0.52f, 0.32f), 0.25f);
        Material woodDark = HumanCharacterBuilder.Mat("Cust_WoodDark", new Color(0.52f, 0.36f, 0.2f), 0.3f);
        Material steel = HumanCharacterBuilder.Mat("Cust_Steel", new Color(0.35f, 0.43f, 0.52f), 0.4f);
        Material dark = HumanCharacterBuilder.Mat("Cust_Dark", new Color(0.1f, 0.11f, 0.13f), 0.3f);
        Material screen = HumanCharacterBuilder.Mat("Cust_Screen", new Color(0.15f, 0.55f, 0.85f), 0.6f, new Color(0.2f, 0.75f, 1.2f));
        Material keys = HumanCharacterBuilder.Mat("Cust_Keys", new Color(0.82f, 0.84f, 0.86f), 0.3f);

        GameObject counter = new GameObject("ServiceCounter");
        counter.transform.SetParent(root, false);
        counter.transform.position = CounterPosition;

        // Zone de livraison, du côté du joueur (nord du comptoir)
        BoxCollider zone = counter.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(0f, 1f, 1.4f);
        zone.size = new Vector3(3.4f, 2f, 2.4f);

        Part(counter.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.9f, 0.8f), wood);
        Part(counter.transform, PrimitiveType.Cube, "Top", new Vector3(0f, 0.94f, 0f), new Vector3(3.4f, 0.08f, 0.95f), woodDark);
        Part(counter.transform, PrimitiveType.Cube, "FrontPanel", new Vector3(0f, 0.45f, -0.41f), new Vector3(3.0f, 0.7f, 0.02f), steel);
        Part(counter.transform, PrimitiveType.Cube, "Kick", new Vector3(0f, 0.04f, 0.0f), new Vector3(3.25f, 0.08f, 0.85f), dark);

        // Caisse enregistreuse : base, écran incliné, touches, tiroir
        Transform reg = new GameObject("CashRegister").transform;
        reg.SetParent(counter.transform, false);
        reg.localPosition = new Vector3(-0.9f, 0.98f, 0f);
        Part(reg, PrimitiveType.Cube, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(0.55f, 0.2f, 0.42f), dark);
        Part(reg, PrimitiveType.Cube, "Keys", new Vector3(0f, 0.215f, 0.08f), new Vector3(0.4f, 0.03f, 0.18f), keys);
        GameObject disp = Part(reg, PrimitiveType.Cube, "Display", new Vector3(0f, 0.38f, -0.1f), new Vector3(0.42f, 0.26f, 0.04f), screen);
        disp.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        Part(reg, PrimitiveType.Cube, "DisplayFrame", new Vector3(0f, 0.38f, -0.12f), new Vector3(0.46f, 0.3f, 0.03f), dark).transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        Part(reg, PrimitiveType.Cube, "Drawer", new Vector3(0f, 0.06f, 0.23f), new Vector3(0.45f, 0.08f, 0.06f), steel);

        // Le billet est posé du côté du joueur
        Transform billPoint = new GameObject("BillPoint").transform;
        billPoint.SetParent(counter.transform, false);
        billPoint.localPosition = new Vector3(0.45f, 1.0f, 0.3f);

        var service = counter.AddComponent<ServiceCounter>();
        var so = new SerializedObject(service);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("queue").objectReferenceValue = queue;
        so.FindProperty("billPrefab").objectReferenceValue = billPrefab;
        so.FindProperty("billPoint").objectReferenceValue = billPoint;
        so.FindProperty("feedback").objectReferenceValue = feedback;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ corbeille

    static void BuildBin(Transform root, FeedbackSpawner feedback)
    {
        Material steel = HumanCharacterBuilder.Mat("Cust_BinSteel", new Color(0.30f, 0.34f, 0.40f), 0.45f);
        Material inside = HumanCharacterBuilder.Mat("Cust_BinInside", new Color(0.05f, 0.06f, 0.07f), 0.2f);
        Material yellow = HumanCharacterBuilder.Mat("Cust_BinRim", new Color(1f, 0.8f, 0.1f), 0.3f);

        GameObject bin = new GameObject("TrashBin");
        bin.transform.SetParent(root, false);
        bin.transform.position = BinPosition;

        BoxCollider zone = bin.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(-0.2f, 1f, -0.4f);
        zone.size = new Vector3(1.8f, 2f, 2f);

        Part(bin.transform, PrimitiveType.Cylinder, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.75f, 0.4f, 0.75f), steel);
        Part(bin.transform, PrimitiveType.Cylinder, "Rim", new Vector3(0f, 0.8f, 0f), new Vector3(0.82f, 0.03f, 0.82f), yellow);
        Part(bin.transform, PrimitiveType.Cylinder, "Inside", new Vector3(0f, 0.805f, 0f), new Vector3(0.62f, 0.03f, 0.62f), inside);
        Transform burst = new GameObject("BurstPoint").transform;
        burst.SetParent(bin.transform, false);
        burst.localPosition = new Vector3(0f, 0.9f, 0f);

        var trash = bin.AddComponent<TrashBin>();
        var so = new SerializedObject(trash);
        so.FindProperty("burstPoint").objectReferenceValue = burst;
        so.FindProperty("feedback").objectReferenceValue = feedback;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
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
}
