using UnityEngine;
using UnityEngine.UI;

// P2 : à la livraison d'une commande, un bandeau « Commande livrée ! » glisse depuis le haut avec le nom du client,
// puis les puces « +$ » et « +Points Pro » s'envolent vers les pastilles correspondantes (dont les compteurs défilent).
public class OrderDoneBanner : MonoBehaviour
{
    [SerializeField] OrderBoard board;
    [SerializeField] RectTransform banner;
    [SerializeField] Text titleText;
    [SerializeField] Text clientText;
    [SerializeField] RectTransform moneyChip;
    [SerializeField] Text moneyText;
    [SerializeField] RectTransform proChip;
    [SerializeField] Text proText;
    [SerializeField] RectTransform moneyTarget;   // pastille d'argent (en haut à gauche)
    [SerializeField] RectTransform proTarget;     // pastille Points Pro
    [SerializeField] float slideIn = 0.35f;
    [SerializeField] float hold = 1.5f;           // le bandeau reste lisible avant que les puces partent
    [SerializeField] float fly = 0.7f;
    [SerializeField] float slideOut = 0.35f;
    [SerializeField] float hiddenOffset = 420f;   // décalage vers le haut quand le bandeau est caché (pixels de canvas)

    float time = -1f;
    Vector2 shownPosition;
    Vector3 moneyStart, proStart;
    bool chipsLaunched;

    public static float SlideEase(float t)   // sortie en douceur avec un léger dépassement
    {
        t = Mathf.Clamp01(t);
        float c = 1.4f;
        float u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }

    public static float FlyEase(float t)     // démarrage lent puis accélération vers la pastille
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    void Awake()
    {
        shownPosition = banner.anchoredPosition;
        moneySlot = moneyChip.anchoredPosition;
        proSlot = proChip.anchoredPosition;
        banner.gameObject.SetActive(false);
    }

    void OnEnable() { board.OnCompleted += OnCompleted; }
    void OnDisable() { board.OnCompleted -= OnCompleted; }

    void OnCompleted(OrderBoard.Offer offer)
    {
        OrderConfig.Client c = board.Config.Clients[Mathf.Clamp(offer.clientIndex, 0, board.Config.Clients.Length - 1)];
        titleText.text = Loc.T("order.done");
        clientText.text = OrderConfig.ClientName(c, offer.clientIndex);
        moneyText.text = "+" + NumberFormat.Short(offer.reward);
        proText.text = "+" + NumberFormat.Short(offer.pro);
        proChip.gameObject.SetActive(offer.pro > 0);
        moneyChip.gameObject.SetActive(true);
        banner.gameObject.SetActive(true);
        banner.SetAsLastSibling();
        time = 0f;
        chipsLaunched = false;
        banner.anchoredPosition = shownPosition + Vector2.up * hiddenOffset;
        ReturnChips();   // si une commande en suit une autre, les puces repartent de leur place
    }

    void Update()
    {
        if (time < 0f) return;
        time += Time.deltaTime;

        float flyStart = slideIn + hold;
        float flyEnd = flyStart + fly;
        float outStart = flyEnd - 0.2f;
        float end = outStart + slideOut;

        float slide = time < slideIn ? SlideEase(time / slideIn) : time < outStart ? 1f : 1f - FlyEase((time - outStart) / slideOut);
        banner.anchoredPosition = Vector2.Lerp(shownPosition + Vector2.up * hiddenOffset, shownPosition, slide);

        if (time >= flyStart)
        {
            if (!chipsLaunched)
            {
                chipsLaunched = true;
                moneyStart = moneyChip.position;
                proStart = proChip.position;
            }
            float k = FlyEase((time - flyStart) / fly);
            Fly(moneyChip, moneyStart, moneyTarget, k);
            Fly(proChip, proStart, proTarget, k);
        }

        if (time >= end)
        {
            time = -1f;
            banner.gameObject.SetActive(false);
            ReturnChips();
        }
    }

    static void Fly(RectTransform chip, Vector3 start, RectTransform target, float k)
    {
        if (!chip.gameObject.activeSelf || target == null) return;
        chip.position = Vector3.Lerp(start, target.position, k);
        chip.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, k);
        if (k >= 1f) chip.gameObject.SetActive(false);
    }

    void ReturnChips()
    {
        // les puces reviennent à leur place dans le bandeau pour la prochaine commande
        moneyChip.localScale = Vector3.one;
        proChip.localScale = Vector3.one;
        moneyChip.anchoredPosition = moneySlot;
        proChip.anchoredPosition = proSlot;
    }

    Vector2 moneySlot, proSlot;
}
