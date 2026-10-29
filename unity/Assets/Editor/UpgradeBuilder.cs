using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Étape 7b : améliorations (état, bouton rond en bas à droite, panneau). Relançable.
public static class UpgradeBuilder
{
    const string Root = "Assets/Prototype";

    public static UpgradeConfig LoadOrCreateConfig()
    {
        string path = Root + "/UpgradeConfig.asset";
        UpgradeConfig config = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(path);
        if (config != null) return config; // réglages conservés s'il existe

        config = ScriptableObject.CreateInstance<UpgradeConfig>();
        AssetDatabase.CreateAsset(config, path);
        var so = new SerializedObject(config);
        SerializedProperty list = so.FindProperty("upgrades");
        // id, nom, type, coût de base, effet par niveau
        object[][] rows =
        {
            new object[] { "stack", "Capacité de la pile", UpgradeKind.StackCapacity, 60, 1f },
            new object[] { "player_speed", "Vitesse du joueur", UpgradeKind.PlayerSpeed, 80, 0.05f },
            new object[] { "job_speed", "Vitesse de fabrication", UpgradeKind.JobSpeed, 100, 0.07f },
            new object[] { "customer_pay", "Paiement des clients", UpgradeKind.CustomerPay, 120, 0.08f },
            new object[] { "employee_speed", "Vitesse des employés", UpgradeKind.EmployeeSpeed, 140, 0.08f },
        };
        list.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("id").stringValue = (string)rows[i][0];
            e.FindPropertyRelative("displayName").stringValue = (string)rows[i][1];
            e.FindPropertyRelative("kind").enumValueIndex = (int)(UpgradeKind)rows[i][2];
            e.FindPropertyRelative("baseCost").intValue = (int)rows[i][3];
            e.FindPropertyRelative("costGrowth").floatValue = 1.35f;
            e.FindPropertyRelative("maxLevel").intValue = 10;
            e.FindPropertyRelative("effectPerLevel").floatValue = (float)rows[i][4];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return config;
    }

    public static void Build()
    {
        Wallet wallet = Object.FindFirstObjectByType<Wallet>();
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        GameObject canvas = GameObject.Find("Canvas");
        Transform safe = canvas != null ? canvas.transform.Find("HudSafeArea") : null;
        if (wallet == null || player == null || safe == null) return;

        foreach (string old in new[] { "UpgradeState" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) Object.DestroyImmediate(g);
        }
        foreach (string old in new[] { "UpgradeButton", "UpgradePanel", "UpgradeHost" })
        {
            Transform t = safe.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        UpgradeConfig config = LoadOrCreateConfig();
        FeedbackSpawner feedback = Object.FindFirstObjectByType<FeedbackSpawner>();

        GameObject stateGo = new GameObject("UpgradeState", typeof(UpgradeState));
        UpgradeState state = stateGo.GetComponent<UpgradeState>();
        var sso = new SerializedObject(state);
        sso.FindProperty("config").objectReferenceValue = config;
        sso.FindProperty("wallet").objectReferenceValue = wallet;
        sso.ApplyModifiedPropertiesWithoutUndo();

        WireConsumers(state, player, feedback);
        BuildUi(safe, state, wallet, player, feedback, config);
    }

    // Les scripts concernés lisent les multiplicateurs de l'état à l'usage.
    static void WireConsumers(UpgradeState state, PlayerController player, FeedbackSpawner feedback)
    {
        SetRef(player, "upgrades", state);
        SetRef(player.GetComponent<StackCarrier>(), "upgrades", state);

        foreach (InstallStation s in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(s, "upgrades", state);
        foreach (ServiceCounter c in Object.FindObjectsByType<ServiceCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(c, "upgrades", state);
        foreach (Employee e in Object.FindObjectsByType<Employee>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(e, "upgrades", state);
        foreach (Courier c in Object.FindObjectsByType<Courier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(c, "upgrades", state);

        CameraFollow follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        foreach (UnlockPad pad in Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(pad, "cameraFollow", follow);
    }

    static void SetRef(Object target, string field, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ interface

    static void BuildUi(Transform safe, UpgradeState state, Wallet wallet, PlayerController player,
        FeedbackSpawner feedback, UpgradeConfig config)
    {
        Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite rounded = UiSprites.Rounded();

        // Bouton rond en bas à droite (zone sûre)
        RectTransform button = Box("UpgradeButton", safe, circle, new Color(0.95f, 0.78f, 0.15f), Vector2.zero, new Vector2(170f, 170f), true);
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 0f);
        button.anchoredPosition = new Vector2(-40f, 60f);
        Box("Ring", button, circle, new Color(0.15f, 0.12f, 0.03f, 0.9f), Vector2.zero, new Vector2(138f, 138f), false);
        // flèche vers le haut : losange + tige
        Rot(Box("ArrowHead", button, round, Color.white, new Vector2(0f, 16f), new Vector2(54f, 54f), false), 45f);
        Box("ArrowStem", button, round, Color.white, new Vector2(0f, -14f), new Vector2(26f, 44f), false);
        RectTransform dot = Box("Dot", button, circle, new Color(1f, 0.25f, 0.2f), new Vector2(56f, 56f), new Vector2(46f, 46f), false);
        Box("DotRim", dot, circle, new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(24f, 24f), false);

        // Panneau
        GameObject panelGo = new GameObject("UpgradePanel", typeof(RectTransform), typeof(PopIn));
        panelGo.transform.SetParent(safe, false);
        RectTransform panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 20f);
        float rowHeight = 168f;
        int n = config.Upgrades.Length;
        panel.sizeDelta = new Vector2(980f, 190f + rowHeight * n + 30f);
        Image back = panelGo.AddComponent<Image>();
        back.sprite = rounded;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 0.9f;
        back.color = new Color(0.10f, 0.13f, 0.18f, 0.92f);

        Label("Title", panel, "Améliorations", 70, new Color(1f, 0.85f, 0.2f), new Vector2(0f, panel.sizeDelta.y * 0.5f - 85f), new Vector2(700f, 110f));
        RectTransform close = Box("CloseButton", panel, circle, new Color(0.75f, 0.25f, 0.25f), new Vector2(panel.sizeDelta.x * 0.5f - 85f, panel.sizeDelta.y * 0.5f - 85f), new Vector2(90f, 90f), true);
        Rot(Box("X1", close, round, Color.white, Vector2.zero, new Vector2(14f, 52f), false), 45f);
        Rot(Box("X2", close, round, Color.white, Vector2.zero, new Vector2(14f, 52f), false), -45f);

        UpgradeRow[] rows = new UpgradeRow[n];
        for (int i = 0; i < n; i++)
        {
            float y = panel.sizeDelta.y * 0.5f - 190f - rowHeight * (i + 0.5f);
            rows[i] = BuildRow(panel, round, circle, rounded, config.Upgrades[i], new Vector2(0f, y), rowHeight - 14f);
        }

        // Le script vit sur un objet toujours actif (le panneau, lui, est désactivé quand il est fermé).
        GameObject host = new GameObject("UpgradeHost", typeof(RectTransform), typeof(UpgradePanel));
        host.transform.SetParent(safe, false);
        var pso = new SerializedObject(host.GetComponent<UpgradePanel>());
        pso.FindProperty("state").objectReferenceValue = state;
        pso.FindProperty("wallet").objectReferenceValue = wallet;
        pso.FindProperty("panel").objectReferenceValue = panelGo;
        pso.FindProperty("openButton").objectReferenceValue = button.GetComponent<Button>();
        pso.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>();
        pso.FindProperty("openButtonRect").objectReferenceValue = button;
        pso.FindProperty("dot").objectReferenceValue = dot.gameObject;
        pso.FindProperty("feedback").objectReferenceValue = feedback;
        pso.FindProperty("player").objectReferenceValue = player.transform;
        SerializedProperty rowList = pso.FindProperty("rows");
        rowList.arraySize = n;
        for (int i = 0; i < n; i++) rowList.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        pso.ApplyModifiedPropertiesWithoutUndo();
    }

    static UpgradeRow BuildRow(RectTransform panel, Sprite round, Sprite circle, Sprite rounded, UpgradeConfig.Upgrade u, Vector2 pos, float height)
    {
        RectTransform row = Box("Row_" + u.id, panel, rounded, new Color(1f, 1f, 1f, 0.08f), pos, new Vector2(920f, height), false);
        row.GetComponent<Image>().pixelsPerUnitMultiplier = 1.1f;

        BuildIcon(row, circle, round, u.kind);

        Text nameText = Label("Name", row, "", 40, Color.white, new Vector2(-70f, 38f), new Vector2(460f, 56f));
        nameText.alignment = TextAnchor.MiddleLeft;
        Text level = Label("Level", row, "", 36, new Color(1f, 0.85f, 0.2f), new Vector2(-70f, -4f), new Vector2(460f, 46f));
        level.alignment = TextAnchor.MiddleLeft;
        Text effect = Label("Effect", row, "", 32, new Color(0.8f, 0.85f, 0.9f), new Vector2(-70f, -42f), new Vector2(460f, 44f));
        effect.alignment = TextAnchor.MiddleLeft;

        RectTransform buy = Box("Buy", row, rounded, new Color(0.2f, 0.7f, 0.3f), new Vector2(300f, 0f), new Vector2(250f, 110f), true);
        buy.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        Text buyLabel = Label("Text", buy, "$ 0", 54, Color.white, Vector2.zero, new Vector2(250f, 110f));

        UpgradeRow comp = row.gameObject.AddComponent<UpgradeRow>();
        var so = new SerializedObject(comp);
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("levelText").objectReferenceValue = level;
        so.FindProperty("effectText").objectReferenceValue = effect;
        so.FindProperty("buyButton").objectReferenceValue = buy.GetComponent<Button>();
        so.FindProperty("buyLabel").objectReferenceValue = buyLabel;
        so.FindProperty("buyImage").objectReferenceValue = buy.GetComponent<Image>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // Icônes en formes simples : disque coloré + motif blanc selon le type.
    static void BuildIcon(RectTransform row, Sprite circle, Sprite round, UpgradeKind kind)
    {
        Color[] colors =
        {
            new Color(0.72f, 0.5f, 0.28f), new Color(0.25f, 0.55f, 0.95f), new Color(0.95f, 0.6f, 0.15f),
            new Color(0.25f, 0.75f, 0.35f), new Color(0.6f, 0.4f, 0.9f)
        };
        RectTransform icon = Box("Icon", row, circle, colors[(int)kind], new Vector2(-370f, 0f), new Vector2(110f, 110f), false);
        switch (kind)
        {
            case UpgradeKind.StackCapacity: // caisse
                Box("Crate", icon, round, new Color(0.95f, 0.85f, 0.62f), Vector2.zero, new Vector2(56f, 46f), false);
                Box("Tape", icon, round, new Color(0.72f, 0.5f, 0.28f), Vector2.zero, new Vector2(12f, 46f), false);
                break;
            case UpgradeKind.PlayerSpeed: // chevrons
            case UpgradeKind.EmployeeSpeed:
                Rot(Box("Chev1", icon, round, Color.white, new Vector2(-12f, 0f), new Vector2(30f, 30f), false), 45f);
                Rot(Box("Chev2", icon, round, Color.white, new Vector2(14f, 0f), new Vector2(30f, 30f), false), 45f);
                break;
            case UpgradeKind.JobSpeed: // cadran
                Box("Dial", icon, circle, Color.white, Vector2.zero, new Vector2(60f, 60f), false);
                Box("DialIn", icon, circle, colors[(int)kind], Vector2.zero, new Vector2(40f, 40f), false);
                Box("Hand", icon, round, Color.white, new Vector2(0f, 8f), new Vector2(8f, 22f), false);
                break;
            default: // billet
                Box("Bill", icon, round, Color.white, Vector2.zero, new Vector2(64f, 40f), false);
                Box("BillSeal", icon, circle, colors[(int)kind], Vector2.zero, new Vector2(22f, 22f), false);
                break;
        }
    }

    static void Rot(RectTransform rt, float angle)
    {
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    static RectTransform Box(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool button)
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
        img.color = color;
        img.raycastTarget = button;
        if (button)
        {
            Button b = go.AddComponent<Button>();
            b.targetGraphic = img;
        }
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
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return t;
    }
}
