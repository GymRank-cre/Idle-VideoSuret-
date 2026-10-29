using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Menu Tools > Idle Prototype > Build scene
// Construit la scène du prototype : sol, joueur, caméra qui suit,
// zone de ramassage et prefab de caisse. Relançable sans risque.
public static class PrototypeSetup
{
    const string Root = "Assets/Prototype";
    const int StartMoney = 0; // mettre 100 pour tester les dalles, puis remettre 0

    // ---- Positions de jeu et caméra : une seule zone à modifier ----
    static readonly Vector3 PilePosition = new Vector3(4.5f, 0.1f, 0f);
    static readonly Vector3 Station1Position = new Vector3(-4.5f, 0.1f, 0f);
    static readonly Vector3 Station2Position = new Vector3(-4.5f, 0.1f, 7.5f);
    static readonly Vector3 PadPosition = new Vector3(-4.5f, 0.01f, 4f);
    static readonly Vector3 CameraOffset = new Vector3(0f, 7.7f, -5.1f); // 10a : plus près (avant 12 / -8) pour un personnage plus gros, angle de 55° inchangé
    const float CameraFov = 65f;

    // ---- Échelle de la pile et vitesse du joueur (4e) ----
    const float CrateScale = 0.6f;       // taille des caisses portées
    const float StackItemHeight = 0.17f; // écart vertical entre deux caisses (pile pleine ~2,3 m)
    const float PlayerMoveSpeed = 4.2f;  // m/s à fond (valeur du [SerializeField] de PlayerController)

    [MenuItem("Tools/Idle Prototype/Build scene")]
    static void Build()
    {
        EnsureFolder(Root);
        EnsureFolder(Root + "/Materials");

        Material groundMat = MakeMaterial("Ground", new Color(0.62f, 0.65f, 0.69f));
        Material playerMat = MakeMaterial("Player", new Color(0.95f, 0.55f, 0.15f));
        Material crateMat = MakeMaterial("Crate", new Color(0.79f, 0.64f, 0.42f));
        Material pileMat = MakeMaterial("Pile", new Color(0.35f, 0.38f, 0.42f));
        Material maxMat = MakeMaterial("Max", new Color(0.85f, 0.2f, 0.2f));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Sol
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // Prefab de caisse
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate";
        crate.transform.localScale = new Vector3(0.5f, 0.3f, 0.4f) * CrateScale;
        crate.GetComponent<Renderer>().sharedMaterial = crateMat;
        Object.DestroyImmediate(crate.GetComponent<Collider>());
        GameObject cratePrefab = PrefabUtility.SaveAsPrefabAsset(crate, Root + "/Crate.prefab");
        Object.DestroyImmediate(crate);

        // Joueur
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(player.GetComponent<Collider>());
        player.AddComponent<CharacterController>();
        PlayerController playerController = player.AddComponent<PlayerController>();
        var playerSo = new SerializedObject(playerController);
        playerSo.FindProperty("moveSpeed").floatValue = PlayerMoveSpeed; // valeur sérialisée, le code ne change pas
        playerSo.ApplyModifiedPropertiesWithoutUndo();
        StackCarrier carrier = player.AddComponent<StackCarrier>();

        GameObject anchor = new GameObject("StackAnchor");
        anchor.transform.SetParent(player.transform, false);
        anchor.transform.localPosition = new Vector3(0f, -0.2f, -0.3f); // collée au dos, à hauteur de hanches (0,7 avec l'ancien visuel)

        GameObject maxIndicator = BuildMaxIndicator(player.transform, maxMat);

        var carrierSo = new SerializedObject(carrier);
        carrierSo.FindProperty("stackAnchor").objectReferenceValue = anchor.transform;
        carrierSo.FindProperty("maxIndicator").objectReferenceValue = maxIndicator;
        carrierSo.FindProperty("itemHeight").floatValue = StackItemHeight;
        carrierSo.FindProperty("makesSound").boolValue = true; // son de ramassage du joueur
        carrierSo.ApplyModifiedPropertiesWithoutUndo();

        // Caméra
        Camera cam = Camera.main;
        cam.transform.position = player.transform.position + CameraOffset;
        cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        cam.fieldOfView = CameraFov;
        CameraFollow follow = cam.gameObject.AddComponent<CameraFollow>();
        var followSo = new SerializedObject(follow);
        followSo.FindProperty("target").objectReferenceValue = player.transform;
        followSo.FindProperty("offset").vector3Value = CameraOffset;
        followSo.ApplyModifiedPropertiesWithoutUndo();

        // Zone de ramassage
        GameObject pile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pile.name = "ResourcePile";
        pile.transform.position = PilePosition;
        pile.transform.localScale = new Vector3(3f, 0.2f, 3f);
        pile.GetComponent<Renderer>().sharedMaterial = pileMat;
        BoxCollider box = pile.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.4f, 10f, 1.4f); // assez haute pour englober le joueur ; 4,2 m de côté pour que l'employé s'y place à côté de la palette
        ResourcePile resourcePile = pile.AddComponent<ResourcePile>();
        var pileSo = new SerializedObject(resourcePile);
        pileSo.FindProperty("itemPrefab").objectReferenceValue = cratePrefab;
        pileSo.ApplyModifiedPropertiesWithoutUndo();

        FeedbackBuilder.Build(); // retour d'information (chiffres flottants, éclats), relié ensuite aux postes et au joueur
        StationConfig stationConfig = LoadOrCreateStationConfig();
        GameObject billPrefab = BuildBillPrefab();
        BuildStation("InstallStation", Station1Position, resourcePile, stationConfig, billPrefab);
        GameObject station2 = BuildStation("InstallStation2", Station2Position, resourcePile, stationConfig, billPrefab);
        station2.SetActive(false); // débloqué par la dalle

        BuildJoystick(player.GetComponent<PlayerController>());
        BuildHud(player, carrier);
        UnlockPad station2Pad = BuildUnlockPad(station2, station2, PadPosition, "UnlockPad", "UnlockConfig", null);
        CustomerBuilder.Build(stationConfig, billPrefab); // clients, comptoir, file d'attente, corbeille (étape 5b)
        EmployeeBuilder.Build(); // dalle d'embauche et employé automatique et livreur (étapes 6a et 6b)
        DemandBuilder.Build(); // production à la demande des clients (étape 6c)
        UpgradeBuilder.Build(); // améliorations : état, bouton et panneau (étape 7b)
        AudioBuilder.Build(); // sons, musique, réglages (étape 8a)
        GuideBuilder.Build(); // flèche et phrase d'objectif du début de partie (étape 6b)

        SaveBuilder.Build(); // sauvegarde, gains hors ligne, écran « Bon retour » (étape 7a)

        AddPressScale(); // 10a : tous les boutons réagissent à l'appui
        WebGLBuilder.Build(); // écran « Touche pour jouer » (étape 8b), au-dessus de tout le reste du Canvas

        WorkshopBuilder.Run(); // habillage visuel (ambiance, atelier, technicien)
        EmployeeBuilder.BakeNavMesh(); // après le décor : le NavMesh voit tous les obstacles

        EditorSceneManager.SaveScene(scene, Root + "/Prototype.unity");
        WebGLBuilder.EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log("Scène du prototype créée : " + Root + "/Prototype.unity. Appuie sur Play, déplace-toi avec WASD jusqu'à la zone grise.");
    }

    static void AddPressScale()
    {
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.GetComponent<PressScale>() == null) b.gameObject.AddComponent<PressScale>();
    }

    // Panneau « MAX » : pastille rouge arrondie avec le mot MAX en blanc à contour sombre, face caméra.
    // Sa hauteur est calée par StackCarrier juste au-dessus de la dernière caisse.
    static GameObject BuildMaxIndicator(Transform player, Material redMat)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = new GameObject("MaxIndicator", typeof(FaceCamera));
        root.transform.SetParent(player, false);
        root.transform.localPosition = new Vector3(0f, 2.3f, 0f);

        GameObject pill = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        pill.name = "Pill";
        pill.transform.SetParent(root.transform, false);
        pill.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        pill.transform.localScale = new Vector3(0.3f, 0.44f, 0.06f); // ~0,88 m de long, 0,3 m de haut
        pill.GetComponent<Renderer>().sharedMaterial = redMat;
        Object.DestroyImmediate(pill.GetComponent<Collider>());

        foreach (bool shadow in new[] { true, false })
        {
            GameObject t = new GameObject(shadow ? "TextShadow" : "Text", typeof(TextMesh));
            t.transform.SetParent(root.transform, false);
            t.transform.localPosition = shadow ? new Vector3(0.012f, -0.012f, -0.04f) : new Vector3(0f, 0f, -0.05f);
            TextMesh tm = t.GetComponent<TextMesh>();
            tm.font = font;
            tm.text = "MAX";
            tm.fontSize = 100;
            tm.characterSize = 0.034f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = shadow ? new Color(0.25f, 0.02f, 0.02f) : Color.white;
            t.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        root.SetActive(false);
        return root;
    }

    // Config (asset conservé s'il existe : les réglages de Sébastien ne sont pas écrasés).
    static StationConfig LoadOrCreateStationConfig()
    {
        string configPath = Root + "/StationConfig.asset";
        StationConfig config = AssetDatabase.LoadAssetAtPath<StationConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<StationConfig>();
            AssetDatabase.CreateAsset(config, configPath);
        }

        // Produits finis : la liste n'est remplie que si elle est vide (une liste modifiée est conservée).
        var so = new SerializedObject(config);
        SerializedProperty products = so.FindProperty("products");
        ProductType[] types = ProductBuilder.EnsureProductTypes();
        if (products.arraySize == 0)
        {
            products.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++)
                products.GetArrayElementAtIndex(i).objectReferenceValue = types[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }
        return config;
    }

    // Prefab de billet : cube vert plat.
    static GameObject BuildBillPrefab()
    {
        Material billMat = MakeMaterial("Bill", new Color(0.25f, 0.75f, 0.35f));
        GameObject bill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bill.name = "Bill";
        bill.transform.localScale = new Vector3(0.4f, 0.04f, 0.25f);
        bill.GetComponent<Renderer>().sharedMaterial = billMat;
        bill.GetComponent<BoxCollider>().isTrigger = true; // détecté par BillCollector
        bill.AddComponent<Bill>();
        GameObject billPrefab = PrefabUtility.SaveAsPrefabAsset(bill, Root + "/Bill.prefab");
        Object.DestroyImmediate(bill);
        return billPrefab;
    }

    // Poste d'installation avec sa barre de progression et son point de sortie des billets.
    static GameObject BuildStation(string stationName, Vector3 position, ResourcePile cratesSource,
        StationConfig config, GameObject billPrefab)
    {
        Material stationMat = MakeMaterial("Station", new Color(0.1f, 0.18f, 0.4f));
        Material barBackMat = MakeMaterial("BarBack", new Color(0.12f, 0.12f, 0.14f));
        Material barFillMat = MakeMaterial("BarFill", new Color(0.95f, 0.8f, 0.2f));

        // Poste : racine non mise à l'échelle (le collider et les enfants restent propres)
        GameObject station = new GameObject(stationName);
        station.transform.position = position;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(station.transform, false);
        visual.transform.localScale = new Vector3(3f, 0.2f, 3f);
        visual.GetComponent<Renderer>().sharedMaterial = stationMat;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        BoxCollider box = station.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 10f, 3f); // assez haute pour englober le joueur
        box.center = new Vector3(0f, 5f, 0f);

        GameObject exit = new GameObject("BillExit");
        exit.transform.SetParent(station.transform, false);
        exit.transform.position = position + new Vector3(0f, -0.05f, -3.2f); // au sud, hors de la zone

        // Barre de progression au-dessus du poste
        GameObject bar = new GameObject("ProgressBar");
        bar.transform.SetParent(station.transform, false);
        bar.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        ProgressBar3D progressBar = bar.AddComponent<ProgressBar3D>();

        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Back";
        back.transform.SetParent(bar.transform, false);
        back.transform.localScale = new Vector3(1.7f, 0.28f, 0.04f);
        back.GetComponent<Renderer>().sharedMaterial = barBackMat;
        Object.DestroyImmediate(back.GetComponent<Collider>());

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "Fill";
        fill.transform.SetParent(bar.transform, false);
        fill.transform.localPosition = new Vector3(-0.8f, 0f, -0.03f);
        fill.transform.localScale = new Vector3(0.0001f, 0.2f, 0.04f);
        fill.GetComponent<Renderer>().sharedMaterial = barFillMat;
        Object.DestroyImmediate(fill.GetComponent<Collider>());

        var barSo = new SerializedObject(progressBar);
        barSo.FindProperty("fill").objectReferenceValue = fill.transform;
        barSo.ApplyModifiedPropertiesWithoutUndo();

        InstallStation installStation = station.AddComponent<InstallStation>();
        var so = new SerializedObject(installStation);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("cratesSource").objectReferenceValue = cratesSource;
        so.FindProperty("billPrefab").objectReferenceValue = billPrefab;
        so.FindProperty("progressBar").objectReferenceValue = progressBar;
        so.FindProperty("billExit").objectReferenceValue = exit.transform;
        so.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        so.FindProperty("outputTray").objectReferenceValue = ProductBuilder.BuildTray(station.transform);
        so.ApplyModifiedPropertiesWithoutUndo();
        return station;
    }

    // Dalle de déblocage jaune : le joueur y paie, puis `target` apparaît (avec un gonflement sur `popObject`).
    // Sert au 2e poste et à l'embauche ; `title` (facultatif) s'affiche au-dessus du prix.
    internal static UnlockPad BuildUnlockPad(GameObject target, GameObject popObject, Vector3 position,
        string padName, string configName, string title, int priceOverride = 0, bool startHidden = false)
    {
        Material padMat = MakeMaterial("UnlockPad", new Color(0.95f, 0.8f, 0.15f));

        string configPath = Root + "/" + configName + ".asset";
        UnlockConfig config = AssetDatabase.LoadAssetAtPath<UnlockConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<UnlockConfig>();
            AssetDatabase.CreateAsset(config, configPath);
        }
        if (priceOverride > 0) // le prix vient de l'EmployeeConfig (une seule source)
        {
            var priceSo = new SerializedObject(config);
            priceSo.FindProperty("price").intValue = priceOverride;
            priceSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        GameObject pad = new GameObject(padName);
        pad.transform.position = position;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(pad.transform, false);
        visual.transform.localScale = new Vector3(3f, 0.02f, 3f);
        visual.GetComponent<Renderer>().sharedMaterial = padMat;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        BoxCollider box = pad.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 10f, 3f);
        box.center = new Vector3(0f, 5f, 0f);

        // Prix affiché au-dessus de la dalle (TextMesh intégré, face à la caméra)
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject labelGo = new GameObject("PriceLabel", typeof(TextMesh), typeof(FaceCamera));
        labelGo.transform.SetParent(pad.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.55f, 0f); // le prix est sous le titre, bien séparé
        TextMesh label = labelGo.GetComponent<TextMesh>();
        label.font = font;
        label.fontSize = 100;
        label.characterSize = 0.1f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        if (!string.IsNullOrEmpty(title))
            BuildPadTitle(labelGo.transform, font, title); // enfant du prix : décalage dans le repère de la caméra (en pixels écran)

        // Barre de remplissage au bord sud de la dalle : se remplit au fil du paiement
        Material fillMat = MakeMaterial("PadFill", new Color(0.3f, 0.9f, 0.4f));
        Material fillBack = MakeMaterial("PadFillBack", new Color(0.12f, 0.12f, 0.14f));
        GameObject backBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backBar.name = "FillBack";
        backBar.transform.SetParent(pad.transform, false);
        backBar.transform.localPosition = new Vector3(0f, 0.03f, -1.75f);
        backBar.transform.localScale = new Vector3(3f, 0.04f, 0.22f);
        backBar.GetComponent<Renderer>().sharedMaterial = fillBack;
        Object.DestroyImmediate(backBar.GetComponent<Collider>());
        GameObject fillBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillBar.name = "FillBar";
        fillBar.transform.SetParent(pad.transform, false);
        fillBar.transform.localPosition = new Vector3(-1.5f, 0.05f, -1.75f);
        fillBar.transform.localScale = new Vector3(0.0001f, 0.05f, 0.16f);
        fillBar.GetComponent<Renderer>().sharedMaterial = fillMat;
        Object.DestroyImmediate(fillBar.GetComponent<Collider>());

        if (popObject.GetComponent<PopIn>() == null) popObject.AddComponent<PopIn>();

        Wallet wallet = Object.FindFirstObjectByType<Wallet>();
        UnlockPad unlockPad = pad.AddComponent<UnlockPad>();
        var so = new SerializedObject(unlockPad);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("wallet").objectReferenceValue = wallet;
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("priceLabel").objectReferenceValue = label;
        so.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        so.FindProperty("saveId").stringValue = padName;
        so.FindProperty("fillBar").objectReferenceValue = fillBar.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (startHidden) // dalle dévoilée plus tard par la précédente, avec un gonflement
        {
            pad.AddComponent<PopIn>();
            pad.SetActive(false);
        }
        return unlockPad;
    }

    // Titre fixe au-dessus du prix (« EMBAUCHE ») : texte jaune avec un contour sombre décalé.
    static void BuildPadTitle(Transform priceLabel, Font font, string title)
    {
        GameObject root = new GameObject("Title", typeof(FaceCamera));
        root.transform.SetParent(priceLabel, false);
        root.transform.localPosition = new Vector3(0f, 1.45f, 0f); // « vers le haut de l'écran », quel que soit l'angle de vue
        foreach (bool shadow in new[] { true, false })
        {
            GameObject t = new GameObject(shadow ? "Shadow" : "Text", typeof(TextMesh));
            t.transform.SetParent(root.transform, false);
            t.transform.localPosition = shadow ? new Vector3(0.025f, -0.025f, 0.02f) : Vector3.zero;
            TextMesh tm = t.GetComponent<TextMesh>();
            tm.font = font;
            tm.text = title;
            tm.fontSize = 100;
            tm.characterSize = 0.056f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = shadow ? new Color(0.05f, 0.07f, 0.1f) : Color.white; // blanc : lisible aussi sur le jaune de la dalle
            t.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }

    // Porte-monnaie, ramassage des billets et HUD (argent en haut à gauche,
    // caisses en haut à droite) dans le Canvas du joystick. Texte UI classique.
    static void BuildHud(GameObject player, StackCarrier carrier)
    {
        GameObject walletGo = new GameObject("Wallet", typeof(Wallet));
        Wallet wallet = walletGo.GetComponent<Wallet>();
        var walletSo = new SerializedObject(wallet);
        walletSo.FindProperty("startMoney").intValue = StartMoney;
        walletSo.ApplyModifiedPropertiesWithoutUndo();

        BillCollector collector = player.AddComponent<BillCollector>();
        var collectorSo = new SerializedObject(collector);
        collectorSo.FindProperty("wallet").objectReferenceValue = wallet;
        collectorSo.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        collectorSo.ApplyModifiedPropertiesWithoutUndo();

        Transform canvas = GameObject.Find("Canvas").transform;

        // Conteneur calé dans la zone sûre de l'écran (encoche) ; les pastilles y sont posées avec une marge haute.
        GameObject safeGo = new GameObject("HudSafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
        safeGo.transform.SetParent(canvas, false);
        RectTransform safe = safeGo.GetComponent<RectTransform>();
        safe.anchorMin = Vector2.zero;
        safe.anchorMax = Vector2.one;
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite dot = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Argent : pastille en haut à gauche avec un billet vert
        RectTransform moneyPill = MakePill("MoneyPill", safe, true, 330f, round);
        MakeIcon("BillIcon", moneyPill, round, new Vector2(24f, 0f), new Vector2(78f, 48f), new Color(0.45f, 0.85f, 0.45f));
        RectTransform billInner = MakeIcon("BillInner", moneyPill, round, new Vector2(32f, 0f), new Vector2(62f, 34f), new Color(0.22f, 0.62f, 0.32f));
        MakeIcon("BillSeal", moneyPill, dot, new Vector2(40f, 0f), new Vector2(22f, 22f), new Color(0.6f, 0.92f, 0.6f));
        Text moneyText = MakePillText("MoneyText", moneyPill);
        MoneyHud moneyHud = moneyText.gameObject.AddComponent<MoneyHud>();
        var moneySo = new SerializedObject(moneyHud);
        moneySo.FindProperty("wallet").objectReferenceValue = wallet;
        moneySo.FindProperty("label").objectReferenceValue = moneyText;
        moneySo.ApplyModifiedPropertiesWithoutUndo();

        // Caisses : pastille en haut à droite avec une caisse
        RectTransform cratePill = MakePill("CratePill", safe, false, 290f, round);
        MakeIcon("CrateIcon", cratePill, round, new Vector2(24f, 0f), new Vector2(60f, 60f), new Color(0.72f, 0.5f, 0.28f));
        MakeIcon("CrateTape", cratePill, round, new Vector2(47f, 0f), new Vector2(14f, 60f), new Color(0.92f, 0.84f, 0.64f));
        Text crateText = MakePillText("CrateText", cratePill);
        CrateHud crateHud = crateText.gameObject.AddComponent<CrateHud>();
        var crateSo = new SerializedObject(crateHud);
        crateSo.FindProperty("carrier").objectReferenceValue = carrier;
        crateSo.FindProperty("label").objectReferenceValue = crateText;
        crateSo.FindProperty("config").objectReferenceValue = FeedbackBuilder.LoadOrCreateConfig();
        crateSo.ApplyModifiedPropertiesWithoutUndo();

        // Produits finis portés à la main (étape 5a) : pastille sous celle des caisses, icône de caméra dôme
        ProductCarrier productCarrier = BuildProductCarrier(player);
        RectTransform productPill = MakePill("ProductPill", safe, false, 290f, round, 60f + 110f + 16f);
        MakeIcon("ProductBase", productPill, round, new Vector2(24f, -16f), new Vector2(62f, 14f), new Color(0.93f, 0.94f, 0.96f));
        MakeIcon("ProductDome", productPill, dot, new Vector2(33f, 4f), new Vector2(44f, 36f), new Color(0.45f, 0.65f, 0.85f));
        Text productText = MakePillText("ProductText", productPill);
        ProductHud productHud = productText.gameObject.AddComponent<ProductHud>();
        var productSo = new SerializedObject(productHud);
        productSo.FindProperty("carrier").objectReferenceValue = productCarrier;
        productSo.FindProperty("label").objectReferenceValue = productText;
        productSo.FindProperty("config").objectReferenceValue = FeedbackBuilder.LoadOrCreateConfig();
        productSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // Les produits finis sont portés devant le torse, sur un petit plateau (distinct de la pile de caisses dans le dos).
    static ProductCarrier BuildProductCarrier(GameObject player)
    {
        GameObject anchor = new GameObject("CarryAnchor");
        anchor.transform.SetParent(player.transform, false);
        anchor.transform.localPosition = new Vector3(0f, -0.2f, 0.55f); // à hauteur de ceinture, devant

        Material trayMat = MakeMaterial("CarryTray", new Color(0.25f, 0.3f, 0.36f));
        GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "CarryTray";
        tray.transform.SetParent(anchor.transform, false);
        tray.transform.localPosition = new Vector3(0f, -0.03f, 0f);
        tray.transform.localScale = new Vector3(0.7f, 0.04f, 0.56f); // 4 produits à plat, 2 x 2
        tray.GetComponent<Renderer>().sharedMaterial = trayMat;
        Object.DestroyImmediate(tray.GetComponent<Collider>());

        ProductCarrier carrier = player.AddComponent<ProductCarrier>();
        var so = new SerializedObject(carrier);
        so.FindProperty("carryAnchor").objectReferenceValue = anchor.transform;
        so.FindProperty("trayVisual").objectReferenceValue = tray;
        so.ApplyModifiedPropertiesWithoutUndo();
        return carrier;
    }

    // Pastille arrondie semi-transparente sombre, ancrée en haut à gauche ou à droite.
    static RectTransform MakePill(string name, Transform parent, bool left, float width, Sprite round, float top = 60f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 corner = new Vector2(left ? 0f : 1f, 1f);
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = new Vector2(left ? 40f : -40f, -top); // marge haute de 60 px pour la 1re pastille
        rt.sizeDelta = new Vector2(width, 110f);

        Image img = go.GetComponent<Image>();
        img.sprite = UiSprites.Rounded();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.75f; // coins bien arrondis
        img.color = new Color(0.05f, 0.07f, 0.10f, 0.55f);
        img.raycastTarget = false; // ne bloque pas le joystick
        return rt;
    }

    static RectTransform MakeIcon(string name, RectTransform pill, Sprite sprite, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pill, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // Texte UI classique à droite de l'icône, avec contour sombre.
    static Text MakePillText(string name, RectTransform pill)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(pill, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(112f, 0f);
        rt.offsetMax = new Vector2(-18f, 0f);

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 66;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }

    // Canvas + EventSystem + joystick tactile flottant (moitié gauche de l'écran).
    static void BuildJoystick(PlayerController player)
    {
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        GameObject area = new GameObject("JoystickArea", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
        area.transform.SetParent(canvasGo.transform, false);
        RectTransform areaRt = area.GetComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = new Vector2(1f, 1f); // tout l'écran : droitiers et gauchers posent le pouce où ils veulent
        areaRt.offsetMin = Vector2.zero;
        areaRt.offsetMax = Vector2.zero;
        area.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible mais capte les touches

        RectTransform bg = MakeCircle("Background", areaRt, circle, 330f, new Color(1f, 1f, 1f, 0.38f)); // adapté au pouce
        RectTransform knob = MakeCircle("Knob", bg, circle, 120f, new Color(1f, 1f, 1f, 0.85f));

        var so = new SerializedObject(area.GetComponent<VirtualJoystick>());
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("knob").objectReferenceValue = knob;
        so.FindProperty("radius").floatValue = 150f; // course du rond, accordée à sa nouvelle taille
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform MakeCircle(string name, RectTransform parent, Sprite sprite, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    static Material MakeMaterial(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
