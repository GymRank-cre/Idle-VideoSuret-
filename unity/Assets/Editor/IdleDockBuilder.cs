using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static IdleHudBuilder;

// Construit le bas de l'écran (UL) : bandeau « Meilleur achat », sélecteur ×1/×10/MAX, loupe, emplacement de la fiche ; et les lignes des postes.
// Mêmes mesures que le web (pixels CSS passés dans Px) ; les pièces sont placées à la main, sans groupe de mise en page, sauf quand un prix change de largeur.
public static class IdleDockBuilder
{
    const float BarHeight = 66f, TopRowBottom = 84f, FicheBottom = 140f, FirstRow = 8f, RowHeight = 76f, RowPitch = 85f, UpWidth = 78f, UpHeight = 62f, WideHeight = 56f, RowPad = 8f, Gap = 7f;
    const string ConfigPath = "Assets/Prototype/IdleEconomyConfig.asset";

    static readonly Color YellowTop = new Color32(0xFF, 0xE2, 0x7A, 0xFF), YellowEdge = new Color32(0xD9, 0x9A, 0x00, 0xFF);
    static readonly Color Cream = new Color32(0xF6, 0xEF, 0xE2, 0xFF), Green2 = new Color32(0x1F, 0x9D, 0x3A, 0xFF), Blue = new Color32(0x1F, 0xA0, 0xFF, 0xFF);
    static readonly Color HintBrown = new Color32(0x7A, 0x6A, 0x4C, 0xFF), BannerBrown = new Color32(0x6B, 0x4A, 0x00, 0xFF), Unknown = new Color32(0x8A, 0x8F, 0x98, 0xFF);
    static readonly Color LockTile = new Color32(0x9A, 0xA6, 0xB3, 0xFF), FarTile = new Color32(0xB8, 0xBF, 0xC8, 0xFF);

    // couleur et icône de la tuile de chaque poste
    static readonly Color[] Tiles = { new Color32(0xC9, 0x8B, 0x4A, 0xFF), new Color32(0xFF, 0x9A, 0x45, 0xFF), new Color32(0x16, 0xA5, 0xB8, 0xFF), new Color32(0x1F, 0xA0, 0xFF, 0xFF), new Color32(0xE5, 0xB9, 0x6B, 0xFF), new Color32(0x7E, 0xC8, 0xFF, 0xFF) };
    static readonly string[] TileIcons = { "caisse", "camera", "eclair", "bouclier", "caisse", "camionnette" };

    public static IdleDock Build(Transform canvas)
    {
        RectTransform area = NewRect("DockArea", canvas);
        Stretch(area);
        area.gameObject.AddComponent<SafeAreaFitter>();

        IdleBestBar bar = BuildBestBar(area);
        BuildModes(area);
        Button view = BuildViewButton(area);

        RectTransform fiche = NewRect("Fiche", area);
        BottomStretch(fiche, Px(14f), Px(14f), Px(FicheBottom), Px(RowHeight));
        fiche.gameObject.SetActive(false);

        var dock = area.gameObject.AddComponent<IdleDock>();
        var so = new SerializedObject(dock);
        so.FindProperty("area").objectReferenceValue = area;
        so.FindProperty("fiche").objectReferenceValue = fiche;
        so.FindProperty("bestBar").objectReferenceValue = bar;
        so.FindProperty("viewButton").objectReferenceValue = view;
        so.FindProperty("rig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
        so.FindProperty("selection").objectReferenceValue = Object.FindFirstObjectByType<IdleSelection>();
        so.FindProperty("barsTop").floatValue = Px(FicheBottom);
        so.FindProperty("ficheTop").floatValue = Px(FicheBottom + RowHeight + 8f);
        so.FindProperty("ficheBottom").floatValue = Px(FicheBottom);
        so.FindProperty("ficheSlide").floatValue = Px(40f);
        so.ApplyModifiedPropertiesWithoutUndo();

        var selection = new SerializedObject(Object.FindFirstObjectByType<IdleSelection>());
        selection.FindProperty("dock").objectReferenceValue = dock;
        selection.FindProperty("fiche").objectReferenceValue = fiche;
        selection.ApplyModifiedPropertiesWithoutUndo();
        return dock;
    }

    static Button BuildViewButton(RectTransform parent)
    {
        RectTransform rt = NewRect("ViewButton", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(Px(46f), Px(51f));
        rt.anchoredPosition = new Vector2(-Px(14f), Px(TopRowBottom - 5f));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Ui("Face_orange");
        img.raycastPadding = new Vector4(-Px(2f), -Px(2f), -Px(2f), -Px(2f));
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        rt.gameObject.AddComponent<PressScale>();
        Image ico = AddIcon(rt, "Icon", "Ico_view", 30f, false);
        ico.rectTransform.anchorMin = ico.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        ico.rectTransform.anchoredPosition = new Vector2(0f, -Px(23f));
        return button;
    }

    // ------------------------------------------------------------------ bandeau « Meilleur achat »

    static IdleBestBar BuildBestBar(RectTransform parent)
    {
        Image bg = Rounded("BestBar", parent, Color.white, Px(18f));
        RectTransform rt = bg.rectTransform;
        BottomStretch(rt, Px(14f), Px(14f), Px(10f), Px(BarHeight));
        bg.gameObject.AddComponent<VerticalGradient>().Set(YellowTop, UiTheme.Yellow);
        AddEdge(bg, YellowEdge, Px(4f), false);
        var shadow = bg.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.25f);
        shadow.effectDistance = new Vector2(0f, -Px(2f));

        Image star = AddIcon(rt, "Star", "etoile", 36f, false);
        Corner(star.rectTransform, new Vector2(0f, 1f), new Vector2(-Px(8f), Px(12f)));
        star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);

        RectTransform titleArea = NewRect("TitleArea", rt);
        Between(titleArea, Px(30f), Px(116f), Px(10f), Px(21f));
        RectTransform titleRow = NewRect("TitleRow", titleArea);
        titleRow.anchorMin = titleRow.anchorMax = new Vector2(0f, 0.5f);
        titleRow.pivot = new Vector2(0f, 0.5f);
        titleRow.sizeDelta = new Vector2(0f, Px(21f));
        var titleLayout = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        titleLayout.spacing = Px(5f);
        titleLayout.childAlignment = TextAnchor.MiddleLeft;
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.childForceExpandWidth = false;
        titleLayout.childForceExpandHeight = false;
        titleRow.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        Text title = AddPlainText(titleRow, "Title", "", 17f, UiTheme.Ink, TextAnchor.MiddleLeft);
        Image titleArrow = AddIcon(titleRow, "Arrow", "Ico_arrow", 16f, true);
        Text titleLevel = AddPlainText(titleRow, "Level", "", 17f, UiTheme.Ink, TextAnchor.MiddleLeft);
        Text detail = AddPlainText(rt, "Detail", "", 13f, BannerBrown, TextAnchor.UpperLeft);
        Between(detail.rectTransform, Px(30f), Px(116f), Px(32f), Px(30f));
        detail.horizontalOverflow = HorizontalWrapMode.Wrap;
        detail.lineSpacing = 0.9f;

        IdleBuyButton go = BuyButton(rt, "BuyBest", Px(98f), Px(50f));
        RectTransform goRect = (RectTransform)go.transform;
        goRect.anchorMin = goRect.anchorMax = new Vector2(1f, 0.5f);
        goRect.pivot = new Vector2(1f, 0.5f);
        goRect.anchoredPosition = new Vector2(-Px(8f), Px(1f));
        Transform visual = goRect.Find("Visual");

        Text label = AddText(visual, "Label", "", 15f, Color.white, TextAnchor.MiddleCenter);
        Between(label.rectTransform, 0f, 0f, Px(5f), Px(18f));
        Text price = PriceRow(visual, 18f, Px(23f), Px(23f));

        // jauge d'attente quand c'est encore trop cher
        RectTransform wait = NewRect("Wait", visual);
        wait.anchorMin = Vector2.zero;
        wait.anchorMax = new Vector2(1f, 0f);
        wait.offsetMin = new Vector2(Px(6f), Px(3f));
        wait.offsetMax = new Vector2(-Px(6f), Px(8f));
        Image fill = Rounded("Fill", wait, UiTheme.Green, Px(2.5f));
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        var btn = new SerializedObject(go);
        btn.FindProperty("labelText").objectReferenceValue = label;
        btn.FindProperty("priceText").objectReferenceValue = price;
        btn.ApplyModifiedPropertiesWithoutUndo();

        var bar = bg.gameObject.AddComponent<IdleBestBar>();
        var so = new SerializedObject(bar);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("titleRow").objectReferenceValue = titleRow;
        so.FindProperty("titleArea").objectReferenceValue = titleArea;
        so.FindProperty("titleArrow").objectReferenceValue = titleArrow.gameObject;
        so.FindProperty("titleLevel").objectReferenceValue = titleLevel;
        so.FindProperty("detailText").objectReferenceValue = detail;
        so.FindProperty("buyButton").objectReferenceValue = go;
        so.FindProperty("waitFill").objectReferenceValue = fill.rectTransform;
        so.FindProperty("rig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    // ------------------------------------------------------------------ ×1 / ×10 / MAX

    static void BuildModes(RectTransform parent)
    {
        RectTransform row = NewRect("BuyModes", parent);
        BottomStretch(row, Px(14f), Px(74f), Px(TopRowBottom + 9f), Px(28f));

        string[] names = { "×1", "×10", "MAX" };
        int[] modes = { 1, 10, 0 };
        float[] widths = { 48f, 48f, 54f };
        var modesScript = row.gameObject.AddComponent<IdleBuyModes>();
        var so = new SerializedObject(modesScript);
        SerializedProperty choices = so.FindProperty("choices");
        choices.arraySize = names.Length;
        float x = 0f;
        for (int i = 0; i < names.Length; i++)
        {
            Image ring = Rounded("Ring" + i, row, UiTheme.Yellow, Px(16f));
            ring.raycastTarget = false;
            Corner(ring.rectTransform, new Vector2(0f, 1f), new Vector2(Px(x - 2f), Px(2f)));
            ring.rectTransform.sizeDelta = new Vector2(Px(widths[i] + 4f), Px(32f));

            Image bg = Rounded("Mode" + i, row, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.35f), Px(14f));
            Corner(bg.rectTransform, new Vector2(0f, 1f), new Vector2(Px(x), 0f));
            bg.rectTransform.sizeDelta = new Vector2(Px(widths[i]), Px(28f));
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            bg.raycastPadding = new Vector4(0f, -Px(8.5f), 0f, -Px(8.5f));   // zone tactile de 122 unités de haut, plus grande que le dessin
            bg.gameObject.AddComponent<PressScale>();
            Text t = AddText(bg.transform, "Text", names[i], 15f, Color.white, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);

            SerializedProperty c = choices.GetArrayElementAtIndex(i);
            c.FindPropertyRelative("mode").intValue = modes[i];
            c.FindPropertyRelative("button").objectReferenceValue = button;
            c.FindPropertyRelative("background").objectReferenceValue = bg;
            c.FindPropertyRelative("ring").objectReferenceValue = ring.gameObject;
            x += widths[i] + 6f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ liste des postes

    public const float RowsHeight = FirstRow + 6 * RowPitch + 6f;

    // Une ligne par poste, dans la feuille « Postes ».
    public static IdleStationRow[] BuildRows(RectTransform content)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<IdleEconomyConfig>(ConfigPath);
        int count = cfg.stations.Length;
        IdleStationView[] views = IdleSceneBuilder.FirstSlots();
        var rows = new IdleStationRow[count];
        for (int i = 0; i < count; i++) rows[i] = BuildRow(content, cfg, i, views[i]);
        return rows;
    }

    static IdleStationRow BuildRow(RectTransform content, IdleEconomyConfig cfg, int i, IdleStationView view)
    {
        Image bg = Rounded("Row" + i, content, Cream, Px(18f));
        bg.raycastTarget = false;
        RectTransform rt = bg.rectTransform;
        TopStretch(rt, Px(2f), Px(2f), Px(FirstRow + i * RowPitch), Px(RowHeight));
        AddShadow(bg, 0.28f, Px(4f));
        string name = Loc.T(cfg.stations[i].nameKey);
        float mid = RowPad + 50f + Gap;   // début de la colonne du milieu

        var row = bg.gameObject.AddComponent<IdleStationRow>();
        var so = new SerializedObject(row);
        so.FindProperty("station").intValue = i;
        so.FindProperty("view").objectReferenceValue = view;
        so.FindProperty("background").objectReferenceValue = bg;

        // poste ouvert
        RectTransform open = Part(rt, "Open");
        Tile(open, Tiles[i], TileIcons[i]);
        float right = RowPad + UpWidth + Gap + UpWidth + Gap;
        Text nameText = AddPlainText(open, "Name", name, 17f, UiTheme.Ink, TextAnchor.MiddleLeft);
        Between(nameText.rectTransform, Px(mid), Px(right), Px(7f), Px(20f));

        RectTransform incomeRow = NewRect("Income", open);
        Between(incomeRow, Px(mid), Px(right), Px(28f), Px(18f));
        Image bill = AddIcon(incomeRow, "Icon", "billet", 18f, false);
        Corner(bill.rectTransform, new Vector2(0f, 1f), Vector2.zero);
        Text income = AddPlainText(incomeRow, "Value", "", 15f, Green2, TextAnchor.MiddleLeft);
        Between(income.rectTransform, Px(21f), 0f, 0f, Px(18f));

        Image track = Rounded("Cycle", open, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.14f), Px(3f));
        track.raycastTarget = false;
        Between(track.rectTransform, Px(mid), Px(right), Px(48f), Px(6f));
        Image fill = Rounded("Fill", track.transform, Blue, Px(3f));
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        Text milestone = AddPlainText(open, "Milestone", "", 11.5f, HintBrown, TextAnchor.MiddleLeft);
        Between(milestone.rectTransform, Px(mid), Px(right), Px(57f), Px(14f));

        IdleBuyButton speed = UpgradeButton(open, "Speed", "eclair", Loc.T("drawer.speed"), RowPad + UpWidth + Gap);
        IdleBuyButton add = UpgradeButton(open, "Add", "plus", Loc.T("drawer.add"), RowPad);

        // prochain poste à débloquer
        RectTransform next = Part(rt, "Next");
        Tile(next, LockTile, "cadenas");
        float wide = RowPad + UpWidth * 2f + Gap;
        Text nextName = AddPlainText(next, "Name", name, 17f, UiTheme.Ink, TextAnchor.MiddleLeft);
        Between(nextName.rectTransform, Px(mid), Px(wide + Gap), Px(9f), Px(20f));
        Text hint = AddPlainText(next, "Hint", "", 12.5f, HintBrown, TextAnchor.UpperLeft);
        Between(hint.rectTransform, Px(mid), Px(wide + Gap), Px(30f), Px(40f));
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.lineSpacing = 0.9f;
        IdleBuyButton unlock = UnlockButton(next, wide - RowPad);

        // postes suivants : encore inconnus
        RectTransform far = Part(rt, "Far");
        Tile(far, FarTile, "cadenas");
        Text farName = AddPlainText(far, "Name", "???", 17f, Unknown, TextAnchor.MiddleLeft);
        Between(farName.rectTransform, Px(mid), Px(RowPad), Px(17f), Px(20f));
        Text farHint = AddPlainText(far, "Hint", "", 12.5f, HintBrown, TextAnchor.UpperLeft);
        Between(farHint.rectTransform, Px(mid), Px(RowPad), Px(39f), Px(20f));

        so.FindProperty("openRoot").objectReferenceValue = open.gameObject;
        so.FindProperty("nextRoot").objectReferenceValue = next.gameObject;
        so.FindProperty("farRoot").objectReferenceValue = far.gameObject;
        so.FindProperty("incomeText").objectReferenceValue = income;
        so.FindProperty("cycleFill").objectReferenceValue = fill.rectTransform;
        so.FindProperty("milestoneText").objectReferenceValue = milestone;
        so.FindProperty("speedButton").objectReferenceValue = speed;
        so.FindProperty("addButton").objectReferenceValue = add;
        so.FindProperty("nextHint").objectReferenceValue = hint;
        so.FindProperty("unlockButton").objectReferenceValue = unlock;
        so.FindProperty("farHint").objectReferenceValue = farHint;
        so.ApplyModifiedPropertiesWithoutUndo();
        next.gameObject.SetActive(false);
        far.gameObject.SetActive(false);
        return row;
    }

    static void Tile(RectTransform parent, Color color, string icon)
    {
        Image tile = Rounded("Tile", parent, color, Px(14f));
        tile.raycastTarget = false;
        RectTransform rt = tile.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(Px(50f), Px(50f));
        rt.anchoredPosition = new Vector2(Px(RowPad), 0f);
        Image ico = AddIcon(rt, "Icon", icon, 40f, false);
        Center(ico.rectTransform, Vector2.zero);
    }

    // ------------------------------------------------------------------ boutons d'achat

    static IdleBuyButton UpgradeButton(RectTransform parent, string name, string icon, string label, float right)
    {
        IdleBuyButton button = BuyButton(parent, name, Px(UpWidth), Px(UpHeight));
        RectTransform rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-Px(right), 0f);
        Transform visual = rt.Find("Visual");

        LabelRow(visual, icon, label, 12.5f, Px(5f));
        Text delta = AddText(visual, "Delta", "", 12f, new Color32(0xE6, 0xFF, 0xE0, 0xFF), TextAnchor.MiddleCenter, 1.2f);
        Between(delta.rectTransform, 0f, 0f, Px(20f), Px(14f));
        Text price = PriceRow(visual, 16f, Px(35f), Px(20f));

        // pastille de niveau, à cheval sur le coin haut gauche ; sa largeur suit le texte
        Image badge = Rounded("Level", visual, UiTheme.Ink, Px(9f));
        badge.raycastTarget = false;
        AddShadow(badge, 0.4f, Px(2f));
        RectTransform b = badge.rectTransform;
        b.anchorMin = b.anchorMax = new Vector2(0f, 1f);
        b.pivot = new Vector2(0f, 1f);
        b.anchoredPosition = new Vector2(-Px(5f), Px(14f));
        var fit = badge.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        var pad = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        pad.padding = new RectOffset(Mathf.RoundToInt(Px(6f)), Mathf.RoundToInt(Px(6f)), 0, 0);
        pad.childAlignment = TextAnchor.MiddleCenter;
        pad.childControlWidth = true;
        pad.childControlHeight = true;
        b.sizeDelta = new Vector2(Px(36f), Px(17f));
        Text level = AddText(b, "Text", "", 11.5f, Color.white, TextAnchor.MiddleCenter, 1f);

        Image star = AddIcon(visual, "BestStar", "etoile", 22f, false);
        Corner(star.rectTransform, new Vector2(1f, 1f), new Vector2(Px(6f), Px(9f)));
        star.rectTransform.pivot = new Vector2(1f, 1f);
        star.color = new Color(1f, 0.6f, 0.25f);
        star.gameObject.SetActive(false);

        var so = new SerializedObject(button);
        so.FindProperty("deltaText").objectReferenceValue = delta;
        so.FindProperty("priceText").objectReferenceValue = price;
        so.FindProperty("levelText").objectReferenceValue = level;
        so.FindProperty("bestStar").objectReferenceValue = star.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    static IdleBuyButton UnlockButton(RectTransform parent, float width)
    {
        IdleBuyButton button = BuyButton(parent, "Unlock", Px(width), Px(WideHeight));
        RectTransform rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-Px(RowPad), 0f);
        Transform visual = rt.Find("Visual");
        LabelRow(visual, "cadenas", Loc.T("drawer.unlock"), 14f, Px(7f));
        Text price = PriceRow(visual, 19f, Px(27f), Px(24f));
        var so = new SerializedObject(button);
        so.FindProperty("priceText").objectReferenceValue = price;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    // Racine fixe (appui et toucher), `Visual` qui peut battre, face à dégradé et épaisseur.
    internal static IdleBuyButton BuyButton(RectTransform parent, string name, float width, float height)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(width, height);
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        rt.gameObject.AddComponent<PressScale>().Silent = true;   // l'achat joue son propre son
        RectTransform visual = NewRect("Visual", rt);
        Stretch(visual);
        Image face = Rounded("Face", visual, Color.white, Px(14f));
        Stretch(face.rectTransform);
        var gradient = face.gameObject.AddComponent<VerticalGradient>();
        Shadow edge = AddEdge(face, UiTheme.Green, Px(4f), false);
        var shadow = face.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.28f);
        shadow.effectDistance = new Vector2(0f, -Px(2f));

        var script = rt.gameObject.AddComponent<IdleBuyButton>();
        var so = new SerializedObject(script);
        so.FindProperty("face").objectReferenceValue = gradient;
        so.FindProperty("edge").objectReferenceValue = edge;
        so.FindProperty("visual").objectReferenceValue = visual;
        so.ApplyModifiedPropertiesWithoutUndo();
        return script;
    }

    // Icône + mot, centrés sur une ligne.
    static void LabelRow(Transform parent, string icon, string text, float css, float top)
    {
        RectTransform row = CenteredRow(parent, "LabelRow", top, Px(css + 3f), 3f);
        AddIcon(row, "Icon", icon, css + 2f, true);
        AddText(row, "Text", text, css, Color.white, TextAnchor.MiddleCenter, 1.2f);
    }

    // Billet + prix, centrés ; la ligne se recentre quand le prix change de largeur.
    static Text PriceRow(Transform parent, float css, float top, float height)
    {
        RectTransform row = CenteredRow(parent, "PriceRow", top, height, 3f);
        AddIcon(row, "Icon", "billet", css + 2f, true);
        return AddText(row, "Price", "0", css, Color.white, TextAnchor.MiddleCenter);
    }

    internal static RectTransform CenteredRow(Transform parent, string name, float top, float height, float spacing)
    {
        RectTransform row = NewRect(name, parent);
        Between(row, 0f, 0f, top, height);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = Px(spacing);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    // ------------------------------------------------------------------ placement

    static RectTransform Part(RectTransform parent, string name)
    {
        RectTransform rt = NewRect(name, parent);
        Stretch(rt);
        return rt;
    }

    internal static Shadow AddEdge(Image img, Color color, float depth, bool upward)
    {
        var edge = img.gameObject.AddComponent<Shadow>();
        edge.effectColor = color;
        edge.effectDistance = new Vector2(0f, upward ? depth : -depth);
        edge.useGraphicAlpha = true;
        return edge;
    }

    // Bande horizontale posée depuis le bas : marges gauche et droite, distance au bas, hauteur.
    internal static void BottomStretch(RectTransform rt, float left, float right, float bottom, float height)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, bottom + height);
    }

    // Bande horizontale : marges gauche et droite, distance depuis le haut, hauteur.
    internal static void TopStretch(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void Between(RectTransform rt, float left, float right, float top, float height) { TopStretch(rt, left, right, top, height); }

    // Placé depuis un coin de son parent ; `offset` : décalage vers le haut et la droite (ou la gauche pour un coin droit).
    internal static void Corner(RectTransform rt, Vector2 anchor, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = offset;
    }
}
