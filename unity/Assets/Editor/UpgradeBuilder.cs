using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Utilitaires de construction d'interface partagés par les constructeurs de scène (boîte arrondie, libellé, rotation).
// (Le nom vient de l'ancien menu d'améliorations, retiré avec l'ancien jeu : U1.)
public static class UpgradeBuilder
{
    internal static void Rot(RectTransform rt, float angle)
    {
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    internal static RectTransform Box(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool button)
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

    internal static Text Label(string name, Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        Text t = go.GetComponent<Text>();
        UiTheme.Style(t, size, color);
        t.alignment = TextAnchor.MiddleCenter;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
