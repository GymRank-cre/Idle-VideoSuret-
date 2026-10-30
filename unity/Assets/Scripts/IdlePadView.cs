using UnityEngine;

// Une plaque d'achat posée au sol (UL) : Vitesse, Ajouter ou Débloquer. Pastille sombre avec icône et prix, bord vert plein qui bat quand c'est abordable,
// jaune en pointillés sinon. Elle lit le jeu et ne modifie rien ; c'est IdleWorldInput qui déclenche l'achat, via IdlePadField.
public class IdlePadView : MonoBehaviour
{
    [SerializeField] int station;
    [SerializeField] IdleGame.PadKind kind;
    [SerializeField] Transform visual;               // ce qui bat
    [SerializeField] MeshFilter face;
    [SerializeField] Mesh meshAffordable;
    [SerializeField] Mesh meshWaiting;
    [SerializeField] TextMesh priceText;
    [SerializeField] Color affordableColor = Color.white;
    [SerializeField] Color waitingColor = new Color32(0xC3, 0xCC, 0xD7, 0xFF);
    [SerializeField, Min(1)] int longLabel = 4;      // au-delà, le prix passe en petit pour rester dans la plaque
    [SerializeField, Min(1)] int bigSize = 90;
    [SerializeField, Min(1)] int smallSize = 72;
    [SerializeField, Min(0f)] float pulseAmount = 0.05f;
    [SerializeField, Min(0f)] float pulseSpeed = 6f;
    [SerializeField, Range(0f, 1f)] float touchDip = 0.18f;   // la plaque se « vide » : elle rétrécit d'un coup puis revient
    [SerializeField, Min(0.1f)] float touchRecover = 3.5f;

    long shownPrice = -1;
    bool affordable, known;
    float dip;

    public int Station => station;
    public IdleGame.PadKind Kind => kind;
    public bool IsVisible => visual.gameObject.activeSelf;
    public Vector3 Position => transform.position;

    public void Refresh(IdleGame game)
    {
        bool visible = game.PadVisible(station, kind);
        if (visual.gameObject.activeSelf != visible) visual.gameObject.SetActive(visible);
        if (!visible) return;

        long price = game.PadPrice(station, kind);
        if (price != shownPrice)
        {
            shownPrice = price;
            string label = NumberFormat.Short(price);
            priceText.text = label;
            priceText.fontSize = label.Length > longLabel ? smallSize : bigSize;
        }
        bool canPay = game.Cash >= price;
        if (canPay == affordable && known) return;
        known = true;
        affordable = canPay;
        face.sharedMesh = canPay ? meshAffordable : meshWaiting;
        priceText.color = canPay ? affordableColor : waitingColor;
    }

    public void Animate(float time, float dt)
    {
        if (!IsVisible) return;
        float pulse = affordable && !MotionSettings.ReduceMotion ? 1f + pulseAmount * Mathf.Sin(time * pulseSpeed + station) : 1f;
        if (dip > 0f) dip = Mathf.Max(0f, dip - dt * touchRecover);
        pulse *= 1f - touchDip * dip;
        visual.localScale = new Vector3(pulse, 1f, pulse);
    }

    public void Touched()
    {
        if (!MotionSettings.ReduceMotion) dip = 1f;
    }
}
