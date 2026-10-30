using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static IdleHudBuilder;
using static IdleDrawerBuilder;

// Construit les fenêtres (U3c) : Commandes, Réputation, Réglages, Bon retour, plus le bandeau des commandes en cours, les messages et les puces.
// Mesures du web : `Px` convertit les pixels CSS (écran 390 de large) en unités du Canvas.
public static class IdleWindowsBuilder
{
    const string ConfigPath = "Assets/Prototype/IdleEconomyConfig.asset";
    const float BottomOverlap = 30f, CardHeight = 166f, CardPitch = 176f;
    const int OrderCards = 6;       // 3 commandes en cours au plus (Réputation) + 3 offres

    static readonly Color Orange = new Color32(0xFF, 0x7A, 0x1A, 0xFF), OrangeTop = new Color32(0xF2, 0x8A, 0x2E, 0xFF), OrangeEdge = new Color32(0xD9, 0x5E, 0x00, 0xFF);
    static readonly Color Cream = new Color32(0xF6, 0xEF, 0xE2, 0xFF), Sand = new Color32(0xE8, 0xDC, 0xC3, 0xFF), Green2 = new Color32(0x1F, 0x9D, 0x3A, 0xFF);
    static readonly Color Teal = new Color32(0x0F, 0x8C, 0x9B, 0xFF), TealTile = new Color32(0x0F, 0x6E, 0x7A, 0xFF), Say = new Color32(0x6F, 0x62, 0x47, 0xFF), Hint = new Color32(0x7A, 0x6A, 0x4C, 0xFF);
    static readonly Color RedTop = new Color32(0xFF, 0x6F, 0x68, 0xFF), RedEdge = new Color32(0xB8, 0x24, 0x1E, 0xFF);

    static readonly string[] Portraits = { "karim", "lea", "marc", "ines", "paul", "sofia", "hugo", "nadia" };
    static readonly string[] RepIcons = { "billet", "eclair", "plus", "commandes", "commandes", "Ico_snd", "flamme" };

    public struct Result
    {
        public IdleSheet orders, reputation, settings;
    }

    public static Result Build(Transform canvas, RectTransform safe, RectTransform drawerPanel, RectTransform cashPill, RectTransform proPill)
    {
        var result = new Result();
        result.orders = BuildOrders(canvas);
        RectTransform strip = BuildStrip(safe, result.orders);
        result.reputation = BuildReputation(canvas);
        result.settings = BuildSettings(canvas);

        // toujours au-dessus des fenêtres : Bon retour, messages, puces
        IdleChips chips = null;
        RectTransform welcomeHost = NewRect("Welcome", canvas);
        Stretch(welcomeHost);
        RectTransform toastsHost = NewRect("Toasts", canvas);
        Stretch(toastsHost);
        RectTransform chipsHost = NewRect("Chips", canvas);
        Stretch(chipsHost);
        chips = BuildChips(chipsHost, cashPill, proPill);
        BuildWelcome(welcomeHost, chips);
        IdleToasts toasts = BuildToasts(toastsHost, drawerPanel);

        var feedback = toastsHost.gameObject.AddComponent<IdleOrderFeedback>();
        var so = new SerializedObject(feedback);
        so.FindProperty("toasts").objectReferenceValue = toasts;
        so.FindProperty("chips").objectReferenceValue = chips;
        so.FindProperty("chipOrigin").objectReferenceValue = strip;
        so.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        var views = new IdleStationView[5];
        foreach (IdleStationView v in Object.FindObjectsByType<IdleStationView>(FindObjectsSortMode.None))
            views[new SerializedObject(v).FindProperty("station").intValue] = v;
        SerializedProperty stations = so.FindProperty("stations");
        stations.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++) stations.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return result;
    }

    // ------------------------------------------------------------------ feuille commune

    struct Sheet
    {
        public IdleSheet script;
        public RectTransform sheet, content;
    }

    static Sheet NewSheet(Transform canvas, string name, string title, float headerCss)
    {
        RectTransform root = NewRect(name, canvas);
        Stretch(root);

        Image dim = Rounded("Dim", root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.55f), 1f);
        dim.type = Image.Type.Simple;
        dim.sprite = null;
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        Image bg = Rounded("Sheet", root, Color.white, Px(28f));
        RectTransform sheet = bg.rectTransform;
        sheet.anchorMin = Vector2.zero;
        sheet.anchorMax = new Vector2(1f, 0f);
        sheet.pivot = new Vector2(0.5f, 0f);
        sheet.sizeDelta = new Vector2(0f, ReferenceHeight * 0.8f);
        sheet.anchoredPosition = new Vector2(0f, -Px(BottomOverlap));
        bg.gameObject.AddComponent<VerticalGradient>().Set(OrangeTop, Orange);
        AddEdge(bg, OrangeEdge, Px(6f), true);
        var group = bg.gameObject.AddComponent<CanvasGroup>();

        Text titleText = AddText(sheet, "Title", title, 30f, Color.white, TextAnchor.MiddleCenter, 2f);
        TopStretch(titleText.rectTransform, 0f, 0f, Px(14f), Px(38f));

        RectTransform body = NewRect("Body", sheet);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(Px(14f), Px(BottomOverlap + 18f));
        body.offsetMax = new Vector2(-Px(14f), -Px(headerCss));
        var catcher = body.gameObject.AddComponent<Image>();
        catcher.color = Color.clear;
        body.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect("Content", body);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = body;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = Px(20f);

        // croix rouge en haut à droite de l'écran, dans la zone sûre
        RectTransform safe = NewRect("Safe", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        Button close = ColorButton(safe, "Close", Px(40f), Px(40f), Px(12f), RedTop, UiTheme.Red, RedEdge);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-Px(16f), -Px(14f));
        Image cross = AddIcon(closeRect, "Icon", "Ico_x", 22f, false);
        Center(cross.rectTransform, Vector2.zero);

        var script = root.gameObject.AddComponent<IdleSheet>();
        var so = new SerializedObject(script);
        so.FindProperty("sheet").objectReferenceValue = sheet;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("dim").objectReferenceValue = dim.GetComponent<Button>();
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("headerHeight").floatValue = Px(headerCss);
        so.FindProperty("bottomPadding").floatValue = Px(18f);
        so.FindProperty("overlap").floatValue = Px(BottomOverlap);
        so.FindProperty("slide").floatValue = Px(40f);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return new Sheet { script = script, sheet = sheet, content = content };
    }

    // Bouton plein à dégradé et épaisseur (croix, Refuser, Collecter…), sans logique d'achat.
    static Button ColorButton(Transform parent, string name, float width, float height, float radius, Color top, Color bottom, Color edge)
    {
        Image face = Rounded(name, parent, Color.white, radius);
        face.rectTransform.sizeDelta = new Vector2(width, height);
        face.gameObject.AddComponent<VerticalGradient>().Set(top, bottom);
        AddEdge(face, edge, Px(4f), false);
        var shadow = face.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.28f);
        shadow.effectDistance = new Vector2(0f, -Px(2f));
        var button = face.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        face.gameObject.AddComponent<PressScale>();
        return button;
    }

    static RectTransform CardAt(RectTransform parent, string name, float top, float height, Color color)
    {
        Image card = Rounded(name, parent, color, Px(18f));
        card.raycastTarget = false;
        AddShadow(card, 0.28f, Px(4f));
        TopStretch(card.rectTransform, Px(2f), Px(2f), top, Px(height));
        return card.rectTransform;
    }

    // ------------------------------------------------------------------ Commandes

    static IdleSheet BuildOrders(Transform canvas)
    {
        Sheet s = NewSheet(canvas, "OrdersSheet", Loc.T("orders.title"), 86f);
        Text sub = AddText(s.sheet, "Sub", Loc.T("orders.sub"), 15f, Color.white, TextAnchor.MiddleCenter, 1f);
        TopStretch(sub.rectTransform, 0f, 0f, Px(52f), Px(22f));

        var window = s.script.gameObject.AddComponent<IdleOrdersWindow>();
        var so = new SerializedObject(window);
        so.FindProperty("sheet").objectReferenceValue = s.script;
        so.FindProperty("content").objectReferenceValue = s.content;
        so.FindProperty("cardPitch").floatValue = Px(CardPitch);
        so.FindProperty("topPadding").floatValue = Px(2f);

        SerializedProperty faces = so.FindProperty("portraits");
        faces.arraySize = Portraits.Length;
        for (int i = 0; i < Portraits.Length; i++) faces.GetArrayElementAtIndex(i).objectReferenceValue = Ui("portrait_" + Portraits[i]);

        SerializedProperty cards = so.FindProperty("cards");
        cards.arraySize = OrderCards;
        for (int i = 0; i < OrderCards; i++) cards.GetArrayElementAtIndex(i).objectReferenceValue = BuildOrderCard(s.content, i);

        RectTransform empty = CardAt(s.content, "Empty", Px(2f), 60f, Cream);
        Text emptyText = AddPlainText(empty, "Text", Loc.T("orders.empty"), 16f, Hint, TextAnchor.MiddleCenter);
        Stretch(emptyText.rectTransform);
        empty.gameObject.SetActive(false);
        so.FindProperty("emptyCard").objectReferenceValue = empty.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return s.script;
    }

    static IdleOrderCard BuildOrderCard(RectTransform content, int index)
    {
        RectTransform card = CardAt(content, "Order" + index, Px(2f + index * CardPitch), CardHeight, Cream);
        var script = card.gameObject.AddComponent<IdleOrderCard>();
        var so = new SerializedObject(script);

        // portrait à coins arrondis : masque arrondi, image dedans
        Image maskImage = Rounded("Face", card, Color.white, Px(16f));
        maskImage.raycastTarget = false;
        RectTransform faceRect = maskImage.rectTransform;
        Corner(faceRect, new Vector2(0f, 1f), new Vector2(Px(10f), -Px(10f)));
        faceRect.sizeDelta = new Vector2(Px(70f), Px(70f));
        maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform pictureRect = NewRect("Picture", faceRect);
        Stretch(pictureRect);
        var picture = pictureRect.gameObject.AddComponent<Image>();
        picture.raycastTarget = false;

        const float Left = 92f;
        Text who = AddPlainText(card, "Name", "", 18f, UiTheme.Ink, TextAnchor.MiddleLeft);
        TopStretch(who.rectTransform, Px(Left), Px(10f), Px(9f), Px(22f));
        Text say = AddPlainText(card, "Say", "", 13f, Say, TextAnchor.MiddleLeft);
        TopStretch(say.rectTransform, Px(Left), Px(10f), Px(32f), Px(16f));

        // encadré : liseré sable, fond blanc
        Image border = Rounded("Need", card, Sand, Px(12f));
        border.raycastTarget = false;
        TopStretch(border.rectTransform, Px(Left), Px(10f), Px(52f), Px(48f));
        Image white = Rounded("Inner", border.transform, Color.white, Px(10f));
        white.raycastTarget = false;
        Stretch(white.rectTransform);
        white.rectTransform.offsetMin = new Vector2(Px(2f), Px(2f));
        white.rectTransform.offsetMax = new Vector2(-Px(2f), -Px(2f));
        RectTransform box = border.rectTransform;

        Text need = AddPlainText(box, "Text", "", 15f, UiTheme.Ink, TextAnchor.MiddleLeft);
        TopStretch(need.rectTransform, Px(9f), Px(8f), Px(5f), Px(18f));
        Image bill = AddIcon(box, "Bill", "billet", 18f, false);
        Corner(bill.rectTransform, new Vector2(0f, 1f), new Vector2(Px(8f), -Px(25f)));
        Text money = AddPlainText(box, "Money", "", 15f, Green2, TextAnchor.MiddleLeft);
        TopStretch(money.rectTransform, Px(29f), Px(120f), Px(25f), Px(18f));
        Image star = AddIcon(box, "Star", "etoile", 18f, false);
        Corner(star.rectTransform, new Vector2(0f, 1f), new Vector2(Px(92f), -Px(25f)));
        Text pro = AddPlainText(box, "Pro", "", 15f, Teal, TextAnchor.MiddleLeft);
        TopStretch(pro.rectTransform, Px(113f), Px(8f), Px(25f), Px(18f));

        // commande en cours : barre dans l'encadré et texte à la place des boutons
        RectTransform progress = NewRect("Progress", card);
        Stretch(progress);
        Image track = Rounded("Track", box, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.15f), Px(4f));
        track.raycastTarget = false;
        TopStretch(track.rectTransform, Px(9f), Px(9f), Px(46f), Px(8f));
        Image fill = Rounded("Fill", track.transform, UiTheme.Green, Px(4f));
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        Text progressText = AddPlainText(progress, "Text", "", 16f, Green2, TextAnchor.MiddleRight);
        progressText.rectTransform.anchorMin = new Vector2(0f, 0f);
        progressText.rectTransform.anchorMax = new Vector2(1f, 0f);
        progressText.rectTransform.pivot = new Vector2(0.5f, 0f);
        progressText.rectTransform.offsetMin = new Vector2(Px(10f), Px(16f));
        progressText.rectTransform.offsetMax = new Vector2(-Px(10f), Px(40f));

        // offre : Refuser, Accepter
        RectTransform actions = NewRect("Actions", card);
        Stretch(actions);
        IdleBuyButton accept = BuyButton(actions, "Accept", Px(104f), Px(46f));
        RectTransform acceptRect = (RectTransform)accept.transform;
        acceptRect.anchorMin = acceptRect.anchorMax = new Vector2(1f, 0f);
        acceptRect.pivot = new Vector2(1f, 0f);
        acceptRect.anchoredPosition = new Vector2(-Px(10f), Px(10f));
        Text acceptLabel = AddText(acceptRect.Find("Visual"), "Label", "", 18f, Color.white, TextAnchor.MiddleCenter);
        Stretch(acceptLabel.rectTransform);
        var acceptSo = new SerializedObject(accept);
        acceptSo.FindProperty("labelText").objectReferenceValue = acceptLabel;
        acceptSo.ApplyModifiedPropertiesWithoutUndo();

        Button refuse = ColorButton(actions, "Refuse", Px(74f), Px(40f), Px(13f), RedTop, UiTheme.Red, RedEdge);
        var refuseRect = (RectTransform)refuse.transform;
        refuseRect.anchorMin = refuseRect.anchorMax = new Vector2(1f, 0f);
        refuseRect.pivot = new Vector2(1f, 0f);
        refuseRect.anchoredPosition = new Vector2(-Px(122f), Px(12f));
        Text refuseLabel = AddText(refuseRect, "Label", Loc.T("orders.refuse"), 15f, Color.white, TextAnchor.MiddleCenter);
        Stretch(refuseLabel.rectTransform);

        so.FindProperty("portrait").objectReferenceValue = picture;
        so.FindProperty("nameText").objectReferenceValue = who;
        so.FindProperty("sayText").objectReferenceValue = say;
        so.FindProperty("needText").objectReferenceValue = need;
        so.FindProperty("moneyText").objectReferenceValue = money;
        so.FindProperty("proText").objectReferenceValue = pro;
        so.FindProperty("needBox").objectReferenceValue = box;
        so.FindProperty("offerBoxHeight").floatValue = Px(48f);
        so.FindProperty("activeBoxHeight").floatValue = Px(62f);
        so.FindProperty("progressRoot").objectReferenceValue = progress.gameObject;
        so.FindProperty("progressBar").objectReferenceValue = track.gameObject;
        so.FindProperty("progressFill").objectReferenceValue = fill.rectTransform;
        so.FindProperty("progressText").objectReferenceValue = progressText;
        so.FindProperty("actions").objectReferenceValue = actions.gameObject;
        so.FindProperty("refuseButton").objectReferenceValue = refuse;
        so.FindProperty("acceptButton").objectReferenceValue = accept;
        so.ApplyModifiedPropertiesWithoutUndo();
        card.gameObject.SetActive(false);
        return script;
    }

    // ------------------------------------------------------------------ bandeau des commandes en cours

    static RectTransform BuildStrip(RectTransform safe, IdleSheet orders)
    {
        RectTransform strip = NewRect("OrderStrip", safe);
        TopStretch(strip, Px(14f), Px(14f + 72f), Px(60f), Px(150f));
        var script = strip.gameObject.AddComponent<IdleOrderStrip>();
        var so = new SerializedObject(script);
        so.FindProperty("ordersSheet").objectReferenceValue = orders;
        SerializedProperty lines = so.FindProperty("lines");
        lines.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            Image bg = Rounded("Line" + i, strip, new Color(UiTheme.Slate.r, UiTheme.Slate.g, UiTheme.Slate.b, 0.92f), Px(14f));
            AddShadow(bg, 0.4f, Px(3f));
            TopStretch(bg.rectTransform, 0f, 0f, Px(i * 50f), Px(44f));
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<PressScale>();

            Text title = AddText(bg.transform, "Title", "", 14f, Color.white, TextAnchor.MiddleLeft, 1f);
            TopStretch(title.rectTransform, Px(10f), Px(70f), Px(5f), Px(18f));
            Text count = AddText(bg.transform, "Count", "", 14f, Color.white, TextAnchor.MiddleRight, 1f);
            TopStretch(count.rectTransform, Px(10f), Px(10f), Px(5f), Px(18f));
            Image track = Rounded("Track", bg.transform, new Color(1f, 1f, 1f, 0.18f), Px(4f));
            track.raycastTarget = false;
            TopStretch(track.rectTransform, Px(10f), Px(10f), Px(28f), Px(8f));
            Image fill = Rounded("Fill", track.transform, UiTheme.Green, Px(4f));
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("root").objectReferenceValue = bg.gameObject;
            line.FindPropertyRelative("button").objectReferenceValue = button;
            line.FindPropertyRelative("title").objectReferenceValue = title;
            line.FindPropertyRelative("count").objectReferenceValue = count;
            line.FindPropertyRelative("fill").objectReferenceValue = fill.rectTransform;
            bg.gameObject.SetActive(false);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return strip;
    }

    // ------------------------------------------------------------------ Réputation

    static IdleSheet BuildReputation(Transform canvas)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<IdleEconomyConfig>(ConfigPath);
        Sheet s = NewSheet(canvas, "ReputationSheet", Loc.T("rep.title"), 86f);

        RectTransform subArea = NewRect("Sub", s.sheet);
        TopStretch(subArea, 0f, 0f, Px(52f), Px(22f));
        var row = subArea.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = Px(5f);
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        AddIcon(subArea, "Star", "etoile", 20f, true);
        Text points = AddText(subArea, "Points", "", 15f, Color.white, TextAnchor.MiddleCenter, 1f);

        var window = s.script.gameObject.AddComponent<IdleRepWindow>();
        var so = new SerializedObject(window);
        so.FindProperty("sheet").objectReferenceValue = s.script;
        so.FindProperty("pointsText").objectReferenceValue = points;
        SerializedProperty cards = so.FindProperty("cards");
        cards.arraySize = cfg.reputation.Length;

        float y = 2f;
        for (int i = 0; i < cfg.reputation.Length; i++)
        {
            string id = cfg.reputation[i].id;
            string desc = Loc.T("repnode." + id + ".desc");
            bool tall = desc.Length > 34;                  // la description tient alors sur 2 lignes
            float height = tall ? 86f : 70f;
            RectTransform card = CardAt(s.content, "Rep" + i, Px(y), height, Cream);

            Image tile = Rounded("Tile", card, TealTile, Px(14f));
            tile.raycastTarget = false;
            RectTransform t = tile.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
            t.pivot = new Vector2(0f, 0.5f);
            t.sizeDelta = new Vector2(Px(48f), Px(48f));
            t.anchoredPosition = new Vector2(Px(10f), 0f);
            Image ico = AddIcon(t, "Icon", RepIcons[i], 36f, false);
            Center(ico.rectTransform, Vector2.zero);

            Text name = AddPlainText(card, "Name", Loc.T("repnode." + id + ".name"), 17f, UiTheme.Ink, TextAnchor.MiddleLeft);
            TopStretch(name.rectTransform, Px(68f), Px(92f), Px(7f), Px(20f));
            Text ds = AddPlainText(card, "Desc", desc, 13f, Hint, TextAnchor.UpperLeft);
            TopStretch(ds.rectTransform, Px(68f), Px(92f), Px(27f), Px(tall ? 32f : 16f));
            ds.horizontalOverflow = HorizontalWrapMode.Wrap;
            ds.lineSpacing = 0.9f;
            Text level = AddPlainText(card, "Level", "", 12.5f, Teal, TextAnchor.MiddleLeft);
            TopStretch(level.rectTransform, Px(68f), Px(92f), Px(height - 22f), Px(16f));

            IdleBuyButton buy = BuyButton(card, "Buy", Px(72f), Px(46f));
            RectTransform b = (RectTransform)buy.transform;
            b.anchorMin = b.anchorMax = new Vector2(1f, 0.5f);
            b.pivot = new Vector2(1f, 0.5f);
            b.anchoredPosition = new Vector2(-Px(10f), Px(1f));
            Transform visual = b.Find("Visual");
            RectTransform priceRow = CenteredRow(visual, "PriceRow", Px(11f), Px(24f), 4f);
            Image star = AddIcon(priceRow, "Star", "etoile", 22f, true);
            Text price = AddText(priceRow, "Price", "0", 18f, Color.white, TextAnchor.MiddleCenter);
            var bso = new SerializedObject(buy);
            bso.FindProperty("priceText").objectReferenceValue = price;
            bso.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty c = cards.GetArrayElementAtIndex(i);
            c.FindPropertyRelative("level").objectReferenceValue = level;
            c.FindPropertyRelative("buy").objectReferenceValue = buy;
            c.FindPropertyRelative("starIcon").objectReferenceValue = star.gameObject;
            y += height + 9f;
        }
        s.content.sizeDelta = new Vector2(0f, Px(y));
        so.FindProperty("contentHeight").floatValue = Px(y);
        so.ApplyModifiedPropertiesWithoutUndo();
        return s.script;
    }

    // ------------------------------------------------------------------ Réglages

    static IdleSheet BuildSettings(Transform canvas)
    {
        Sheet s = NewSheet(canvas, "SettingsSheet", Loc.T("settings.title.big"), 62f);
        var window = s.script.gameObject.AddComponent<IdleSettingsWindow>();
        var so = new SerializedObject(window);
        so.FindProperty("sheet").objectReferenceValue = s.script;

        so.FindProperty("soundToggle").objectReferenceValue = ToggleCard(s.content, "Sound", Loc.T("settings.sound"), 2f);
        so.FindProperty("motionToggle").objectReferenceValue = ToggleCard(s.content, "Motion", Loc.T("settings.effects"), 70f);

        RectTransform erase = CardAt(s.content, "Erase", Px(138f), 58f, Cream);
        Text label = AddPlainText(erase, "Label", Loc.T("settings.erase.game"), 18f, UiTheme.Ink, TextAnchor.MiddleLeft);
        TopStretch(label.rectTransform, Px(14f), Px(160f), Px(16f), Px(26f));
        Button eraseButton = ColorButton(erase, "Button", Px(148f), Px(38f), Px(13f), RedTop, UiTheme.Red, RedEdge);
        var eb = (RectTransform)eraseButton.transform;
        eb.anchorMin = eb.anchorMax = new Vector2(1f, 0.5f);
        eb.pivot = new Vector2(1f, 0.5f);
        eb.anchoredPosition = new Vector2(-Px(10f), Px(1f));
        Text eraseLabel = AddText(eb, "Label", "", 15f, Color.white, TextAnchor.MiddleCenter);
        Stretch(eraseLabel.rectTransform);
        so.FindProperty("eraseButton").objectReferenceValue = eraseButton;
        so.FindProperty("eraseLabel").objectReferenceValue = eraseLabel;

        Text version = AddText(s.content, "Version", "", 13f, Color.white, TextAnchor.MiddleCenter, 1f);
        TopStretch(version.rectTransform, 0f, 0f, Px(206f), Px(20f));
        so.FindProperty("versionText").objectReferenceValue = version;

        s.content.sizeDelta = new Vector2(0f, Px(230f));
        so.FindProperty("contentHeight").floatValue = Px(230f);
        so.FindProperty("knobTravel").floatValue = Px(13f);
        so.ApplyModifiedPropertiesWithoutUndo();
        return s.script;
    }

    static Button ToggleCard(RectTransform content, string name, string label, float top)
    {
        RectTransform card = CardAt(content, name, Px(top), 58f, Cream);
        Text text = AddPlainText(card, "Label", label, 18f, UiTheme.Ink, TextAnchor.MiddleLeft);
        TopStretch(text.rectTransform, Px(14f), Px(80f), Px(16f), Px(26f));

        Image track = Rounded("Toggle", card, UiTheme.Green, Px(16f));
        RectTransform t = track.rectTransform;
        t.anchorMin = t.anchorMax = new Vector2(1f, 0.5f);
        t.pivot = new Vector2(1f, 0.5f);
        t.sizeDelta = new Vector2(Px(58f), Px(32f));
        t.anchoredPosition = new Vector2(-Px(12f), 0f);
        track.raycastPadding = new Vector4(-Px(6f), -Px(6f), -Px(6f), -Px(6f));   // zone tactile de 122 unités au moins
        var button = track.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        track.gameObject.AddComponent<PressScale>();

        Image knob = Rounded("Knob", t, Color.white, Px(12f));
        knob.raycastTarget = false;
        knob.rectTransform.sizeDelta = new Vector2(Px(24f), Px(24f));
        Center(knob.rectTransform, Vector2.zero);
        AddShadow(knob, 0.3f, Px(2f));
        return button;
    }

    // ------------------------------------------------------------------ Bon retour

    static void BuildWelcome(RectTransform host, IdleChips chips)
    {
        RectTransform root = NewRect("Root", host);
        Stretch(root);
        Image dim = Rounded("Dim", root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.62f), 1f);
        dim.type = Image.Type.Simple;
        dim.sprite = null;
        Stretch(dim.rectTransform);

        Image card = Rounded("Card", root, Cream, Px(20f));
        RectTransform rt = card.rectTransform;
        rt.sizeDelta = new Vector2(Px(340f), Px(198f));
        Center(rt, Px(40f) * Vector2.up);
        card.gameObject.AddComponent<PopIn>();

        Text title = AddText(rt, "Title", Loc.T("offline.title"), 28f, Color.white, TextAnchor.MiddleCenter, 2f);
        TopStretch(title.rectTransform, 0f, 0f, Px(18f), Px(36f));
        Text duration = AddPlainText(rt, "Duration", "", 15f, Say, TextAnchor.MiddleCenter);
        TopStretch(duration.rectTransform, Px(10f), Px(10f), Px(58f), Px(20f));

        RectTransform big = CenteredRow(rt, "Gain", Px(84f), Px(52f), 6f);
        AddIcon(big, "Bill", "billet", 46f, true);
        Text gain = AddPlainText(big, "Value", "", 40f, Green2, TextAnchor.MiddleCenter);

        Button collect = ColorButton(rt, "Collect", Px(104f), Px(46f), Px(14f), new Color32(0x6B, 0xF0, 0x83, 0xFF), UiTheme.Green, Green2);
        var c = (RectTransform)collect.transform;
        c.anchorMin = c.anchorMax = new Vector2(0.5f, 0f);
        c.pivot = new Vector2(0.5f, 0f);
        c.anchoredPosition = new Vector2(0f, Px(14f));
        Text label = AddText(c, "Label", Loc.T("welcome.collect"), 18f, Color.white, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);

        var window = host.gameObject.AddComponent<IdleWelcomeWindow>();
        var so = new SerializedObject(window);
        so.FindProperty("root").objectReferenceValue = root.gameObject;
        so.FindProperty("durationText").objectReferenceValue = duration;
        so.FindProperty("gainText").objectReferenceValue = gain;
        so.FindProperty("collectButton").objectReferenceValue = collect;
        so.FindProperty("chips").objectReferenceValue = chips;
        so.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ messages et puces

    static IdleToasts BuildToasts(RectTransform host, RectTransform drawerPanel)
    {
        var toasts = host.gameObject.AddComponent<IdleToasts>();
        var so = new SerializedObject(toasts);
        so.FindProperty("drawerPanel").objectReferenceValue = drawerPanel;
        so.FindProperty("gapAboveDrawer").floatValue = Px(26f);
        so.FindProperty("slotHeight").floatValue = Px(78f);
        so.FindProperty("rise").floatValue = Px(34f);
        SerializedProperty slots = so.FindProperty("slots");
        slots.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            Image bg = Rounded("Toast" + i, host, new Color(UiTheme.Slate.r, UiTheme.Slate.g, UiTheme.Slate.b, 0.94f), Px(18f));
            bg.raycastTarget = false;
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            var layout = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            int padX = Mathf.RoundToInt(Px(18f)), padY = Mathf.RoundToInt(Px(8f));
            layout.padding = new RectOffset(padX, padX, padY, padY);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text text = AddText(rt, "Text", "", 24f, UiTheme.Yellow, TextAnchor.MiddleCenter);
            text.lineSpacing = 0.95f;
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("rect").objectReferenceValue = rt;
            slot.FindPropertyRelative("text").objectReferenceValue = text;
            slot.FindPropertyRelative("group").objectReferenceValue = group;
            bg.gameObject.SetActive(false);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return toasts;
    }

    static IdleChips BuildChips(RectTransform host, RectTransform cashPill, RectTransform proPill)
    {
        var chips = host.gameObject.AddComponent<IdleChips>();
        var so = new SerializedObject(chips);
        so.FindProperty("cashTarget").objectReferenceValue = cashPill;
        so.FindProperty("proTarget").objectReferenceValue = proPill;
        SerializedProperty list = so.FindProperty("chips");
        list.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            Image bg = Rounded("Chip" + i, host, Green2, Px(14f));
            bg.raycastTarget = false;
            bg.rectTransform.sizeDelta = new Vector2(Px(92f), Px(30f));
            Center(bg.rectTransform, Vector2.zero);
            Text text = AddText(bg.transform, "Text", "", 18f, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            SerializedProperty chip = list.GetArrayElementAtIndex(i);
            chip.FindPropertyRelative("rect").objectReferenceValue = bg.rectTransform;
            chip.FindPropertyRelative("text").objectReferenceValue = text;
            chip.FindPropertyRelative("background").objectReferenceValue = bg;
            chip.FindPropertyRelative("group").objectReferenceValue = group;
            bg.gameObject.SetActive(false);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return chips;
    }
}
