using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// P4 : audit d'accessibilité de la scène (`Tools > Idle Prototype > Audit accessibilité`).
// 1. Contraste du texte contre son fond (rapport WCAG, cible >= 4,5:1) ; un texte avec contour sombre est jugé sur son contour.
// 2. Taille des zones tactiles (cible >= 122 unités du canvas de référence, soit environ 44 points sur un téléphone).
// Le résultat est écrit dans la console ; les fonctions de calcul sont testées par UnitTests.
public static class AccessibilityAudit
{
    public const float MinContrast = 4.5f;
    public const float MinTouch = 122f;

    public static float Luminance(Color c)
    {
        return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
    }

    static float Linear(float v)
    {
        return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    public static float Contrast(Color a, Color b)
    {
        float la = Luminance(a), lb = Luminance(b);
        float hi = Mathf.Max(la, lb), lo = Mathf.Min(la, lb);
        return (hi + 0.05f) / (lo + 0.05f);
    }

    // Agrandit la zone de toucher (invisible) des boutons trop petits jusqu'à MinTouch, sans changer leur apparence.
    public static int ExpandTouchAreas()
    {
        int changed = 0;
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Graphic g = b.targetGraphic;
            RectTransform rt = b.transform as RectTransform;
            if (g == null || rt == null || !g.raycastTarget) continue;
            Vector2 size = rt.rect.size;
            if (size.x < 1f || size.y < 1f) continue;   // plein écran
            float dx = Mathf.Max(0f, (MinTouch - size.x) * 0.5f), dy = Mathf.Max(0f, (MinTouch - size.y) * 0.5f);
            if (dx <= 0f && dy <= 0f) continue;
            g.raycastPadding = new Vector4(-dx, -dy, -dx, -dy);   // valeurs négatives : la zone déborde du dessin
            EditorUtility.SetDirty(g);
            changed++;
        }
        return changed;
    }

    [MenuItem("Tools/Idle Prototype/Audit accessibilité")]
    public static void Run()
    {
        var sb = new StringBuilder("Audit accessibilité\n");

        // --- contraste du texte
        int checkedTexts = 0, outlined = 0, direct = 0;
        var failing = new List<string>();
        foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(t.text) || t.color.a < 0.5f) continue;
            Color back;
            if (!Background(t.transform, out back)) continue;   // texte posé sur la scène 3D : hors périmètre de l'audit
            checkedTexts++;
            Outline outline = t.GetComponent<Outline>();
            float ratio = Contrast(t.color, back);
            if (ratio >= MinContrast) { direct++; continue; }
            if (outline != null && outline.effectColor.a > 0.5f && Contrast(t.color, outline.effectColor) >= MinContrast) { outlined++; continue; }
            failing.Add(Path(t.transform) + " « " + Shorten(t.text) + " » : " + ratio.ToString("0.0") + ":1 (texte " + Hex(t.color) + " sur " + Hex(back) + ")");
        }
        sb.Append("Contraste : ").Append(checkedTexts).Append(" textes sur fond uni ; ").Append(direct).Append(" >= 4,5:1 ; ")
          .Append(outlined).Append(" lisibles grâce au contour sombre ; ").Append(failing.Count).Append(" insuffisants\n");
        foreach (string f in failing) sb.Append("  - ").Append(f).Append('\n');

        // --- zones tactiles
        int buttons = 0;
        var small = new List<string>();
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            RectTransform rt = b.transform as RectTransform;
            Canvas canvas = b.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null) continue;
            Vector2 size = Vector2.Scale(rt.rect.size, rt.lossyScale) / Mathf.Max(0.0001f, canvas.rootCanvas.transform.lossyScale.x);
            if (b.targetGraphic != null) { Vector4 pad = b.targetGraphic.raycastPadding; size += new Vector2(-(pad.x + pad.z), -(pad.y + pad.w)); }   // zone de toucher réelle
            if (size.x < 1f || size.y < 1f) continue;   // plein écran / non dimensionné à ce stade
            buttons++;
            if (Mathf.Min(size.x, size.y) < MinTouch) small.Add(Path(b.transform) + " : " + size.x.ToString("0") + " x " + size.y.ToString("0"));
        }
        sb.Append("Zones tactiles : ").Append(buttons).Append(" boutons mesurés ; ").Append(small.Count).Append(" plus petits que ").Append(MinTouch).Append('\n');
        foreach (string s in small) sb.Append("  - ").Append(s).Append('\n');

        Debug.Log(sb.ToString());
    }

    // Couleur du fond : l'image opaque la plus proche parmi les parents.
    static bool Background(Transform t, out Color color)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            Image img = p.GetComponent<Image>();
            if (img != null && img.enabled && img.color.a >= 0.7f) { color = img.color; return true; }
        }
        color = Color.black;
        return false;
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static string Shorten(string s)
    {
        s = s.Replace("\n", " ");
        return s.Length > 24 ? s.Substring(0, 24) + "..." : s;
    }

    static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
}
