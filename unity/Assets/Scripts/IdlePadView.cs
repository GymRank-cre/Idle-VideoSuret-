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
    [SerializeField] Transform priceRoot;            // le prix se tient face à la caméra : couché au sol, la perspective l'écraserait
    [SerializeField] Color affordableColor = Color.white;
    [SerializeField] Color waitingColor = new Color32(0xC3, 0xCC, 0xD7, 0xFF);
    [SerializeField, Min(1)] int bigSize = 130;
    [SerializeField, Min(0.1f)] float maxWidth = 0.9f;        // largeur que le prix ne dépasse pas, en cases
    [SerializeField, Range(0.5f, 1f)] float minFit = 0.8f;    // il rétrécit jusque-là, pas au-delà
    [SerializeField, Min(0f)] float pulseAmount = 0.05f;
    [SerializeField, Min(0f)] float pulseSpeed = 6f;
    [SerializeField, Range(0f, 1f)] float touchDip = 0.14f;   // à l'achat, la plaque se « vide » puis rebondit
    [SerializeField, Min(0f)] float touchDepth = 0.06f;       // et s'enfonce
    [SerializeField, Min(0.1f)] float touchRecover = 3.5f;

    long shownPrice = -1;
    bool affordable, known;
    float dip;
    Renderer priceRenderer;

    // Taille du prix selon le zoom : pleine vue d'ensemble, plus discrète de près, où la plaque suffit à le porter.
    public static float PriceScale(float zoom) { return Mathf.Lerp(1f, 0.62f, Mathf.InverseLerp(1f, 2.6f, zoom)); }

    // Taille de police pour que le prix tienne dans la pastille. `width` : sa largeur à la taille `size`.
    public static int FitSize(int size, float width, float maxWidth, float minFit)
    {
        return Mathf.RoundToInt(size * Mathf.Clamp(maxWidth / Mathf.Max(0.01f, width), minFit, 1f));
    }

    public int Station => station;
    public IdleGame.PadKind Kind => kind;
    public bool IsVisible => visual.gameObject.activeSelf;
    public Vector3 Position => transform.position;

    void Awake()
    {
        priceRenderer = priceText.GetComponent<Renderer>();
    }

    public void Refresh(IdleGame game)
    {
        bool visible = game.PadVisible(station, kind);
        if (visual.gameObject.activeSelf != visible) visual.gameObject.SetActive(visible);
        if (!visible) return;

        long price = game.PadPrice(station, kind);
        if (price != shownPrice)
        {
            shownPrice = price;
            priceText.fontSize = bigSize;
            priceText.text = NumberFormat.Short(price);
            priceText.fontSize = FitSize(bigSize, priceRenderer.localBounds.size.x, maxWidth, minFit);
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
        pulse *= 1f - touchDip * dip * Mathf.Cos(dip * Mathf.PI * 2f);   // au retour, elle dépasse un peu : c'est le rebond
        visual.localScale = new Vector3(pulse, 1f, pulse);
        visual.localPosition = new Vector3(0f, -touchDepth * dip, 0f);
        float price = PriceScale(1f / IdleCameraRig.ScreenScale);
        priceRoot.localScale = new Vector3(price, price, 1f);
    }

    public void Touched()
    {
        if (!MotionSettings.ReduceMotion) dip = 1f;
    }
}
