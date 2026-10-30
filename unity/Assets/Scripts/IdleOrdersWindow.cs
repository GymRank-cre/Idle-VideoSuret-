using UnityEngine;

// Fenêtre Commandes (U3c) : d'abord les commandes en cours, puis les offres. Les cartes sont créées une fois pour toutes et réutilisées ;
// la liste n'est reconstruite que si les commandes changent, les barres avancent sans la toucher.
public class IdleOrdersWindow : MonoBehaviour
{
    [SerializeField] IdleSheet sheet;
    [SerializeField] IdleOrderCard[] cards;
    [SerializeField] GameObject emptyCard;
    [SerializeField] Sprite[] portraits;              // même ordre que les clés client.N.name
    [SerializeField] RectTransform content;
    [SerializeField, Min(1f)] float cardPitch = 487f;
    [SerializeField, Min(0f)] float topPadding = 6f;
    [SerializeField, Min(0.02f)] float checkEvery = 0.1f;

    int shownSignature = int.MinValue;
    float timer;

    void OnEnable()
    {
        shownSignature = int.MinValue;
        timer = 0f;
        Refresh();
    }

    void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = checkEvery;
        Refresh();
    }

    void Refresh()
    {
        IdleGame game = IdleHost.Instance.Game;
        int signature = Signature(game);
        if (signature != shownSignature)
        {
            shownSignature = signature;
            Rebuild(game);
        }
        for (int i = 0; i < game.Active.Count && i < cards.Length; i++) cards[i].Progress(game.Active[i]);
    }

    static int Signature(IdleGame game)
    {
        int s = game.ActiveSlots;
        foreach (IdleGame.Offer o in game.Active) s = s * 31 + o.id;
        s = s * 31 + 1;
        foreach (IdleGame.Offer o in game.Offers) s = s * 31 + o.id;
        return s;
    }

    void Rebuild(IdleGame game)
    {
        bool full = game.Active.Count >= game.ActiveSlots;
        int shown = 0;
        foreach (IdleGame.Offer o in game.Active) Place(game, o, true, full, ref shown);
        foreach (IdleGame.Offer o in game.Offers) Place(game, o, false, full, ref shown);
        for (int i = shown; i < cards.Length; i++) cards[i].gameObject.SetActive(false);

        bool empty = shown == 0;
        emptyCard.SetActive(empty);
        float height = topPadding + (empty ? 1 : shown) * cardPitch;
        content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        sheet.SetContentHeight(height);
    }

    void Place(IdleGame game, IdleGame.Offer order, bool active, bool full, ref int index)
    {
        if (index >= cards.Length) return;
        IdleOrderCard card = cards[index];
        card.gameObject.SetActive(true);
        var rt = (RectTransform)card.transform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -topPadding - index * cardPitch);
        card.Show(game, order, portraits[order.client % portraits.Length], active, full);
        index++;
    }
}
