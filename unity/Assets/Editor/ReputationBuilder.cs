using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Arbre Réputation (11d) : données (10 noeuds), état, bouton « Réputation » et panneau. Appelé après les commandes.
public static class ReputationBuilder
{
    const string Root = "Assets/Prototype";

    public static ReputationConfig LoadOrCreateConfig()
    {
        string path = Root + "/ReputationConfig.asset";
        ReputationConfig config = AssetDatabase.LoadAssetAtPath<ReputationConfig>(path);
        if (config != null && config.Nodes != null && config.Nodes.Length > 0) return config;
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ReputationConfig>();
            AssetDatabase.CreateAsset(config, path);
        }

        // id, nom, description, icône, couleur, effet, valeur/niveau, niveau max, coût de base, prérequis, niveau requis, géré
        object[][] rows =
        {
            new object[] { "revenue", "Revenus", "+{0} % de paiements", "$", "3DDC5A", ReputationEffect.Revenue, 10f, 10, 2, "", 0, true },
            new object[] { "chain", "Vitesse de la chaîne", "+{0} % de vitesse", ">>", "27D3E6", ReputationEffect.ChainSpeed, 5f, 10, 3, "", 0, true },
            new object[] { "offline", "Gains hors ligne", "+{0} % hors ligne", "Zz", "A855F7", ReputationEffect.OfflineGain, 20f, 5, 3, "", 0, true },
            new object[] { "offlinecap", "Plafond hors ligne", "+{0} h de plafond", "8h", "7C4DFF", ReputationEffect.OfflineCap, 4f, 1, 6, "offline", 2, true },
            new object[] { "stack", "Capacité de la pile", "+{0} caisse(s)", "+1", "B9824A", ReputationEffect.StackCapacity, 1f, 5, 4, "", 0, true },
            new object[] { "speed", "Vitesse du technicien", "+{0} % de vitesse", "Run", "1FA0FF", ReputationEffect.PlayerSpeed, 4f, 5, 3, "", 0, true },
            new object[] { "patience", "Patience des clients", "+{0} % de patience", ":)", "FFC400", ReputationEffect.OrderPatience, 15f, 4, 2, "", 0, true },
            new object[] { "offers", "Offres de commandes", "+{0} offre affichée", "3+", "FF7A1A", ReputationEffect.OfferSlots, 1f, 1, 5, "", 0, true },
            new object[] { "active", "Commandes en cours", "+{0} en parallèle", "x2", "F2453D", ReputationEffect.ActiveOrders, 1f, 1, 12, "offers", 1, false },
            new object[] { "discount", "Remise sur les dalles", "-{0} % de prix", "%", "3DDC5A", ReputationEffect.PadDiscount, 4f, 5, 4, "", 0, true },
        };
        var so = new SerializedObject(config);
        SerializedProperty list = so.FindProperty("nodes");
        list.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            object[] r = rows[i];
            e.FindPropertyRelative("id").stringValue = (string)r[0];
            e.FindPropertyRelative("displayName").stringValue = (string)r[1];
            e.FindPropertyRelative("description").stringValue = (string)r[2];
            e.FindPropertyRelative("iconText").stringValue = (string)r[3];
            e.FindPropertyRelative("iconColor").colorValue = Hex((string)r[4]);
            e.FindPropertyRelative("effect").enumValueIndex = (int)(ReputationEffect)r[5];
            e.FindPropertyRelative("valuePerLevel").floatValue = (float)r[6];
            e.FindPropertyRelative("maxLevel").intValue = (int)r[7];
            e.FindPropertyRelative("baseCost").intValue = (int)r[8];
            e.FindPropertyRelative("costGrowth").floatValue = 1.6f;
            e.FindPropertyRelative("prerequisiteId").stringValue = (string)r[9];
            e.FindPropertyRelative("prerequisiteLevel").intValue = (int)r[10];
            e.FindPropertyRelative("implemented").boolValue = (bool)r[11];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
        return config;
    }

    static Color Hex(string h)
    {
        return new Color32(System.Convert.ToByte(h.Substring(0, 2), 16), System.Convert.ToByte(h.Substring(2, 2), 16), System.Convert.ToByte(h.Substring(4, 2), 16), 255);
    }

    public static void Build()
    {
        GameObject canvas = GameObject.Find("Canvas");
        Transform safe = canvas != null ? canvas.transform.Find("HudSafeArea") : null;
        ProPoints pro = Object.FindFirstObjectByType<ProPoints>();
        if (safe == null || pro == null) return;

        GameObject oldState = GameObject.Find("ReputationState");
        if (oldState != null) Object.DestroyImmediate(oldState);
        foreach (string old in new[] { "ReputationButton", "ReputationPanel", "ReputationHost", "ReputationBackdrop" })
        {
            Transform t = safe.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        ReputationConfig config = LoadOrCreateConfig();
        var stateGo = new GameObject("ReputationState", typeof(ReputationState));
        var state = stateGo.GetComponent<ReputationState>();
        var sso = new SerializedObject(state);
        sso.FindProperty("config").objectReferenceValue = config;
        sso.FindProperty("pro").objectReferenceValue = pro;
        sso.ApplyModifiedPropertiesWithoutUndo();

        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite rounded = UiSprites.Rounded();

        // bouton carré orange à étoile sous « Commandes », pastille rouge quand un achat est possible
        GameObject btnGo = new GameObject("ReputationButton", typeof(RectTransform), typeof(Button));
        btnGo.transform.SetParent(safe, false);
        RectTransform button = btnGo.GetComponent<RectTransform>();
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-40f, -890f);
        button.sizeDelta = new Vector2(150f, 150f);
        RectTransform baseRt = UpgradeBuilder.Box("Base", button, rounded, new Color32(0xC2, 0x53, 0x0A, 0xFF), new Vector2(0f, -14f), new Vector2(150f, 150f), false);
        baseRt.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        RectTransform face = UpgradeBuilder.Box("Face", button, rounded, UiTheme.Orange, Vector2.zero, new Vector2(150f, 150f), true);
        face.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        btnGo.GetComponent<Button>().targetGraphic = face.GetComponent<Image>();
        Object.DestroyImmediate(face.GetComponent<Button>());
        RectTransform starImg = UpgradeBuilder.Box("Star", face, UiSprites.Star(), Color.white, Vector2.zero, new Vector2(96f, 96f), false);
        starImg.GetComponent<Image>().type = Image.Type.Simple;
        starImg.GetComponent<Image>().preserveAspect = true;
        RectTransform dot = UpgradeBuilder.Box("Dot", button, circle, UiTheme.Red, new Vector2(62f, 62f), new Vector2(60f, 60f), false);
        UpgradeBuilder.Box("DotRim", dot, circle, new Color(1f, 1f, 1f, 0.95f), Vector2.zero, new Vector2(60f, 60f), false).SetAsFirstSibling();
        UpgradeBuilder.Box("DotIn", dot, circle, UiTheme.Red, Vector2.zero, new Vector2(50f, 50f), false);
        UpgradeBuilder.Label("Mark", dot, "!", 38, Color.white, Vector2.zero, new Vector2(60f, 60f));
        btnGo.AddComponent<PopIn>();

        // fond sombre cliquable + panneau orange
        GameObject backdropGo = new GameObject("ReputationBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropGo.transform.SetParent(safe, false);
        var bdRt = backdropGo.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one; bdRt.offsetMin = bdRt.offsetMax = Vector2.zero;
        var bdImg = backdropGo.GetComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.6f);
        backdropGo.GetComponent<Button>().targetGraphic = bdImg;
        backdropGo.SetActive(false);

        int n = config.Nodes.Length;
        const float rowH = 118f;
        float height = 190f + rowH * n + 40f;
        GameObject panelGo = new GameObject("ReputationPanel", typeof(RectTransform), typeof(PopIn));
        panelGo.transform.SetParent(safe, false);
        RectTransform panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 10f);
        panel.sizeDelta = new Vector2(980f, height);
        Image back = panelGo.AddComponent<Image>();
        back.sprite = rounded;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 0.7f;
        back.color = UiTheme.Orange;

        float top = height * 0.5f;
        RectTransform header = UpgradeBuilder.Box("Header", panel, rounded, UiTheme.Slate, new Vector2(0f, top - 85f), new Vector2(940f, 120f), false);
        header.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Title", header, Loc.T("rep.title"), 70, Color.white, new Vector2(40f, 0f), new Vector2(560f, 110f));
        // solde de Points Pro : pastille cyan à étoile dans le bandeau
        RectTransform bal = UpgradeBuilder.Box("Balance", header, rounded, new Color32(0x1B, 0xA8, 0xBB, 0xFF), new Vector2(-330f, 0f), new Vector2(220f, 80f), false);
        bal.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        RectTransform bstar = UpgradeBuilder.Box("Star", bal, UiSprites.Star(), Color.white, new Vector2(-66f, 0f), new Vector2(56f, 56f), false);
        bstar.GetComponent<Image>().type = Image.Type.Simple;
        bstar.GetComponent<Image>().preserveAspect = true;
        Text balance = UpgradeBuilder.Label("Value", bal, "0", 52, Color.white, new Vector2(34f, 0f), new Vector2(130f, 76f));
        UpgradeBuilder.Box("CloseRim", panel, circle, Color.white, new Vector2(418f, top - 85f), new Vector2(112f, 112f), false);
        RectTransform close = UpgradeBuilder.Box("CloseButton", panel, circle, UiTheme.Red, new Vector2(418f, top - 85f), new Vector2(100f, 100f), true);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X1", close, rounded, Color.white, Vector2.zero, new Vector2(14f, 54f), false), 45f);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X2", close, rounded, Color.white, Vector2.zero, new Vector2(14f, 54f), false), -45f);

        var rows = new ReputationRow[n];
        for (int i = 0; i < n; i++)
            rows[i] = BuildRow(panel, rounded, circle, new Vector2(0f, top - 190f - rowH * (i + 0.5f)), rowH - 10f);

        GameObject host = new GameObject("ReputationHost", typeof(RectTransform), typeof(ReputationUi));
        host.transform.SetParent(safe, false);
        var ui = host.GetComponent<ReputationUi>();
        var so = new SerializedObject(ui);
        so.FindProperty("state").objectReferenceValue = state;
        so.FindProperty("pro").objectReferenceValue = pro;
        so.FindProperty("openButton").objectReferenceValue = btnGo.GetComponent<Button>();
        so.FindProperty("badge").objectReferenceValue = dot.gameObject;
        so.FindProperty("panel").objectReferenceValue = panelGo;
        so.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>();
        so.FindProperty("backdrop").objectReferenceValue = backdropGo.GetComponent<Button>();
        so.FindProperty("balanceText").objectReferenceValue = balance;
        so.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        SerializedProperty hn = so.FindProperty("hideNames");
        string[] names = { "UpgradeButton", "OrdersButton", "ReputationButton", "GuideBar", "OrderBar" };
        hn.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++) hn.GetArrayElementAtIndex(i).stringValue = names[i];
        SerializedProperty rl = so.FindProperty("rows");
        rl.arraySize = n;
        for (int i = 0; i < n; i++) rl.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static ReputationRow BuildRow(RectTransform panel, Sprite rounded, Sprite circle, Vector2 pos, float height)
    {
        RectTransform card = UpgradeBuilder.Box("Row", panel, rounded, new Color32(0xFF, 0xF1, 0xD6, 0xFF), pos, new Vector2(920f, height), false);
        card.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        RectTransform icon = UpgradeBuilder.Box("Icon", card, circle, UiTheme.Green, new Vector2(-400f, 0f), new Vector2(84f, 84f), false);
        Text iconText = UpgradeBuilder.Label("Symbol", icon, "$", 36, Color.white, Vector2.zero, new Vector2(84f, 84f));
        Text nameText = Ink("Name", card, 36, new Vector2(-60f, 24f), new Vector2(560f, 46f));
        Text effect = Ink("Effect", card, 28, new Vector2(-60f, -20f), new Vector2(560f, 40f));
        effect.color = new Color32(0x2B, 0x6F, 0x3A, 0xFF);
        Text level = Ink("Level", card, 34, new Vector2(150f, 24f), new Vector2(120f, 46f));
        level.alignment = TextAnchor.MiddleRight;

        RectTransform buy = UpgradeBuilder.Box("Buy", card, rounded, UiTheme.Green, new Vector2(335f, 0f), new Vector2(230f, 84f), true);
        buy.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        RectTransform star = UpgradeBuilder.Box("Star", buy, UiSprites.Star(), new Color32(0x27, 0xD3, 0xE6, 0xFF), new Vector2(-70f, 0f), new Vector2(44f, 44f), false);
        star.GetComponent<Image>().type = Image.Type.Simple;
        star.GetComponent<Image>().preserveAspect = true;
        Text buyText = UpgradeBuilder.Label("Price", buy, "0", 44, Color.white, new Vector2(18f, 0f), new Vector2(170f, 84f));

        var row = card.gameObject.AddComponent<ReputationRow>();
        var so = new SerializedObject(row);
        so.FindProperty("card").objectReferenceValue = card.GetComponent<Image>();
        so.FindProperty("iconBack").objectReferenceValue = icon.GetComponent<Image>();
        so.FindProperty("iconText").objectReferenceValue = iconText;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("effectText").objectReferenceValue = effect;
        so.FindProperty("levelText").objectReferenceValue = level;
        so.FindProperty("buy").objectReferenceValue = buy.GetComponent<Button>();
        so.FindProperty("buyBack").objectReferenceValue = buy.GetComponent<Image>();
        so.FindProperty("buyText").objectReferenceValue = buyText;
        so.FindProperty("star").objectReferenceValue = star.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    static Text Ink(string name, Transform parent, int size, Vector2 pos, Vector2 box)
    {
        Text t = UpgradeBuilder.Label(name, parent, "", size, UiTheme.Ink, pos, box);
        foreach (Outline o in t.GetComponents<Outline>()) Object.DestroyImmediate(o);
        foreach (Shadow s in t.GetComponents<Shadow>()) Object.DestroyImmediate(s);
        t.color = UiTheme.Ink;
        t.alignment = TextAnchor.MiddleLeft;
        return t;
    }
}
