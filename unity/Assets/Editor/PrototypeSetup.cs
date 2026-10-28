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
        crate.transform.localScale = new Vector3(0.5f, 0.3f, 0.4f);
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
        player.AddComponent<PlayerController>();
        StackCarrier carrier = player.AddComponent<StackCarrier>();

        GameObject anchor = new GameObject("StackAnchor");
        anchor.transform.SetParent(player.transform, false);
        anchor.transform.localPosition = new Vector3(0f, -0.2f, -0.7f);

        GameObject maxIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        maxIndicator.name = "MaxIndicator";
        maxIndicator.transform.SetParent(player.transform, false);
        maxIndicator.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        maxIndicator.transform.localScale = new Vector3(0.8f, 0.3f, 0.05f);
        maxIndicator.GetComponent<Renderer>().sharedMaterial = maxMat;
        Object.DestroyImmediate(maxIndicator.GetComponent<Collider>());
        maxIndicator.SetActive(false);

        var carrierSo = new SerializedObject(carrier);
        carrierSo.FindProperty("stackAnchor").objectReferenceValue = anchor.transform;
        carrierSo.FindProperty("maxIndicator").objectReferenceValue = maxIndicator;
        carrierSo.ApplyModifiedPropertiesWithoutUndo();

        // Caméra
        Camera cam = Camera.main;
        cam.transform.position = new Vector3(0f, 12f, -8f);
        cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        CameraFollow follow = cam.gameObject.AddComponent<CameraFollow>();
        var followSo = new SerializedObject(follow);
        followSo.FindProperty("target").objectReferenceValue = player.transform;
        followSo.ApplyModifiedPropertiesWithoutUndo();

        // Zone de ramassage
        GameObject pile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pile.name = "ResourcePile";
        pile.transform.position = new Vector3(6f, 0.1f, 0f);
        pile.transform.localScale = new Vector3(3f, 0.2f, 3f);
        pile.GetComponent<Renderer>().sharedMaterial = pileMat;
        BoxCollider box = pile.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 10f, 1f); // assez haute pour englober le joueur
        ResourcePile resourcePile = pile.AddComponent<ResourcePile>();
        var pileSo = new SerializedObject(resourcePile);
        pileSo.FindProperty("itemPrefab").objectReferenceValue = cratePrefab;
        pileSo.ApplyModifiedPropertiesWithoutUndo();

        StationConfig stationConfig = LoadOrCreateStationConfig();
        GameObject billPrefab = BuildBillPrefab();
        BuildStation("InstallStation", new Vector3(-6f, 0.1f, 0f), resourcePile, stationConfig, billPrefab);
        GameObject station2 = BuildStation("InstallStation2", new Vector3(-6f, 0.1f, 9f), resourcePile, stationConfig, billPrefab);
        station2.SetActive(false); // débloqué par la dalle

        BuildJoystick(player.GetComponent<PlayerController>());
        BuildHud(player, carrier);
        BuildUnlockPad(station2);

        WorkshopBuilder.Run(); // habillage visuel (ambiance, atelier, technicien)

        EditorSceneManager.SaveScene(scene, Root + "/Prototype.unity");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log("Scène du prototype créée : " + Root + "/Prototype.unity. Appuie sur Play, déplace-toi avec WASD jusqu'à la zone grise.");
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
        so.ApplyModifiedPropertiesWithoutUndo();
        return station;
    }

    // Dalle de déblocage jaune : le joueur y paie, puis le poste `target` apparaît.
    static void BuildUnlockPad(GameObject target)
    {
        Material padMat = MakeMaterial("UnlockPad", new Color(0.95f, 0.8f, 0.15f));

        string configPath = Root + "/UnlockConfig.asset";
        UnlockConfig config = AssetDatabase.LoadAssetAtPath<UnlockConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<UnlockConfig>();
            AssetDatabase.CreateAsset(config, configPath);
        }

        GameObject pad = new GameObject("UnlockPad");
        pad.transform.position = new Vector3(-6f, 0.01f, 5f);

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
        GameObject labelGo = new GameObject("PriceLabel", typeof(TextMesh), typeof(FaceCamera));
        labelGo.transform.SetParent(pad.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        TextMesh label = labelGo.GetComponent<TextMesh>();
        label.font = font;
        label.fontSize = 100;
        label.characterSize = 0.1f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        target.AddComponent<PopIn>();

        Wallet wallet = Object.FindFirstObjectByType<Wallet>();
        UnlockPad unlockPad = pad.AddComponent<UnlockPad>();
        var so = new SerializedObject(unlockPad);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("wallet").objectReferenceValue = wallet;
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("priceLabel").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
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
        collectorSo.ApplyModifiedPropertiesWithoutUndo();

        Transform canvas = GameObject.Find("Canvas").transform;

        Text moneyText = MakeText("MoneyText", canvas, new Vector2(0f, 1f), new Vector2(40f, -40f), TextAnchor.UpperLeft);
        MoneyHud moneyHud = moneyText.gameObject.AddComponent<MoneyHud>();
        var moneySo = new SerializedObject(moneyHud);
        moneySo.FindProperty("wallet").objectReferenceValue = wallet;
        moneySo.FindProperty("label").objectReferenceValue = moneyText;
        moneySo.ApplyModifiedPropertiesWithoutUndo();

        Text crateText = MakeText("CrateText", canvas, new Vector2(1f, 1f), new Vector2(-40f, -40f), TextAnchor.UpperRight);
        CrateHud crateHud = crateText.gameObject.AddComponent<CrateHud>();
        var crateSo = new SerializedObject(crateHud);
        crateSo.FindProperty("carrier").objectReferenceValue = carrier;
        crateSo.FindProperty("label").objectReferenceValue = crateText;
        crateSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static Text MakeText(string name, Transform parent, Vector2 corner, Vector2 offset, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(480f, 120f);

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 80;
        text.fontStyle = FontStyle.Bold;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false; // ne bloque pas le joystick
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
        areaRt.anchorMax = new Vector2(0.6f, 1f);
        areaRt.offsetMin = Vector2.zero;
        areaRt.offsetMax = Vector2.zero;
        area.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible mais capte les touches

        RectTransform bg = MakeCircle("Background", areaRt, circle, 200f, new Color(1f, 1f, 1f, 0.25f));
        RectTransform knob = MakeCircle("Knob", bg, circle, 90f, new Color(1f, 1f, 1f, 0.7f));

        var so = new SerializedObject(area.GetComponent<VirtualJoystick>());
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("knob").objectReferenceValue = knob;
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
