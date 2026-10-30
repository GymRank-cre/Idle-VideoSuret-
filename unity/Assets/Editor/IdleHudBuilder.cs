using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Construit l'interface fixe du jeu (U3a) : Canvas, zone sûre, barre du haut (argent, revenu, Points Pro, engrenage) et colonne de
// boutons orange (Commandes, Réputation, Turbo). Mêmes mesures que le web : `Px` convertit les pixels CSS (écran 390 de large) en unités du Canvas.
public static class IdleHudBuilder
{
    const float ReferenceWidth = 1080f;
    internal const float ReferenceHeight = 2337f;   // 390 x 844, la grille de référence
    const string UiFolder = "Assets/Prototype/Textures/Ui/";
    const int RoundedRadius = 40;          // rayon en pixels du sprite RoundedRect

    static readonly Color ProTeal = new Color32(0x0F, 0x6E, 0x7A, 0xFF);

    internal static float Px(float css) { return css * ReferenceWidth / 390f; }

    public static void Build()
    {
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform safe = NewRect("SafeArea", canvasGo.transform);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        var hud = safe.gameObject.AddComponent<IdleHud>();
        var so = new SerializedObject(hud);
        BuildTopBar(safe, so);
        BuildSideColumn(safe, so);
        so.ApplyModifiedPropertiesWithoutUndo();

        IdleDrawerBuilder.Build(canvasGo.transform);
    }

    // ------------------------------------------------------------------ barre du haut

    static void BuildTopBar(RectTransform parent, SerializedObject hud)
    {
        RectTransform bar = NewRect("TopBar", parent);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.offsetMin = new Vector2(Px(14f), -Px(10f + 42f));
        bar.offsetMax = new Vector2(-Px(14f), -Px(10f));
        var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = Px(8f);
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        // argent : largeur fixe du web (176), réduite sur les petits écrans
        Image cash = Pill("CashPill", bar, UiTheme.Slate);
        Row(cash.gameObject, 8f, 14f, 7f, TextAnchor.MiddleLeft);
        Size(cash.gameObject, Px(120f), Px(176f), -1f);
        AddIcon(cash.transform, "Icon", "billet", 30f, true);
        Text cashText = AddText(cash.transform, "Cash", "0", 23f, Color.white, TextAnchor.MiddleLeft);
        Size(cashText.gameObject, -1f, -1f, 1f);
        hud.FindProperty("cashText").objectReferenceValue = cashText;

        Image rate = Pill("RatePill", bar, UiTheme.Slate);
        Row(rate.gameObject, 12f, 12f, 0f, TextAnchor.MiddleCenter);
        hud.FindProperty("rateText").objectReferenceValue = AddText(rate.transform, "Rate", "0/s", 17f, Color.white, TextAnchor.MiddleCenter);

        // Points Pro : cachés tant qu'aucune commande n'a été livrée
        Image pro = Pill("ProPill", bar, ProTeal);
        Row(pro.gameObject, 8f, 12f, 7f, TextAnchor.MiddleLeft);
        AddIcon(pro.transform, "Icon", "etoile", 30f, true);
        hud.FindProperty("proText").objectReferenceValue = AddText(pro.transform, "Pro", "0", 23f, Color.white, TextAnchor.MiddleLeft);
        hud.FindProperty("proPill").objectReferenceValue = pro.gameObject;
        pro.gameObject.SetActive(false);

        RectTransform spacer = NewRect("Spacer", bar);   // pousse l'engrenage à droite
        Size(spacer.gameObject, 0f, 0f, 1f);

        Image gear = Pill("GearButton", bar, UiTheme.Slate);
        Size(gear.gameObject, Px(42f), Px(42f), -1f);
        gear.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        gear.gameObject.AddComponent<PressScale>();
        Image gearIcon = AddIcon(gear.transform, "Icon", "engrenage", 32f, false);
        Center(gearIcon.rectTransform, Vector2.zero);
    }

    // ------------------------------------------------------------------ colonne de boutons

    static void BuildSideColumn(RectTransform parent, SerializedObject hud)
    {
        Sprite orange = Ui("Face_orange");
        Sprite yellow = Ui("Face_yellow");

        Image orders = SquareButton(parent, "OrdersButton", "commandes", orange, 0);
        Text ordersCount;
        hud.FindProperty("ordersBadge").objectReferenceValue = Badge(orders.transform, "0", out ordersCount);
        hud.FindProperty("ordersBadgeText").objectReferenceValue = ordersCount;

        Image reputation = SquareButton(parent, "ReputationButton", "etoile", orange, 1);
        hud.FindProperty("reputationBadge").objectReferenceValue = Badge(reputation.transform, "!", out _);

        Image turbo = SquareButton(parent, "TurboButton", "flamme", orange, 2);
        hud.FindProperty("turboButton").objectReferenceValue = turbo.GetComponent<Button>();
        hud.FindProperty("turboFace").objectReferenceValue = turbo;
        hud.FindProperty("turboIdleFace").objectReferenceValue = orange;
        hud.FindProperty("turboActiveFace").objectReferenceValue = yellow;

        // voile de recharge : couvre la face (58 x 58), pas son épaisseur
        Image cover = Rounded("Cover", turbo.transform, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.55f), Px(17f));
        cover.raycastTarget = false;
        RectTransform rt = cover.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(Px(58f), Px(58f));
        Text cd = AddText(cover.transform, "Time", "", 17f, Color.white, TextAnchor.MiddleCenter);
        Stretch(cd.rectTransform);
        hud.FindProperty("turboCover").objectReferenceValue = cover;
        hud.FindProperty("turboText").objectReferenceValue = cd;
        cover.gameObject.SetActive(false);
    }

    // Bouton carré orange : la face vient du web (dégradé + épaisseur), 58 + 7 px de haut, 12 px entre deux boutons.
    static Image SquareButton(RectTransform parent, string name, string icon, Sprite face, int index)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(Px(58f), Px(65f));
        rt.anchoredPosition = new Vector2(-Px(14f), -Px(62f + index * 70f));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = face;
        rt.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        rt.gameObject.AddComponent<PressScale>();
        Image ico = AddIcon(rt, "Icon", icon, 44f, false);
        ico.rectTransform.anchorMin = ico.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        ico.rectTransform.anchoredPosition = new Vector2(0f, -Px(29f));
        return img;
    }

    // Pastille rouge à bord blanc, à cheval sur le coin haut droit du bouton (cachée au départ).
    static GameObject Badge(Transform button, string text, out Text label)
    {
        Image ring = Rounded("Badge", button, Color.white, Px(12f));
        ring.raycastTarget = false;
        RectTransform rt = ring.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(Px(24f), Px(24f));
        rt.anchoredPosition = new Vector2(Px(7f), Px(7f));
        AddShadow(ring, 0.4f, Px(2f));
        Image red = Rounded("Fill", ring.transform, UiTheme.Red, Px(10f));
        red.raycastTarget = false;
        Stretch(red.rectTransform);
        red.rectTransform.offsetMin = new Vector2(Px(2f), Px(2f));
        red.rectTransform.offsetMax = new Vector2(-Px(2f), -Px(2f));
        label = AddText(ring.transform, "Text", text, 15f, Color.white, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        ring.gameObject.SetActive(false);
        return ring.gameObject;
    }

    // ------------------------------------------------------------------ briques

    internal static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    internal static void Center(RectTransform rt, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
    }

    internal static Image Rounded(string name, Transform parent, Color color, float radius)
    {
        RectTransform rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UiSprites.Rounded();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = RoundedRadius / radius;
        img.color = color;
        return img;
    }

    // Pastille sombre de 42 px de haut, ombre pleine dessous (3 px).
    static Image Pill(string name, Transform parent, Color color)
    {
        Image img = Rounded(name, parent, color, Px(21f));
        AddShadow(img, 0.45f, Px(3f));
        return img;
    }

    internal static void AddShadow(Image img, float alpha, float depth)
    {
        var shadow = img.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, alpha);
        shadow.effectDistance = new Vector2(0f, -depth);
        shadow.useGraphicAlpha = true;
    }

    static void Row(GameObject go, float left, float right, float spacing, TextAnchor anchor)
    {
        var row = go.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(Mathf.RoundToInt(Px(left)), Mathf.RoundToInt(Px(right)), 0, 0);
        row.spacing = Px(spacing);
        row.childAlignment = anchor;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
    }

    static void Size(GameObject go, float min, float preferred, float flexible)
    {
        var e = go.AddComponent<LayoutElement>();
        e.minWidth = min;
        e.preferredWidth = preferred;
        e.flexibleWidth = flexible;
    }

    internal static Image AddIcon(Transform parent, string name, string icon, float css, bool inRow)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(Px(css), Px(css));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Ui(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (inRow)
        {
            var e = rt.gameObject.AddComponent<LayoutElement>();
            e.minWidth = e.preferredWidth = Px(css);
            e.minHeight = e.preferredHeight = Px(css);
        }
        return img;
    }

    // Texte de jeu : blanc contouré.
    internal static Text AddText(Transform parent, string name, string text, float css, Color color, TextAnchor anchor, float outlineCss = 1.5f)
    {
        Text t = NewText(parent, name, text, anchor);
        UiTheme.Style(t, Mathf.RoundToInt(Px(css)), color, Px(outlineCss));
        return t;
    }

    // Texte posé sur un fond clair : sans contour.
    internal static Text AddPlainText(Transform parent, string name, string text, float css, Color color, TextAnchor anchor)
    {
        Text t = NewText(parent, name, text, anchor);
        t.font = UiTheme.Font;
        t.fontSize = Mathf.RoundToInt(Px(css));
        t.color = color;
        return t;
    }

    static Text NewText(Transform parent, string name, string text, TextAnchor anchor)
    {
        RectTransform rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Icône ou face de bouton : réglée en sprite à la première utilisation, sans mipmaps ni compression.
    internal static Sprite Ui(string file)
    {
        string path = UiFolder + file + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError("Image d'interface manquante : " + path); return null; }
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
