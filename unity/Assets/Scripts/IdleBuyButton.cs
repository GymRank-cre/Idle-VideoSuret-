using UnityEngine;
using UnityEngine.UI;

// Bouton d'achat du tiroir (U3b) : vert si on peut payer, gris sinon, jaune s'il est conseillé. Il affiche ce qu'on lui donne et ne décide rien.
// Le battement agit sur `visual`, un enfant : la racine reste libre pour l'appui (PressScale) et garde une zone de toucher fixe.
public class IdleBuyButton : MonoBehaviour
{
    [SerializeField] VerticalGradient face;
    [SerializeField] Shadow edge;                 // épaisseur du bouton
    [SerializeField] Transform visual;
    [SerializeField] Text labelText;
    [SerializeField] Text deltaText;
    [SerializeField] Text priceText;
    [SerializeField] Text levelText;
    [SerializeField] GameObject bestStar;
    [SerializeField, Min(1f)] float pulseScale = 1.07f;
    [SerializeField, Min(0.1f)] float pulsePeriod = 1f;

    static readonly Color GreenTop = new Color32(0x6B, 0xF0, 0x83, 0xFF), Green = new Color32(0x3D, 0xDC, 0x5A, 0xFF), GreenEdge = new Color32(0x1F, 0x9D, 0x3A, 0xFF);
    static readonly Color GreyTop = new Color32(0xCB, 0xD3, 0xDC, 0xFF), Grey = new Color32(0xB9, 0xC3, 0xCF, 0xFF), GreyEdge = new Color32(0x8F, 0x9B, 0xA9, 0xFF);
    static readonly Color YellowTop = new Color32(0xFF, 0xE2, 0x7A, 0xFF), Yellow = new Color32(0xFF, 0xC4, 0x00, 0xFF), YellowEdge = new Color32(0xD9, 0x9A, 0x00, 0xFF);
    static readonly Color PaleTop = new Color32(0xFF, 0xE9, 0xA0, 0xFF), Pale = new Color32(0xF3, 0xCB, 0x4A, 0xFF), PaleEdge = new Color32(0xBF, 0xA0, 0x2B, 0xFF);

    long shownPrice = -1;
    bool pulsing;

    public Button Button => GetComponent<Button>();

    public void SetPrice(long price)
    {
        if (price == shownPrice) return;
        shownPrice = price;
        priceText.text = NumberFormat.Short(price);
    }

    public void SetLabel(string text) { labelText.text = text; }
    public void SetDelta(string text) { deltaText.text = text; }
    public void SetLevel(string text) { levelText.text = text; }

    // Conseillé et abordable : jaune qui bat ; conseillé mais trop cher : jaune pâle ; sinon vert ou gris.
    public void SetState(bool affordable, bool best, bool pulse)
    {
        if (best) Paint(affordable ? YellowTop : PaleTop, affordable ? Yellow : Pale, affordable ? YellowEdge : PaleEdge);
        else Paint(affordable ? GreenTop : GreyTop, affordable ? Green : Grey, affordable ? GreenEdge : GreyEdge);
        if (bestStar != null && bestStar.activeSelf != best) bestStar.SetActive(best);
        pulsing = pulse && affordable;
        if (!pulsing) visual.localScale = Vector3.one;
    }

    void Paint(Color top, Color bottom, Color edgeColor)
    {
        face.Set(top, bottom);
        if (edge.effectColor != edgeColor) edge.effectColor = edgeColor;
    }

    void Update()
    {
        if (!pulsing) return;
        if (MotionSettings.ReduceMotion) { visual.localScale = Vector3.one; return; }
        float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / pulsePeriod);
        visual.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, k);
    }
}
