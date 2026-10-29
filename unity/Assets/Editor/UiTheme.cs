using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Thème de l'interface (K1) : police unique Lilita One, palette de la bible artistique, texte de jeu blanc contouré.
// Un seul endroit pour le style : les constructeurs de scène l'appellent au lieu de recopier les réglages.
public static class UiTheme
{
    const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

    // Palette (QUALITE.md)
    public static readonly Color Ink = new Color32(0x0E, 0x16, 0x20, 0xFF);
    public static readonly Color Slate = new Color32(0x1E, 0x2A, 0x38, 0xFF);
    public static readonly Color Yellow = new Color32(0xFF, 0xC4, 0x00, 0xFF);
    public static readonly Color Orange = new Color32(0xFF, 0x7A, 0x1A, 0xFF);
    public static readonly Color Money = new Color32(0x8C, 0xFF, 0x9E, 0xFF);
    public static readonly Color Cyan = new Color32(0x8D, 0xF1, 0xFF, 0xFF);
    public static readonly Color Red = new Color32(0xF2, 0x45, 0x3D, 0xFF);
    public static readonly Color Green = new Color32(0x3D, 0xDC, 0x5A, 0xFF);

    static Font font;

    // La police est chargée depuis le projet ; si elle manque (copie incomplète), repli sur la police intégrée.
    public static Font Font
    {
        get
        {
            if (font != null) return font;
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // Socle plein sous un élément (effet « jouet ») : copie de `target` décalée vers le bas, placée juste derrière lui.
    public static RectTransform AddBase(RectTransform target, Color baseColor, float depth, float ppum)
    {
        GameObject go = new GameObject(target.name + "Base", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(target.parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = target.anchorMin;
        rt.anchorMax = target.anchorMax;
        rt.pivot = target.pivot;
        rt.sizeDelta = target.sizeDelta;
        rt.anchoredPosition = target.anchoredPosition + new Vector2(0f, -depth);
        Image img = go.GetComponent<Image>();
        img.sprite = UiSprites.Rounded();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = ppum;
        img.color = baseColor;
        img.raycastTarget = false;
        go.transform.SetSiblingIndex(target.GetSiblingIndex());
        return rt;
    }

    // Texte d'interface de jeu : police Lilita One, blanc, contour sombre (distance en unités du Canvas 1080) et ombre portée.
    public static void Style(Text text, int size, Color color, float outline = 4f)
    {
        text.font = Font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Normal; // Lilita One est déjà grasse
        text.color = color;
        foreach (Outline o in text.GetComponents<Outline>()) Object.DestroyImmediate(o);
        foreach (Shadow s in text.GetComponents<Shadow>()) if (!(s is Outline)) Object.DestroyImmediate(s);

        var o4 = text.gameObject.AddComponent<Outline>();
        o4.effectColor = Ink;
        o4.effectDistance = new Vector2(outline, -outline);
        o4.useGraphicAlpha = true;
        var sh = text.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.8f);
        sh.effectDistance = new Vector2(0f, -outline * 2f);
    }
}
