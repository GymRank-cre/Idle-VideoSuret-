using UnityEngine;

// Plaque d'achat posée au sol pour un poste (U2b) : diamant sombre, bord jaune en pointillés ou vert quand on a de quoi payer,
// flèche d'amélioration (poste ouvert) ou « NOUVEAU » (prochain poste à débloquer), prix en gros. Elle lit le jeu et ne modifie rien :
// l'achat est déclenché par IdlePlateInput. Cachée tant que le poste précédent n'est pas ouvert.
public class IdlePlateView : MonoBehaviour
{
    [SerializeField] int station;
    [SerializeField] Transform visual;               // ce qui pulse
    [SerializeField] GameObject borderAffordable;    // bord vert plein
    [SerializeField] GameObject borderWaiting;       // bord jaune en pointillés
    [SerializeField] GameObject arrow;               // flèche (poste ouvert)
    [SerializeField] Renderer arrowRenderer;
    [SerializeField] Material arrowOn;
    [SerializeField] Material arrowOff;
    [SerializeField] GameObject newTag;              // « NOUVEAU » (poste à débloquer)
    [SerializeField] TextMesh[] priceTexts;          // [0] = texte, les autres = contour sombre
    [SerializeField] Color affordableColor = Color.white;
    [SerializeField] Color waitingColor = new Color32(0xC3, 0xCC, 0xD7, 0xFF);
    [SerializeField, Min(0f)] float pulseAmount = 0.05f;
    [SerializeField, Min(0f)] float pulseSpeed = 6f;
    [SerializeField] float halfHitSize = 0.8f;       // moitié du côté de la zone de toucher : 1,6 m pour une plaque de 1 m (1,5 fois le visuel)

    long shownPrice = -1;
    int shownState = -1;
    bool shownAffordable;
    float pulseStart;

    public int Station => station;
    public bool IsVisible => visual != null && visual.gameObject.activeSelf;

    // La zone de toucher est un carré autour du centre de la plaque, aligné sur les axes de la dalle.
    public bool Contains(Vector3 point)
    {
        Vector3 c = transform.position;
        return Mathf.Abs(point.x - c.x) <= halfHitSize && Mathf.Abs(point.z - c.z) <= halfHitSize;
    }

    public float SqrDistance(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        IdleGame.PlateState state = game.PlateStateOf(station);
        bool visible = state != IdleGame.PlateState.Hidden;
        if (visual.gameObject.activeSelf != visible) visual.gameObject.SetActive(visible);
        if (!visible) { shownState = -1; return; }

        long price = game.PlatePrice(station);
        bool affordable = game.Cash >= price;
        if ((int)state != shownState)
        {
            shownState = (int)state;
            arrow.SetActive(state == IdleGame.PlateState.Open);
            newTag.SetActive(state == IdleGame.PlateState.Unlock);
            shownPrice = -1;
        }
        if (price != shownPrice)
        {
            shownPrice = price;
            string label = NumberFormat.Short(price);
            int size = Mathf.RoundToInt(90f * Mathf.Min(1f, 3.0f / Mathf.Max(1, label.Length)));   // un prix tient toujours dans la plaque
            foreach (TextMesh t in priceTexts) { t.text = label; t.fontSize = size; }
        }
        if (affordable != shownAffordable || pulseStart == 0f)
        {
            shownAffordable = affordable;
            pulseStart = Time.time;
            borderAffordable.SetActive(affordable);
            borderWaiting.SetActive(!affordable);
            if (arrowRenderer != null) arrowRenderer.sharedMaterial = affordable ? arrowOn : arrowOff;
            priceTexts[0].color = affordable ? affordableColor : waitingColor;
        }
        float pulse = affordable && !MotionSettings.ReduceMotion ? 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed) : 1f;
        visual.localScale = new Vector3(pulse, 1f, pulse);
    }
}
