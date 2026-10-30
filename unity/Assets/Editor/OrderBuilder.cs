using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Commandes de clients (11c) : données, tableau, quai d'expédition, camionnette et interface. Appelé après les améliorations.
public static class OrderBuilder
{
    const string Root = "Assets/Prototype";
    static readonly Vector3 DockPosition = new Vector3(11.5f, 0.01f, -8.2f);   // près de la porte roulante, hors des couloirs des clients
    static readonly Vector3 VanPark = new Vector3(15.0f, 0f, -10.9f);           // juste dehors, devant la porte
    static readonly Vector3 VanAway = new Vector3(32f, 0f, -10.9f);

    public static OrderConfig LoadOrCreateConfig()
    {
        string path = Root + "/OrderConfig.asset";
        OrderConfig config = AssetDatabase.LoadAssetAtPath<OrderConfig>(path);
        if (config != null) return config;

        config = ScriptableObject.CreateInstance<OrderConfig>();
        AssetDatabase.CreateAsset(config, path);
        var so = new SerializedObject(config);
        object[][] clients =
        {
            new object[] { "Karim", "Gérant", "E0AC82", "3B2A20", "1FA0FF", "CFE6FF" },
            new object[] { "Léa", "Boulangère", "F2C9A5", "7A4A2A", "F2453D", "FFD6DC" },
            new object[] { "Marc", "Garagiste", "C68A5E", "1A1A1A", "3DDC5A", "D6F5D2" },
            new object[] { "Inès", "Pharmacienne", "8D5A3C", "0E0E12", "A855F7", "E8D9FF" },
            new object[] { "Paul", "Hôtelier", "F5D2B3", "C9A24A", "FF7A1A", "FFE8C7" },
            new object[] { "Sofia", "Directrice", "D9A074", "5A2E1A", "27D3E6", "CFF3F7" },
        };
        SerializedProperty list = so.FindProperty("clients");
        list.arraySize = clients.Length;
        for (int i = 0; i < clients.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("displayName").stringValue = (string)clients[i][0];
            e.FindPropertyRelative("job").stringValue = (string)clients[i][1];
            e.FindPropertyRelative("skin").colorValue = Hex((string)clients[i][2]);
            e.FindPropertyRelative("hair").colorValue = Hex((string)clients[i][3]);
            e.FindPropertyRelative("cloth").colorValue = Hex((string)clients[i][4]);
            e.FindPropertyRelative("backdrop").colorValue = Hex((string)clients[i][5]);
        }
        object[][] tiers =
        {
            new object[] { "Petite", 1, 2, 120f, 6f },
            new object[] { "Moyenne", 3, 5, 180f, 3f },
            new object[] { "Grosse", 6, 10, 300f, 1f },
        };
        SerializedProperty tl = so.FindProperty("tiers");
        tl.arraySize = tiers.Length;
        for (int i = 0; i < tiers.Length; i++)
        {
            SerializedProperty e = tl.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("displayName").stringValue = (string)tiers[i][0];
            e.FindPropertyRelative("minItems").intValue = (int)tiers[i][1];
            e.FindPropertyRelative("maxItems").intValue = (int)tiers[i][2];
            e.FindPropertyRelative("patience").floatValue = (float)tiers[i][3];
            e.FindPropertyRelative("weight").floatValue = (float)tiers[i][4];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config); // sur disque tout de suite : un rechargement de domaine ne doit pas perdre les données
        return config;
    }

    static Color Hex(string h)
    {
        return new Color32(System.Convert.ToByte(h.Substring(0, 2), 16), System.Convert.ToByte(h.Substring(2, 2), 16), System.Convert.ToByte(h.Substring(4, 2), 16), 255);
    }

    public static void Build()
    {
        Wallet wallet = Object.FindFirstObjectByType<Wallet>();
        GameObject canvas = GameObject.Find("Canvas");
        Transform safe = canvas != null ? canvas.transform.Find("HudSafeArea") : null;
        StationConfig station = AssetDatabase.LoadAssetAtPath<StationConfig>(Root + "/StationConfig.asset");
        if (wallet == null || safe == null || station == null || station.Products == null || station.Products.Length == 0) return;

        foreach (string old in new[] { "OrderBoard", "ProPoints", "ShippingDock", "DeliveryVan" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }
        foreach (string old in new[] { "OrdersButton", "OrdersPanel", "OrdersHost", "OrderBar", "ProPill" })
        {
            Transform t = safe.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        OrderConfig config = LoadOrCreateConfig();
        FeedbackSpawner feedback = Object.FindFirstObjectByType<FeedbackSpawner>();
        CameraFollow cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
        GameObject packRoot = null;
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g.name == "PackMachine" && g.transform.parent == null) packRoot = g;

        var proGo = new GameObject("ProPoints", typeof(ProPoints));
        ProPoints pro = proGo.GetComponent<ProPoints>();

        var boardGo = new GameObject("OrderBoard", typeof(OrderBoard));
        OrderBoard board = boardGo.GetComponent<OrderBoard>();
        var bso = new SerializedObject(board);
        bso.FindProperty("config").objectReferenceValue = config;
        bso.FindProperty("packMachine").objectReferenceValue = packRoot;
        bso.FindProperty("wallet").objectReferenceValue = wallet;
        bso.FindProperty("pro").objectReferenceValue = pro;
        SerializedProperty types = bso.FindProperty("types");
        int n = Mathf.Min(3, station.Products.Length);
        types.arraySize = n;
        for (int i = 0; i < n; i++) types.GetArrayElementAtIndex(i).objectReferenceValue = station.Products[i];
        bso.ApplyModifiedPropertiesWithoutUndo();

        Transform van = BuildVan(board, feedback, cameraFollow);
        Transform dockTarget = van;
        GameObject dock = BuildDock(board, dockTarget, feedback, packRoot);
        WireCourier(board, dock.transform);
        BuildUi(safe, board, pro, cameraFollow, dock.transform);
    }

    // ------------------------------------------------------------------ quai, camionnette

    static GameObject BuildDock(OrderBoard board, Transform target, FeedbackSpawner feedback, GameObject packRoot)
    {
        var dock = new GameObject("ShippingDock");
        dock.transform.position = DockPosition;
        var box = dock.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 10f, 3f);
        box.center = new Vector3(0f, 5f, 0f);

        Material plate = PrototypeSetup.MakeMaterial("DockPlate", UiTheme.Slate);
        Material edge = PrototypeSetup.MakeMaterial("DockEdge", new Color32(0xFF, 0xC4, 0x00, 0xFF));
        RoundedBox.Make(dock.transform, "Plate", new Vector3(0f, 0.03f, 0f), new Vector3(3f, 0.06f, 3f), 0.05f, plate);
        RoundedBox.Make(dock.transform, "Edge", new Vector3(0f, 0.02f, 0f), new Vector3(3.2f, 0.04f, 3.2f), 0.05f, edge);

        var label = new GameObject("Label", typeof(FaceCamera));
        label.transform.SetParent(dock.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        MachineBuilder.MakeText("Outline", label.transform, new Vector3(0.03f, -0.03f, 0.01f), UiTheme.Ink, 12, 0.055f, TextAnchor.MiddleCenter).text = "QUAI";
        MachineBuilder.MakeText("Text", label.transform, Vector3.zero, Color.white, 13, 0.055f, TextAnchor.MiddleCenter).text = "QUAI";

        // petite camionnette posée sur la plaque : on comprend que c'est là que partent les colis
        Material vw = PrototypeSetup.MakeMaterial("VanWhite", new Color(0.94f, 0.95f, 0.97f));
        Material vb = PrototypeSetup.MakeMaterial("VanBlue", new Color32(0x1F, 0xA0, 0xFF, 0xFF));
        var mini = new GameObject("MiniVan").transform;
        mini.SetParent(dock.transform, false);
        mini.localPosition = new Vector3(0f, 0.04f, -0.55f);
        mini.localRotation = Quaternion.Euler(0f, 90f, 0f);
        mini.localScale = Vector3.one * 0.3f;
        VanBody(mini);

        var sd = dock.AddComponent<ShippingDock>();
        var so = new SerializedObject(sd);
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("feedback").objectReferenceValue = feedback;
        so.ApplyModifiedPropertiesWithoutUndo();

        var bso = new SerializedObject(board);
        bso.FindProperty("dockObject").objectReferenceValue = dock;
        bso.ApplyModifiedPropertiesWithoutUndo();
        dock.SetActive(false);
        return dock;
    }

    // Corps de fourgon blanc arrondi à bande bleue, cabine, pare-brise et roues (camionnette livrée, garée, et mini-fourgon du quai).
    public static void VanBody(Transform root)
    {
        Material white = PrototypeSetup.MakeMaterial("VanWhite", new Color(0.94f, 0.95f, 0.97f));
        Material blue = PrototypeSetup.MakeMaterial("VanBlue", new Color32(0x1F, 0xA0, 0xFF, 0xFF));
        Material glass = PrototypeSetup.MakeMaterial("VanGlass", new Color(0.2f, 0.3f, 0.4f));
        Material tire = PrototypeSetup.MakeMaterial("VanTire", new Color(0.08f, 0.09f, 0.1f));
        RoundedBox.Make(root.transform, "Box", new Vector3(0f, 1.45f, -0.7f), new Vector3(1.9f, 1.9f, 2.9f), 0.18f, white);
        RoundedBox.Make(root.transform, "Cab", new Vector3(0f, 1.0f, 1.35f), new Vector3(1.9f, 1.3f, 1.4f), 0.2f, blue);
        RoundedBox.Make(root.transform, "Window", new Vector3(0f, 1.25f, 1.85f), new Vector3(1.55f, 0.55f, 0.15f), 0.05f, glass);
        RoundedBox.Make(root.transform, "Stripe", new Vector3(0f, 0.9f, -0.7f), new Vector3(1.95f, 0.18f, 2.95f), 0.05f, blue);
        foreach (Vector3 w in new[] { new Vector3(-0.9f, 0.38f, 1.2f), new Vector3(0.9f, 0.38f, 1.2f), new Vector3(-0.9f, 0.38f, -1.3f), new Vector3(0.9f, 0.38f, -1.3f) })
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localPosition = w;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.76f, 0.14f, 0.76f);
            wheel.GetComponent<Renderer>().sharedMaterial = tire;
        }
    }

    static Transform BuildVan(OrderBoard board, FeedbackSpawner feedback, CameraFollow cameraFollow)
    {
        var root = new GameObject("DeliveryVan");
        root.transform.position = VanAway;
        root.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // le nez vers l'atelier (-x)

        VanBody(root.transform);

        // bulle d'humeur au-dessus
        var bubble = new GameObject("Mood", typeof(SpriteRenderer), typeof(FaceCamera));
        bubble.transform.SetParent(root.transform, false);
        bubble.transform.localPosition = new Vector3(0f, 3.6f, 0f);
        bubble.transform.localScale = Vector3.one * 1.1f;
        var sr = bubble.GetComponent<SpriteRenderer>();
        sr.sprite = UiSprites.Mood(0);
        sr.sortingOrder = 20;

        var park = new GameObject("VanPark").transform;
        park.position = VanPark;
        park.SetParent(root.transform.parent, true);
        var peek = new GameObject("VanPeek").transform;
        peek.position = new Vector3(14.8f, 0f, -10.6f); // la vue portrait ne couvre que ~6 m de large : il faut viser la camionnette elle-même
        var away = new GameObject("VanAway").transform;
        away.position = VanAway;

        var van = root.AddComponent<DeliveryVan>();
        var so = new SerializedObject(van);
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("awayPoint").objectReferenceValue = away;
        so.FindProperty("parkPoint").objectReferenceValue = park;
        so.FindProperty("peekPoint").objectReferenceValue = peek;
        so.FindProperty("moodBubble").objectReferenceValue = sr;
        SerializedProperty moods = so.FindProperty("moodSprites");
        moods.arraySize = 3;
        for (int i = 0; i < 3; i++) moods.GetArrayElementAtIndex(i).objectReferenceValue = UiSprites.Mood(i);
        so.FindProperty("feedback").objectReferenceValue = feedback;
        so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root.transform;
    }

    static void WireCourier(OrderBoard board, Transform dock)
    {
        Courier courier = Object.FindFirstObjectByType<Courier>(FindObjectsInactive.Include);
        GameObject standsGo = GameObject.Find("CourierStands");
        if (courier == null || standsGo == null) return;
        foreach (string old in new[] { "DockStand", "DockLook" })
        {
            Transform t = standsGo.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        var stand = new GameObject("DockStand").transform;
        stand.SetParent(standsGo.transform, false);
        stand.position = dock.position + new Vector3(-1.0f, 0f, 0.9f);
        var look = new GameObject("DockLook").transform;
        look.SetParent(standsGo.transform, false);
        look.position = dock.position;
        var so = new SerializedObject(courier);
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("dockStand").objectReferenceValue = stand;
        so.FindProperty("dockLook").objectReferenceValue = look;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ interface

    static void BuildUi(Transform safe, OrderBoard board, ProPoints pro, CameraFollow cameraFollow, Transform dock)
    {
        Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite rounded = UiSprites.Rounded();
        OrderConfig config = board.Config;
        Transform canvasRoot = safe.parent;

        // bouton carré orange « Commandes » sous le bouton Améliorations, avec la pastille rouge du nombre d'offres
        GameObject rootGo = new GameObject("OrdersButton", typeof(RectTransform), typeof(Button));
        rootGo.transform.SetParent(safe, false);
        RectTransform button = rootGo.GetComponent<RectTransform>();
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-40f, -680f);
        button.sizeDelta = new Vector2(150f, 150f);
        RectTransform baseRt = UpgradeBuilder.Box("Base", button, rounded, new Color32(0xC2, 0x53, 0x0A, 0xFF), new Vector2(0f, -14f), new Vector2(150f, 150f), false);
        baseRt.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        RectTransform face = UpgradeBuilder.Box("Face", button, rounded, UiTheme.Orange, Vector2.zero, new Vector2(150f, 150f), true);
        face.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        rootGo.GetComponent<Button>().targetGraphic = face.GetComponent<Image>();
        Object.DestroyImmediate(face.GetComponent<Button>());
        // pictogramme : planchette (rectangle blanc, pince orange, lignes orange, coche verte), coins nets
        RectTransform clipboard = UpgradeBuilder.Box("Board", face, rounded, Color.white, new Vector2(0f, -6f), new Vector2(78f, 96f), false);
        clipboard.GetComponent<Image>().pixelsPerUnitMultiplier = 3.2f;
        Sharp(UpgradeBuilder.Box("Clip", face, rounded, new Color32(0xC2, 0x53, 0x0A, 0xFF), new Vector2(0f, 42f), new Vector2(38f, 20f), false), 3.2f);
        Sharp(UpgradeBuilder.Box("L1", face, rounded, UiTheme.Orange, new Vector2(10f, 14f), new Vector2(40f, 8f), false), 3.2f);
        Sharp(UpgradeBuilder.Box("L2", face, rounded, UiTheme.Orange, new Vector2(10f, -4f), new Vector2(40f, 8f), false), 3.2f);
        Sharp(UpgradeBuilder.Box("L3", face, rounded, UiTheme.Orange, new Vector2(10f, -22f), new Vector2(28f, 8f), false), 3.2f);
        UpgradeBuilder.Rot(Sharp(UpgradeBuilder.Box("Tick1", face, rounded, UiTheme.Green, new Vector2(-24f, -20f), new Vector2(7f, 14f), false), 3f), -35f);
        UpgradeBuilder.Rot(Sharp(UpgradeBuilder.Box("Tick2", face, rounded, UiTheme.Green, new Vector2(-18f, -17f), new Vector2(7f, 22f), false), 3f), 35f);
        RectTransform dot = UpgradeBuilder.Box("Dot", button, circle, UiTheme.Red, new Vector2(62f, 62f), new Vector2(60f, 60f), false);
        UpgradeBuilder.Box("DotRim", dot, circle, new Color(1f, 1f, 1f, 0.95f), Vector2.zero, new Vector2(60f, 60f), false).SetAsFirstSibling();
        UpgradeBuilder.Box("DotIn", dot, circle, UiTheme.Red, Vector2.zero, new Vector2(50f, 50f), false);
        Text badgeText = UpgradeBuilder.Label("Count", dot, "1", 40, Color.white, Vector2.zero, new Vector2(60f, 60f));
        button.gameObject.AddComponent<PopIn>();

        // fond sombre plein écran, cliquable : toucher en dehors du panneau le ferme
        GameObject backdropGo = new GameObject("OrdersBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropGo.transform.SetParent(safe, false);
        var bdRt = backdropGo.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one; bdRt.offsetMin = bdRt.offsetMax = Vector2.zero;
        var bdImg = backdropGo.GetComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.6f);
        backdropGo.GetComponent<Button>().targetGraphic = bdImg;
        backdropGo.SetActive(false);

        // panneau : orange, bandeau sombre COMMANDES, cartes crème
        GameObject panelGo = new GameObject("OrdersPanel", typeof(RectTransform), typeof(PopIn));
        panelGo.transform.SetParent(safe, false);
        RectTransform panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 10f);
        panel.sizeDelta = new Vector2(980f, 1280f);
        Image back = panelGo.AddComponent<Image>();
        back.sprite = rounded;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 0.7f;
        back.color = UiTheme.Orange;
        RectTransform header = UpgradeBuilder.Box("Header", panel, rounded, UiTheme.Slate, new Vector2(0f, 560f), new Vector2(940f, 130f), false);
        header.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Title", header, Loc.T("orders.title"), 78, Color.white, Vector2.zero, new Vector2(700f, 120f));
        UpgradeBuilder.Box("CloseRim", panel, circle, Color.white, new Vector2(418f, 560f), new Vector2(112f, 112f), false);
        RectTransform close = UpgradeBuilder.Box("CloseButton", panel, circle, UiTheme.Red, new Vector2(418f, 560f), new Vector2(100f, 100f), true);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X1", close, round, Color.white, Vector2.zero, new Vector2(14f, 54f), false), 45f);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X2", close, round, Color.white, Vector2.zero, new Vector2(14f, 54f), false), -45f);

        var cards = new OrderCard[3];
        for (int i = 0; i < 3; i++)
            cards[i] = BuildCard(panel, rounded, circle, new Vector2(0f, 330f - 345f * i), i);
        Text empty = UpgradeBuilder.Label("Empty", panel, Loc.T("orders.empty"), 52, Color.white, new Vector2(0f, 120f), new Vector2(820f, 160f));

        // barre compacte sous la consigne
        RectTransform bar = UpgradeBuilder.Box("OrderBar", safe, rounded, UiTheme.Slate, new Vector2(0f, 0f), new Vector2(700f, 92f), false);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -430f);
        ChildBase(bar, rounded, new Color32(0x12, 0x1A, 0x24, 0xFF), 8f); // socle DANS la barre : il se cache avec elle
        RectTransform fillBack = UpgradeBuilder.Box("FillBack", bar, rounded, new Color32(0x12, 0x1A, 0x24, 0xFF), new Vector2(0f, -34f), new Vector2(640f, 14f), false);
        RectTransform fill = UpgradeBuilder.Box("Fill", fillBack, rounded, UiTheme.Yellow, Vector2.zero, new Vector2(640f, 14f), false);
        var fillImg = fill.GetComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        Text barText = UpgradeBuilder.Label("Text", bar, "", 42, Color.white, new Vector2(0f, 8f), new Vector2(680f, 60f));

        // pastille Points Pro : cyan, étoile blanche ; cachée tant que 0
        RectTransform proPill = UpgradeBuilder.Box("ProPill", safe, rounded, new Color32(0x1B, 0xA8, 0xBB, 0xFF), Vector2.zero, new Vector2(290f, 110f), false);
        proPill.anchorMin = proPill.anchorMax = proPill.pivot = new Vector2(0f, 1f);
        proPill.anchoredPosition = new Vector2(40f, -186f);
        proPill.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        ChildBase(proPill, rounded, new Color32(0x0F, 0x6E, 0x7A, 0xFF), 10f);
        RectTransform star = UpgradeBuilder.Box("Star", proPill, UiSprites.Star(), Color.white, new Vector2(-96f, 0f), new Vector2(76f, 76f), false);
        star.GetComponent<Image>().type = Image.Type.Simple;
        star.GetComponent<Image>().preserveAspect = true;
        Text proText = UpgradeBuilder.Label("Value", proPill, "0", 70, Color.white, new Vector2(34f, 0f), new Vector2(180f, 100f));

        GameObject host = new GameObject("OrdersHost", typeof(RectTransform), typeof(OrdersUi));
        host.transform.SetParent(safe, false);
        var ui = host.GetComponent<OrdersUi>();
        var so = new SerializedObject(ui);
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("pro").objectReferenceValue = pro;
        so.FindProperty("openButton").objectReferenceValue = button.GetComponent<Button>();
        so.FindProperty("badge").objectReferenceValue = dot.gameObject;
        so.FindProperty("badgeText").objectReferenceValue = badgeText;
        so.FindProperty("panel").objectReferenceValue = panelGo;
        so.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>();
        so.FindProperty("emptyText").objectReferenceValue = empty;
        so.FindProperty("bar").objectReferenceValue = bar.gameObject;
        so.FindProperty("barText").objectReferenceValue = barText;
        so.FindProperty("barFill").objectReferenceValue = fillImg;
        so.FindProperty("proPill").objectReferenceValue = proPill.gameObject;
        so.FindProperty("proText").objectReferenceValue = proText;
        so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        so.FindProperty("dockPoint").objectReferenceValue = dock;
        so.FindProperty("backdrop").objectReferenceValue = backdropGo.GetComponent<Button>();
        SerializedProperty hn = so.FindProperty("hideNames");
        string[] names = { "UpgradeButton", "OrdersButton", "ReputationButton", "ViewButton", "GuideBar" };
        hn.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++) hn.GetArrayElementAtIndex(i).stringValue = names[i];
        SerializedProperty list = so.FindProperty("cards");
        list.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static OrderCard BuildCard(RectTransform panel, Sprite rounded, Sprite circle, Vector2 pos, int index)
    {
        RectTransform card = UpgradeBuilder.Box("Card" + index, panel, rounded, new Color32(0xFF, 0xF1, 0xD6, 0xFF), pos, new Vector2(920f, 325f), false);
        card.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;

        // portrait : fond ardoise, épaules, tête, cheveux
        RectTransform frame = UpgradeBuilder.Box("Portrait", card, rounded, new Color32(0xCF, 0xE6, 0xFF, 0xFF), new Vector2(-352f, 30f), new Vector2(190f, 190f), false);
        frame.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        RectTransform shoulders = UpgradeBuilder.Box("Cloth", frame, rounded, UiTheme.Orange, new Vector2(0f, -66f), new Vector2(150f, 70f), false);
        RectTransform earL = UpgradeBuilder.Box("EarL", frame, circle, UiTheme.Orange, new Vector2(-48f, 6f), new Vector2(26f, 32f), false);
        RectTransform earR = UpgradeBuilder.Box("EarR", frame, circle, UiTheme.Orange, new Vector2(48f, 6f), new Vector2(26f, 32f), false);
        RectTransform head = UpgradeBuilder.Box("Skin", frame, circle, UiTheme.Orange, new Vector2(0f, 10f), new Vector2(92f, 98f), false);
        RectTransform hairTop = UpgradeBuilder.Box("Hair", frame, circle, UiTheme.Ink, new Vector2(0f, 44f), new Vector2(98f, 50f), false);
        RectTransform noseRt = UpgradeBuilder.Box("Nose", frame, circle, UiTheme.Orange, new Vector2(0f, 2f), new Vector2(16f, 20f), false);
        UpgradeBuilder.Box("EyeL", frame, circle, UiTheme.Ink, new Vector2(-18f, 14f), new Vector2(10f, 12f), false);
        UpgradeBuilder.Box("EyeR", frame, circle, UiTheme.Ink, new Vector2(18f, 14f), new Vector2(10f, 12f), false);

        Text nameText = InkLabel("Name", card, 44, new Vector2(100f, 118f), new Vector2(620f, 60f), TextAnchor.MiddleLeft);
        Text content = InkLabel("Content", card, 36, new Vector2(98f, 44f), new Vector2(610f, 110f), TextAnchor.UpperLeft);
        content.horizontalOverflow = HorizontalWrapMode.Wrap;
        content.verticalOverflow = VerticalWrapMode.Overflow;
        content.lineSpacing = 0.9f;
        Text reward = InkLabel("Reward", card, 50, new Vector2(70f, -16f), new Vector2(560f, 70f), TextAnchor.MiddleLeft);
        reward.color = new Color32(0x1F, 0x8F, 0x3A, 0xFF);

        RectTransform refuse = UpgradeBuilder.Box("Refuse", card, rounded, UiTheme.Red, new Vector2(-301f, -118f), new Vector2(270f, 90f), true);
        UpgradeBuilder.Label("Text", refuse, Loc.T("orders.refuse"), 44, Color.white, Vector2.zero, new Vector2(270f, 90f));
        RectTransform accept = UpgradeBuilder.Box("Accept", card, rounded, UiTheme.Green, new Vector2(25f, -118f), new Vector2(300f, 90f), true);
        UpgradeBuilder.Label("Text", accept, Loc.T("orders.accept"), 46, Color.white, Vector2.zero, new Vector2(300f, 90f));
        RectTransform later = UpgradeBuilder.Box("Later", card, rounded, UiTheme.Cyan, new Vector2(326f, -118f), new Vector2(220f, 90f), true);
        later.GetComponent<Image>().color = new Color32(0x1F, 0xA0, 0xFF, 0xFF);
        Text laterLabel = UpgradeBuilder.Label("Text", later, Loc.T("orders.x2"), 44, Color.white, Vector2.zero, new Vector2(220f, 90f));

        var c = card.gameObject.AddComponent<OrderCard>();
        var so = new SerializedObject(c);
        so.FindProperty("bg").objectReferenceValue = frame.GetComponent<Image>();
        so.FindProperty("earLeft").objectReferenceValue = earL.GetComponent<Image>();
        so.FindProperty("earRight").objectReferenceValue = earR.GetComponent<Image>();
        so.FindProperty("nose").objectReferenceValue = noseRt.GetComponent<Image>();
        so.FindProperty("skin").objectReferenceValue = head.GetComponent<Image>();
        so.FindProperty("hair").objectReferenceValue = hairTop.GetComponent<Image>();
        so.FindProperty("cloth").objectReferenceValue = shoulders.GetComponent<Image>();
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("contentText").objectReferenceValue = content;
        so.FindProperty("rewardText").objectReferenceValue = reward;
        so.FindProperty("accept").objectReferenceValue = accept.GetComponent<Button>();
        so.FindProperty("refuse").objectReferenceValue = refuse.GetComponent<Button>();
        so.FindProperty("later").objectReferenceValue = later.GetComponent<Button>();
        so.FindProperty("laterLabel").objectReferenceValue = laterLabel;
        so.ApplyModifiedPropertiesWithoutUndo();
        card.gameObject.SetActive(false);
        return c;
    }

    static RectTransform Sharp(RectTransform rt, float ppum)
    {
        rt.GetComponent<Image>().pixelsPerUnitMultiplier = ppum;
        return rt;
    }

    // Socle plein derrière un élément, placé dedans (premier enfant) : il apparaît et disparaît avec lui.
    static void ChildBase(RectTransform parent, Sprite rounded, Color color, float depth)
    {
        RectTransform b = UpgradeBuilder.Box("Base", parent, rounded, color, new Vector2(0f, -depth), parent.sizeDelta, false);
        b.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        b.SetAsFirstSibling();
    }

    // Texte sombre sur carte crème (le texte blanc contouré de la bible est pour les fonds sombres et colorés).
    static Text InkLabel(string name, Transform parent, int size, Vector2 pos, Vector2 box, TextAnchor anchor)
    {
        Text t = UpgradeBuilder.Label(name, parent, "", size, UiTheme.Ink, pos, box);
        foreach (Outline o in t.GetComponents<Outline>()) Object.DestroyImmediate(o);
        foreach (Shadow s in t.GetComponents<Shadow>()) Object.DestroyImmediate(s);
        t.color = UiTheme.Ink;
        t.alignment = anchor;
        return t;
    }
}
